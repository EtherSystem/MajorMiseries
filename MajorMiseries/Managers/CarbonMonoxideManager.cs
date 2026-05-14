using UnitySceneManager = UnityEngine.SceneManagement.SceneManager;
using static MajorMiseries.Afflictions.COExposure;
using static MajorMiseries.Afflictions.COPoisoning;
using Random = UnityEngine.Random;

namespace MajorMiseries
{
    internal static partial class AfflictionLogic
    {
        // ============================================================================
        //                         Carbon Monoxide Exposure
        // ============================================================================

        private const float CO_MIN_FIRE_BURN_HOURS = 2f;
        private const float CO_ROLL_INTERVAL_HOURS = 10f / 60f;      // 10 in-game minutes
        private const float CO_EXPOSURE_ROLL_CHANCE = 10f;           // percent per roll, per valid fire
        private const float CO_LINGER_AFTER_FIRE_OUT_HOURS = 2f;     // contaminated scene lingers for 2h after last valid fire

        private const float CO_RESPIRATOR_CANISTER_DRAIN_MULTIPLIER = 0.5f;
        private const float CO_RESPIRATOR_CANISTER_SECONDS_PER_GAME_HOUR = 300f;

        private sealed class CORiskSceneState
        {
            public float LastRollTimeHours = -999f;
            public float LastValidFireSeenTimeHours = -999f;
            public bool SceneContaminated = false;
        }

        private static readonly Dictionary<string, CORiskSceneState> _coSceneStates = [];

        private enum CORespiratorState
        {
            Unknown,
            Unequipped,
            EquippedInactive,
            Protected
        }

        private static CORespiratorState s_CORespiratorLastLoggedState = CORespiratorState.Unknown;

        private static CORespiratorState GetCORespiratorState()
        {
            try
            {
                if (!RespiratorManager.IsEquipped) return CORespiratorState.Unequipped;

                var respirator = RespiratorManager.CurrentEquipped;
                if (respirator == null) return CORespiratorState.Unequipped;

                bool protectionActive = RespiratorManager.IsProtectionActive() || respirator.HasActiveProtection;

                return protectionActive ? CORespiratorState.Protected : CORespiratorState.EquippedInactive;
            }
            catch
            {
                return CORespiratorState.Unequipped;
            }
        }

        internal static void ResetCORespiratorProtectionState()
        {
            s_CORespiratorLastLoggedState = CORespiratorState.Unknown;
        }

