using static MajorMiseries.Afflictions.BlackLungRisk;
using static MajorMiseries.Afflictions.BlackLung;

namespace MajorMiseries
{
    internal static partial class AfflictionLogic
    {
        // =======================================================================================
        //                                 Black Lung logic
        // =======================================================================================

        private static readonly HashSet<string> s_BlackLungScenes = new(StringComparer.OrdinalIgnoreCase)
        {
            // scene to filter by name :
            "DamCaveTransitionZone", // WR <-> PV transition cave
            "MineTransitionZone", // CH Aurora mine
            "HighwayMineTransitionZone", // DP n3 coal mine
            "CanneryMarshTransitionCave", // BI <-> FM transition cave
            "CanyonRoadCave", // KP North <-> KP South transition cave
            "RiverValleyTransitionCave", // MT <-> HRV transition cave
            "AshCaveA", // AC <-> TWM transition cave
            "AshCaveB", // AC cave between Long Falls and Miner's Folly
            "AshMine",     // AC Gold Mine
            "IceCaveA", // HRV South ice cave system
            "IceCaveB", // HRV North ice cave system
            "MountainCaveA", // TWM cave close to the engine down a ravine
            "MountainCaveB", // TWM secluded shelf cave
            "MountainTownCaveA", // MT cave close to crashed plane
            "MountainTownCaveB", // MT <-> ML transition cave
            "BlackrockMineA", // Blackrock Last Prospect
            "WhalingMine", // DP Abandoned Mine n5
            "BlackrockCaveA", // transition zone between TM and Blackrock
            "BlackrockSteamTunnelsASurvival", // steam tunnels in blackrock prison
            "CaveB", // PV Misty Falls picnic area cave
            "CaveC", // DP Broken bridge falls cave
            "CaveD",  // FM Marsh Ridge cave
            "HubCave", // Far Territories cave system
            "MiningRegionMine", // ZoC Langstone mine
            "MineConcentratorBuilding", // ZoC Concentrator
            "MountainPassCaveA", // SP cave system
            "MountainPassCaveB" // SP abandoned mine
        };

        internal static bool IsBlackLungScene(string? sceneName)
        {
            return !string.IsNullOrEmpty(sceneName) && s_BlackLungScenes.Contains(sceneName);
        }

        private const float BLACK_LUNG_EXPOSURE_MAX = 75f;
        private const float BLACK_LUNG_RISK_START_THRESHOLD = 75f;

        private const float BLACK_LUNG_EXPOSURE_TIME_TO_RISK_HOURS = 14f * 24f; // 336h = 14 days to reach 75 exposure
        private const float BLACK_LUNG_RISK_TIME_TO_BLACK_LUNG_HOURS = BLACK_LUNG_EXPOSURE_TIME_TO_RISK_HOURS / 3f; // 112h = time for risk to reach 100

        private const float BLACK_LUNG_DECAY_SLOWDOWN_MULTIPLIER = 4f; // recovery is 4x slower than buildup

        private const float BLACK_LUNG_EXPOSURE_GAIN_PER_HOUR = BLACK_LUNG_EXPOSURE_MAX / BLACK_LUNG_EXPOSURE_TIME_TO_RISK_HOURS;
        private const float BLACK_LUNG_EXPOSURE_DECAY_PER_HOUR = BLACK_LUNG_EXPOSURE_GAIN_PER_HOUR / BLACK_LUNG_DECAY_SLOWDOWN_MULTIPLIER; // 1344h from 75 to 0

        private const float BLACK_LUNG_RISK_GAIN_PER_HOUR = 100f / BLACK_LUNG_RISK_TIME_TO_BLACK_LUNG_HOURS;
        private const float BLACK_LUNG_RISK_DECAY_PER_HOUR = BLACK_LUNG_RISK_GAIN_PER_HOUR / BLACK_LUNG_DECAY_SLOWDOWN_MULTIPLIER; // 448h from 100 to 0

        private const float BLACK_LUNG_SLEEP_RECOVERY_MULTIPLIER = 10f;
        private const float BLACK_LUNG_COAL_SCENE_WORSENING_MULTIPLIER = 10f;
        private const float BLACK_LUNG_RESPIRATOR_CANISTER_DRAIN_MULTIPLIER = 0.05f;
        private const float BLACK_LUNG_RESPIRATOR_CANISTER_SECONDS_PER_GAME_HOUR = 300f;

        private static bool _blackLungSleepTrackingActive = false;
        private static float _blackLungTrackedSleepHours = 0f;

        private static bool s_BlackLungExposureSceneActive = false;
        private static string s_BlackLungExposureSceneName = string.Empty;
        private static float s_BlackLungExposureSceneStartExposure = 0f;