        internal static float UpdateCORespiratorProtectionAndGetUnprotectedHours(float gameHoursPassed, string sceneName)
        {
            if (gameHoursPassed <= 0f) return 0f;

            CORespiratorState respiratorState = GetCORespiratorState();
            float unprotectedGameHoursPassed = gameHoursPassed;

            if (respiratorState == CORespiratorState.Protected)
            {
                unprotectedGameHoursPassed = 0f;

                try
                {
                    var respirator = RespiratorManager.CurrentEquipped;

                    if (respirator != null)
                    {
                        var canister = respirator.m_AttachedCanister;

                        if (canister != null && canister.IsValid)
                        {
                            GearItem canisterGear = canister.GearItem;

                            if (canisterGear != null)
                            {
                                float durationSeconds = Mathf.Max(1f, canister.m_ProtectionDurationRTSeconds);
                                float conditionBefore = canister.NormalizedCondition;

                                float filterSecondsToConsume = gameHoursPassed * CO_RESPIRATOR_CANISTER_SECONDS_PER_GAME_HOUR * CO_RESPIRATOR_CANISTER_DRAIN_MULTIPLIER;

                                float conditionDrain = filterSecondsToConsume / durationSeconds;
                                float conditionAfter = Mathf.Clamp01(conditionBefore - conditionDrain);

                                if (!Mathf.Approximately(conditionBefore, conditionAfter))
                                {
                                    canisterGear.SetNormalizedHP(conditionAfter, false);

                                    if (conditionBefore > 0f && conditionAfter <= 0f)
                                    {
                                        float protectedFraction = conditionDrain > 0f ? Mathf.Clamp01(conditionBefore / conditionDrain) : 1f;

                                        unprotectedGameHoursPassed = gameHoursPassed * (1f - protectedFraction);
                                        respiratorState = CORespiratorState.EquippedInactive;

                                        RespiratorManager.MaybeForceExpireCanister();
                                        Core.Log($"Respirator canister depleted in '{sceneName}' while blocking CO exposure.");
                                    }
                                }
                            }
                        }
                        else
                        {
                            unprotectedGameHoursPassed = gameHoursPassed;
                            respiratorState = CORespiratorState.EquippedInactive;
                        }
                    }
                    else
                    {
                        unprotectedGameHoursPassed = gameHoursPassed;
                        respiratorState = CORespiratorState.Unequipped;
                    }
                }
                catch
                {
                    unprotectedGameHoursPassed = gameHoursPassed;
                    respiratorState = CORespiratorState.Unequipped;
                }
            }

            if (respiratorState != s_CORespiratorLastLoggedState)
            {
                CORespiratorState previousState = s_CORespiratorLastLoggedState;
                s_CORespiratorLastLoggedState = respiratorState;

                bool suppressInitialUnequippedLog = previousState == CORespiratorState.Unknown && respiratorState == CORespiratorState.Unequipped;

                if (!suppressInitialUnequippedLog)
                {
                    switch (respiratorState)
                    {
                        case CORespiratorState.Protected:
                            Core.Log($"Respirator protection active in '{sceneName}' -> CO exposure and CO poisoning progression are blocked.");
                            break;

                        case CORespiratorState.EquippedInactive:
                            Core.Log($"Respirator equipped in '{sceneName}' but protection is inactive -> no usable canister, CO can still affect the survivor.");
                            break;

                        case CORespiratorState.Unequipped:
                            Core.Log($"Respirator unequipped in '{sceneName}' -> CO protection inactive.");
                            break;
                    }
                }
            }

            return unprotectedGameHoursPassed;
        }

        internal static void UpdateCOExposure(float gameHoursPassed)
        {
            if (!Settings.options.EnableCarbonMonoxide) return;

            if (gameHoursPassed <= 0f) return;

            TimeOfDay tod = GameManager.GetTimeOfDayComponent();
            if (tod == null) return;

            if (HasAffliction<COExposureAffliction>()) return;

            if (HasAffliction<COPoisoningAffliction>()) return;

            if (!IsPlayerInIndoorScene())
            {
                ResetCORespiratorProtectionState();
                return;
            }

            string sceneName = GetCurrentSceneName();
            if (string.IsNullOrEmpty(sceneName))
            {
                ResetCORespiratorProtectionState();
                return;
            }

            float nowHours = tod.GetHoursPlayedNotPaused();
            CORiskSceneState state = GetOrCreateCOSceneState(sceneName);

            bool hasValidFire = TryGetIndoorValidCOFireInfo(nowHours, out float qualifyingSinceHours, out int eligibleFireCount);
            bool sceneStillContaminated = state.SceneContaminated && IsSceneStillCOContaminated(state, nowHours, hasValidFire);

            float unprotectedGameHoursPassed = gameHoursPassed;
            bool coHazardActive = hasValidFire || sceneStillContaminated;

            if (coHazardActive)
            {
                unprotectedGameHoursPassed = UpdateCORespiratorProtectionAndGetUnprotectedHours(gameHoursPassed, sceneName);

                if (unprotectedGameHoursPassed <= 0f)
                {
                    if (hasValidFire)
                    {
                        state.LastValidFireSeenTimeHours = nowHours;
                        state.LastRollTimeHours = nowHours;
                    }

                    return;
                }
            }
            else
            {
                ResetCORespiratorProtectionState();
            }

            if (hasValidFire)
            {
                state.LastValidFireSeenTimeHours = nowHours;

                if (state.LastRollTimeHours < 0f)
                {
                    state.LastRollTimeHours = unprotectedGameHoursPassed < gameHoursPassed ? nowHours - unprotectedGameHoursPassed : qualifyingSinceHours;
                }
                else if (unprotectedGameHoursPassed < gameHoursPassed)
                {
                    state.LastRollTimeHours = Mathf.Max(state.LastRollTimeHours, nowHours - unprotectedGameHoursPassed);
                }
            }

            if (state.SceneContaminated)
            {
                if (IsSceneStillCOContaminated(state, nowHours, hasValidFire))
                {
                    Core.Log($"CO contaminated scene re-entry -> applying COExposure immediately in scene '{sceneName}'.");
                    new COExposureAffliction(AfflictionBodyArea.Head).Start();
                    AfflictionSaveHelper.QueueSurvivalSave();
                    return;
                }

                Core.Log($"CO scene contamination expired in scene '{sceneName}'.");
                ResetCOSceneState(state);
            }

            if (!hasValidFire) return;

            float elapsed = nowHours - state.LastRollTimeHours;
            if (elapsed < CO_ROLL_INTERVAL_HOURS) return;

            int rollCount = Mathf.FloorToInt(elapsed / CO_ROLL_INTERVAL_HOURS);
            if (rollCount <= 0) return;

            float rollChance = GetCORollChanceForFireCount(eligibleFireCount);

            for (int i = 0; i < rollCount; i++)
            {
                float roll = Random.Range(0f, 100f);
                Core.Log($"CO roll -> fires={eligibleFireCount} chance={rollChance:0.##}% roll={roll:0.##} scene='{sceneName}'");

                if (roll <= rollChance)
                {
                    state.SceneContaminated = true;
                    state.LastValidFireSeenTimeHours = nowHours;
                    state.LastRollTimeHours += (i + 1) * CO_ROLL_INTERVAL_HOURS;

                    Core.Log($"CO roll succeeded -> scene '{sceneName}' is now contaminated, applying COExposure. fires={eligibleFireCount}, chance={rollChance:0.##}%");
                    new COExposureAffliction(AfflictionBodyArea.Head).Start();
                    AfflictionSaveHelper.QueueSurvivalSave();
                    return;
                }
            }

            state.LastRollTimeHours += rollCount * CO_ROLL_INTERVAL_HOURS;
        }