        private const float BLACK_LUNG_WORSENING_LOG_INTERVAL_HOURS = 1f;
        private static string s_BlackLungWorseningSceneName = string.Empty;
        private static float s_BlackLungWorseningLogHoursAdded = 0f;
        private static float s_BlackLungWorseningLogGameHours = 0f;

        internal static float GetBlackLungExposureGainPerHour() => BLACK_LUNG_EXPOSURE_GAIN_PER_HOUR;
        internal static float GetBlackLungExposureDecayPerHour() => BLACK_LUNG_EXPOSURE_DECAY_PER_HOUR;
        internal static float GetBlackLungExposureMax() => BLACK_LUNG_EXPOSURE_MAX;
        internal static float GetBlackLungRiskGainPerHour() => BLACK_LUNG_RISK_GAIN_PER_HOUR;
        internal static float GetBlackLungRiskDecayPerHour() => BLACK_LUNG_RISK_DECAY_PER_HOUR;

        private static void ResetBlackLungWorseningLog()
        {
            s_BlackLungWorseningSceneName = string.Empty;
            s_BlackLungWorseningLogHoursAdded = 0f;
            s_BlackLungWorseningLogGameHours = 0f;
        }

        private enum BlackLungRespiratorState
        {
            Unknown,
            Unequipped,
            EquippedInactive,
            Protected
        }

        private static BlackLungRespiratorState s_BlackLungRespiratorLastLoggedState = BlackLungRespiratorState.Unknown;

        private static BlackLungRespiratorState GetBlackLungRespiratorState()
        {
            try
            {
                if (!RespiratorManager.IsEquipped) return BlackLungRespiratorState.Unequipped;

                var respirator = RespiratorManager.CurrentEquipped;
                if (respirator == null) return BlackLungRespiratorState.Unequipped;

                bool protectionActive = RespiratorManager.IsProtectionActive() || respirator.HasActiveProtection;

                return protectionActive ? BlackLungRespiratorState.Protected : BlackLungRespiratorState.EquippedInactive;
            }
            catch
            {
                return BlackLungRespiratorState.Unequipped;
            }
        }

        internal static bool IsBlackLungRespiratorProtected()
        {
            return GetBlackLungRespiratorState() == BlackLungRespiratorState.Protected;
        }

        private static void AccumulateBlackLungWorseningLog(string sceneName, float gameHoursPassed, float hoursAdded)
        {
            if (gameHoursPassed <= 0f || hoursAdded <= 0f) return;

            if (string.IsNullOrEmpty(s_BlackLungWorseningSceneName))
            {
                s_BlackLungWorseningSceneName = sceneName;
            }
            else if (!string.Equals(s_BlackLungWorseningSceneName, sceneName, StringComparison.OrdinalIgnoreCase))
            {
                FlushBlackLungWorseningLog();
                s_BlackLungWorseningSceneName = sceneName;
            }

            s_BlackLungWorseningLogHoursAdded += hoursAdded;
            s_BlackLungWorseningLogGameHours += gameHoursPassed;

            if (s_BlackLungWorseningLogGameHours >= BLACK_LUNG_WORSENING_LOG_INTERVAL_HOURS) FlushBlackLungWorseningLog();
        }

        private static void FlushBlackLungWorseningLog()
        {
            if (s_BlackLungWorseningLogGameHours <= 0f || s_BlackLungWorseningLogHoursAdded <= 0f)
            {
                ResetBlackLungWorseningLog();
                return;
            }

            string scenePart = string.IsNullOrEmpty(s_BlackLungWorseningSceneName) ? string.Empty : $" in '{s_BlackLungWorseningSceneName}'";

            Core.Log($"BlackLung worsened by coal exposure{scenePart} -> +{s_BlackLungWorseningLogHoursAdded:0.###}h remaining over {s_BlackLungWorseningLogGameHours * 60f:0} min.");

            ResetBlackLungWorseningLog();
        }

        private static void BeginBlackLungExposureScene(string sceneName)
        {
            s_BlackLungExposureSceneActive = true;
            s_BlackLungExposureSceneName = sceneName;
            s_BlackLungExposureSceneStartExposure = Core.State.BlackLungExposure;

            Core.Log($"BlackLung exposure scene entered: '{sceneName}' -> exposure will start increasing.");
        }

        private static void EndBlackLungExposureScene(string? nextSceneName, bool nextSceneWillIncreaseExposure, bool exposureWillDecrease)
        {
            if (!s_BlackLungExposureSceneActive) return;

            float totalExposure = Core.State.BlackLungExposure;
            float sceneExposureGenerated = Mathf.Max(0f, totalExposure - s_BlackLungExposureSceneStartExposure);

            string message = $"BlackLung exposure scene exited: '{s_BlackLungExposureSceneName}' -> total exposure {totalExposure:0.###}, scene gain +{sceneExposureGenerated:0.###}.";

            if (exposureWillDecrease)
            {
                message += " Exposure stops increasing and will now decrease.";
            }
            else if (nextSceneWillIncreaseExposure && !string.IsNullOrEmpty(nextSceneName))
            {
                message += $" Exposure will continue increasing in scene '{nextSceneName}'.";
            }
            else
            {
                message += " Exposure stops increasing.";
            }

            Core.Log(message);

            s_BlackLungExposureSceneActive = false;
            s_BlackLungExposureSceneName = string.Empty;
            s_BlackLungExposureSceneStartExposure = 0f;
        }

        private static void UpdateBlackLungExposureSceneTracking(string sceneName, bool inCoalScene, bool canGainExposure, bool exposureWillDecreaseOutsideCoal)
        {
            if (canGainExposure)
            {
                if (!s_BlackLungExposureSceneActive)
                {
                    BeginBlackLungExposureScene(sceneName);
                    return;
                }

                if (!string.Equals(s_BlackLungExposureSceneName, sceneName, StringComparison.OrdinalIgnoreCase))
                {
                    EndBlackLungExposureScene(sceneName, true, false);
                    BeginBlackLungExposureScene(sceneName);
                }

                return;
            }

            if (!s_BlackLungExposureSceneActive)
                return;

            if (inCoalScene)
            {
                if (!string.Equals(s_BlackLungExposureSceneName, sceneName, StringComparison.OrdinalIgnoreCase))
                {
                    EndBlackLungExposureScene(sceneName, false, false);
                }

                return;
            }

            EndBlackLungExposureScene(sceneName, false, exposureWillDecreaseOutsideCoal);
        }