        internal static bool IsPlayerStillInActiveCOScene()
        {
            if (!IsPlayerInIndoorScene()) return false;

            TimeOfDay tod = GameManager.GetTimeOfDayComponent();
            if (tod == null) return false;

            string sceneName = GetCurrentSceneName();
            if (string.IsNullOrEmpty(sceneName)) return false;

            if (!_coSceneStates.TryGetValue(sceneName, out CORiskSceneState? state) || state == null || !state.SceneContaminated) return false;

            float nowHours = tod.GetHoursPlayedNotPaused();
            bool hasValidFire = TryGetIndoorValidCOFireInfo(nowHours, out _, out _);

            if (hasValidFire)
            {
                state.LastValidFireSeenTimeHours = nowHours;
                return true;
            }

            if (IsSceneStillCOContaminated(state, nowHours, hasValidFire: false)) return true;

            Core.Log($"CO active scene check expired -> scene '{sceneName}' is no longer contaminated.");
            ResetCOSceneState(state);
            return false;
        }

        private static bool TryGetIndoorValidCOFireInfo(float nowHours, out float qualifyingSinceHours, out int eligibleFireCount)
        {
            qualifyingSinceHours = -1f;
            eligibleFireCount = 0;

            if (!IsPlayerInIndoorScene()) return false;

            if (FireManager.m_Fires == null) return false;

            int count = FireManager.m_Fires.Count;
            float longestBurnHours = -1f;

            for (int i = 0; i < count; i++)
            {
                Fire? fire = FireManager.m_Fires[i];
                if (fire == null) continue;

                if (!fire.IsBurning()) continue;

                if (!IsCOEligibleFire(fire)) continue;

                float burnHours = fire.GetBurningTimeTODHours();
                if (burnHours < CO_MIN_FIRE_BURN_HOURS) continue;

                eligibleFireCount++;

                if (burnHours > longestBurnHours)
                    longestBurnHours = burnHours;
            }

            if (eligibleFireCount <= 0 || longestBurnHours < CO_MIN_FIRE_BURN_HOURS) return false;

            qualifyingSinceHours = nowHours - (longestBurnHours - CO_MIN_FIRE_BURN_HOURS);
            return true;
        }

        private static float GetCORollChanceForFireCount(int eligibleFireCount)
        {
            if (eligibleFireCount <= 0) return 0f;

            return Mathf.Clamp(CO_EXPOSURE_ROLL_CHANCE * eligibleFireCount, 0f, 100f);
        }

        private static bool IsCOEligibleFire(Fire fire)
        {
            if (fire == null || !fire.IsBurning()) return false;

            bool isWoodStoveFire = IsWoodStoveFire(fire);
            bool isCampfireFire = IsCampfireFire(fire);

            if (isCampfireFire) return true;

            if (isWoodStoveFire) return IsFireBarrelWoodStove(fire);

            return false;
        }

        private static bool IsWoodStoveFire(Fire fire)
        {
            if (fire == null) return false;

            var woodStoves = FireManager.m_WoodStoves;
            if (woodStoves == null) return false;

            for (int i = 0; i < woodStoves.Count; i++)
            {
                WoodStove? woodStove = woodStoves[i];
                if (woodStove == null || woodStove.Fire == null) continue;

                if (SameFire(woodStove.Fire, fire)) return true;
            }

            return false;
        }

        private static bool IsCampfireFire(Fire fire)
        {
            if (fire == null) return false;

            if (fire.m_Campfire != null) return true;

            var campfires = FireManager.m_Campfires;
            if (campfires == null) return false;

            for (int i = 0; i < campfires.Count; i++)
            {
                Campfire? campfire = campfires[i];
                if (campfire == null || campfire.Fire == null) continue;

                if (SameFire(campfire.Fire, fire)) return true;
            }

            return false;
        }

        private static bool IsFireBarrelWoodStove(Fire fire)
        {
            WoodStove? woodStove = FindWoodStoveForFire(fire);
            if (woodStove == null) return false;

            string objectName = woodStove.gameObject != null ? woodStove.gameObject.name ?? string.Empty : string.Empty;
            if (objectName.Contains("FireBarrel", StringComparison.OrdinalIgnoreCase)) return true;

            string path = GetTransformPath(woodStove.transform);
            if (path.Contains("FireBarrel", StringComparison.OrdinalIgnoreCase)) return true;

            return false;
        }

        private static WoodStove? FindWoodStoveForFire(Fire fire)
        {
            if (fire == null) return null;

            var woodStoves = FireManager.m_WoodStoves;
            if (woodStoves == null) return null;

            for (int i = 0; i < woodStoves.Count; i++)
            {
                WoodStove? woodStove = woodStoves[i];
                if (woodStove == null || woodStove.Fire == null) continue;

                if (SameFire(woodStove.Fire, fire)) return woodStove;
            }

            return null;
        }

        private static bool SameFire(Fire a, Fire b)
        {
            if (a == null || b == null) return false;

            return a.GetInstanceID() == b.GetInstanceID();
        }

        private static string GetTransformPath(Transform t)
        {
            if (t == null) return "<null>";

            string path = t.name;
            Transform current = t.parent;

            while (current != null)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }

            return path;
        }

        private static bool IsSceneStillCOContaminated(CORiskSceneState state, float nowHours, bool hasValidFire)
        {
            if (!state.SceneContaminated) return false;

            if (hasValidFire) return true;

            if (state.LastValidFireSeenTimeHours < 0f) return false;

            return (nowHours - state.LastValidFireSeenTimeHours) < CO_LINGER_AFTER_FIRE_OUT_HOURS;
        }

        private static CORiskSceneState GetOrCreateCOSceneState(string sceneName)
        {
            if (!_coSceneStates.TryGetValue(sceneName, out CORiskSceneState? state) || state == null)
            {
                state = new CORiskSceneState();
                _coSceneStates[sceneName] = state;
            }

            return state;
        }

        private static void ResetCOSceneState(CORiskSceneState state)
        {
            state.SceneContaminated = false;
            state.LastRollTimeHours = -999f;
            state.LastValidFireSeenTimeHours = -999f;
        }

        private static string GetCurrentSceneName()
        {
            return UnitySceneManager.GetActiveScene().name;
        }

        private static bool IsPlayerInIndoorScene()
        {
            Weather? weather = GameManager.GetWeatherComponent();
            if (weather == null) return false;

            return weather.IsIndoorScene();
        }
    }
}