        internal static void UpdateBlackLungExposure(float gameHoursPassed)
        {
            if (!Settings.options.EnableBlackLung) return;

            if (gameHoursPassed <= 0f) return;

            string sceneName = GameManager.m_ActiveScene;
            if (string.IsNullOrEmpty(sceneName)) return;

            bool inCoalScene = IsBlackLungScene(sceneName);
            float unprotectedGameHoursPassed = gameHoursPassed;

            if (inCoalScene)
            {
                BlackLungRespiratorState respiratorState = GetBlackLungRespiratorState();

                if (respiratorState == BlackLungRespiratorState.Protected)
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

                                    float filterSecondsToConsume = gameHoursPassed * BLACK_LUNG_RESPIRATOR_CANISTER_SECONDS_PER_GAME_HOUR * BLACK_LUNG_RESPIRATOR_CANISTER_DRAIN_MULTIPLIER;

                                    float conditionDrain = filterSecondsToConsume / durationSeconds;
                                    float conditionAfter = Mathf.Clamp01(conditionBefore - conditionDrain);

                                    if (!Mathf.Approximately(conditionBefore, conditionAfter))
                                    {
                                        canisterGear.SetNormalizedHP(conditionAfter, false);

                                        if (conditionBefore > 0f && conditionAfter <= 0f)
                                        {
                                            float protectedFraction = conditionDrain > 0f ? Mathf.Clamp01(conditionBefore / conditionDrain) : 1f;

                                            unprotectedGameHoursPassed = gameHoursPassed * (1f - protectedFraction);
                                            respiratorState = BlackLungRespiratorState.EquippedInactive;

                                            RespiratorManager.MaybeForceExpireCanister();
                                            Core.Log($"Respirator canister depleted in '{sceneName}' while blocking BlackLung exposure.");
                                        }
                                    }
                                }
                            }
                            else
                            {
                                unprotectedGameHoursPassed = gameHoursPassed;
                                respiratorState = BlackLungRespiratorState.EquippedInactive;
                            }
                        }
                        else
                        {
                            unprotectedGameHoursPassed = gameHoursPassed;
                            respiratorState = BlackLungRespiratorState.Unequipped;
                        }
                    }
                    catch
                    {
                        unprotectedGameHoursPassed = gameHoursPassed;
                        respiratorState = BlackLungRespiratorState.Unequipped;
                    }
                }

                if (respiratorState != s_BlackLungRespiratorLastLoggedState)
                {
                    BlackLungRespiratorState previousState = s_BlackLungRespiratorLastLoggedState;
                    s_BlackLungRespiratorLastLoggedState = respiratorState;

                    bool suppressInitialUnequippedLog = previousState == BlackLungRespiratorState.Unknown && respiratorState == BlackLungRespiratorState.Unequipped;

                    if (!suppressInitialUnequippedLog)
                    {
                        switch (respiratorState)
                        {
                            case BlackLungRespiratorState.Protected:
                                Core.Log($"Respirator protection active in '{sceneName}' -> BlackLung exposure, risk and worsening are blocked.");
                                break;

                            case BlackLungRespiratorState.EquippedInactive:
                                Core.Log($"Respirator equipped in '{sceneName}' but protection is inactive -> no usable canister, BlackLung can still worsen.");
                                break;

                            case BlackLungRespiratorState.Unequipped:
                                Core.Log($"Respirator unequipped in '{sceneName}' -> BlackLung protection inactive.");
                                break;
                        }
                    }
                }
            }
            else
            {
                s_BlackLungRespiratorLastLoggedState = BlackLungRespiratorState.Unknown;
                unprotectedGameHoursPassed = 0f;
            }

            BlackLungAffliction? activeBlackLung = GetAffliction<BlackLungAffliction>();
            bool hasBlackLungRisk = HasAffliction<BlackLungRiskAffliction>();

            UpdateBlackLungExposureSceneTracking(sceneName, inCoalScene, inCoalScene && unprotectedGameHoursPassed > 0f && activeBlackLung == null && !hasBlackLungRisk, activeBlackLung == null && !hasBlackLungRisk);

            if (activeBlackLung != null)
            {
                if (inCoalScene && unprotectedGameHoursPassed > 0f)
                {
                    float addedHours = unprotectedGameHoursPassed * BLACK_LUNG_COAL_SCENE_WORSENING_MULTIPLIER;
                    activeBlackLung.EndTime += addedHours;
                    AccumulateBlackLungWorseningLog(sceneName, unprotectedGameHoursPassed, addedHours);
                }
                else
                {
                    FlushBlackLungWorseningLog();
                }

                return;
            }

            FlushBlackLungWorseningLog();

            if (hasBlackLungRisk)
            {
                if (!Mathf.Approximately(Core.State.BlackLungExposure, BLACK_LUNG_RISK_START_THRESHOLD))
                {
                    float exposureBeforeLock = Core.State.BlackLungExposure;
                    Core.State.BlackLungExposure = BLACK_LUNG_RISK_START_THRESHOLD;
                    Core.Instance?.MarkDirty();
                    Core.Log($"BlackLungRisk active -> exposure locked at {BLACK_LUNG_RISK_START_THRESHOLD:0.###} ({exposureBeforeLock:0.###} => {Core.State.BlackLungExposure:0.###}).");
                }

                return;
            }

            float oldExposure = Core.State.BlackLungExposure;

            if (inCoalScene && unprotectedGameHoursPassed > 0f)
            {
                Core.State.BlackLungExposure = Mathf.Clamp(Core.State.BlackLungExposure + (unprotectedGameHoursPassed * BLACK_LUNG_EXPOSURE_GAIN_PER_HOUR), 0f, BLACK_LUNG_EXPOSURE_MAX);
            }
            else if (!inCoalScene)
            {
                Core.State.BlackLungExposure = Mathf.Clamp(Core.State.BlackLungExposure - (gameHoursPassed * BLACK_LUNG_EXPOSURE_DECAY_PER_HOUR), 0f, BLACK_LUNG_EXPOSURE_MAX);
            }

            if (!Mathf.Approximately(oldExposure, Core.State.BlackLungExposure))
            {
                Core.Instance?.MarkDirty();
            }

            if (inCoalScene && unprotectedGameHoursPassed > 0f && Core.State.BlackLungExposure >= BLACK_LUNG_RISK_START_THRESHOLD)
            {
                new BlackLungRiskAffliction(AfflictionBodyArea.Head).Start();
                AfflictionSaveHelper.QueueSurvivalSave();
            }
        }

        internal static void UpdateBlackLungSleepTracking(float gameHoursPassed)
        {
            if (!Settings.options.EnableBlackLung)
            {
                _blackLungSleepTrackingActive = false;
                _blackLungTrackedSleepHours = 0f;
                return;
            }

            if (gameHoursPassed <= 0f) return;

            Rest? rest = GameManager.GetRestComponent();
            bool hasBlackLung = GetAffliction<BlackLungAffliction>() != null;
            bool isSleeping = hasBlackLung && rest != null && rest.IsSleeping();

            if (isSleeping)
            {
                if (!_blackLungSleepTrackingActive)
                {
                    _blackLungSleepTrackingActive = true;
                    _blackLungTrackedSleepHours = 0f;

                    Core.Log("BlackLung sleep tracking started.");
                }

                _blackLungTrackedSleepHours += gameHoursPassed;
                return;
            }

            if (_blackLungSleepTrackingActive)
            {
                _blackLungSleepTrackingActive = false;

                float sleptHours = _blackLungTrackedSleepHours;
                _blackLungTrackedSleepHours = 0f;

                if (sleptHours > 0f)
                {
                    Core.Log($"BlackLung sleep tracking finalized -> actual={sleptHours:0.###}h");
                    ProcessBlackLungSleepRecovery(sleptHours);
                }
            }
        }
    }
}