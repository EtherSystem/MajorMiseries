using System.Collections;
using UnitySceneManager = UnityEngine.SceneManagement.SceneManager;
using AfflictionComponent.Components;
using MajorMiseries.Patches;
using MajorMiseries.Persistence;
using static MajorMiseries.Afflictions.BrokenArm;
using static MajorMiseries.Afflictions.BrokenLeg;
using static MajorMiseries.Afflictions.RequiemStagesAfflictions.Dirge;
using static MajorMiseries.Afflictions.RequiemStagesAfflictions.Knell;
using static MajorMiseries.Afflictions.RequiemStagesAfflictions.Omen;
using static MajorMiseries.Afflictions.RequiemStagesAfflictions.Requiem;
using static MajorMiseries.Afflictions.ScarredFlesh;
using static MajorMiseries.Afflictions.BlackLungRisk;
using static MajorMiseries.Afflictions.BlackLung;
using static MajorMiseries.Afflictions.COExposure;
using static MajorMiseries.Afflictions.COPoisoning;
using static MajorMiseries.Afflictions.CorpseSicknessRisk;
using static MajorMiseries.Afflictions.CorpseSickness;
using static MajorMiseries.Afflictions.SevereAnkleSprain;
using static MajorMiseries.Afflictions.SevereWristSprain;
using static MajorMiseries.Afflictions.Buffs.HomeComfort;
using static MajorMiseries.Afflictions.HomeSickness;
using static MajorMiseries.Afflictions.RegionalDistress;
using Random = UnityEngine.Random;

namespace MajorMiseries
{
    internal static class AfflictionLogic
    {
        // =================================================================
        //                        General Logic
        // =================================================================

        private static int _lastProcessedHour = -1;

        private const float REFRESH_INTERVAL_SECONDS = 0.25f;
        private static float _lastRefreshUnscaledTime = -999f;

        private const float VANILLA_MAX_CONDITION_HP = 100f;

        internal static void ResetRuntime()
        {
            _lastProcessedHour = -1;
            _lastRefreshUnscaledTime = -999f;
            _coSceneStates.Clear();

            s_BlackLungExposureSceneActive = false;
            s_BlackLungExposureSceneName = string.Empty;
            s_BlackLungExposureSceneStartExposure = 0f;
            ResetBlackLungWorseningLog();
            s_WasPlayerNearCorpseSource = false;
            s_LastCorpseSourceLabel = string.Empty;
            _blackLungSleepTrackingActive = false;
            _blackLungTrackedSleepHours = 0f;
            s_BlackLungRespiratorLastLoggedState = BlackLungRespiratorState.Unknown;
            s_CORespiratorLastLoggedState = CORespiratorState.Unknown;

            ResetCorpseTracking();

            StageGaugeLockVisuals.ResetRuntime();

            _applyingSevere = false;
            _pendingSevere = false;
            _severeWasActive = false;
            _loggedPendingSevereSuppression = false;

            _processingFallInjury = false;
            _conditionBeforeFall = -1f;

            _cache.Reset();
        }

        internal static bool IsReady()
        {
            var mgr = AfflictionManager.GetAfflictionManagerInstance();

            if (GameManager.GetConditionComponent() == null) return false;
            if (GameManager.GetPlayerManagerComponent() == null) return false;
            if (mgr == null) return false;
            if (mgr.m_Afflictions == null) return false;

            return true;
        }

        internal static void Tick()
        {
            if (!IsReady()) return;

            int totalHours = GetTotalHoursAliveFromGame();
            if (totalHours < 0) return;

            if (totalHours == _lastProcessedHour) return;

            _lastProcessedHour = totalHours;

            if (!Settings.options.EnableRequiemStages)
            {
                if (GetAppliedStage() != RequiemStage.None)
                {
                    CureAllStageAfflictions();
                    ForceRefreshEffects();
                }

                return;
            }

            TimeOfDay tod = GameManager.GetTimeOfDayComponent();
            if (tod != null)
            {
                int day = tod.GetDayNumber();
                int hour = Mathf.FloorToInt(tod.GetHour());
                float daysAlive = totalHours / 24f;

                Core.Log($"time check: day={day} hour={hour} totalHours={totalHours} days={daysAlive:0.###}");
            }

            RequiemStage stage = GetStageFromTotalHours(totalHours);
            RequiemStage appliedStage = GetAppliedStage();

            if (appliedStage == stage)
            {
                Core.Log($"stage unchanged: {stage}");
                return;
            }

            CureAllStageAfflictions();
            ApplyStage(stage);
            ForceRefreshEffects();
            ClampConditionToCurrentStageMax(stage);

            if (stage != RequiemStage.None)
            {
                string? locId = GetPopupStageLocId(stage);
                if (!string.IsNullOrEmpty(locId))
                {
                    DisplayStagePopup.ShowStagePopup(GetPopupStageNumber(stage), locId);
                }
            }

            Core.Log($"stage updated: {appliedStage} -> {stage}");
        }

        internal static void LogRequiemStagesDisabledByPlayer()
        {
            RequiemStage appliedStage = GetAppliedStage();

            if (appliedStage != RequiemStage.None) Core.Log($"requiem stages disabled by player -> cured {appliedStage}");

            else Core.Log("requiem stages disabled by player");
        }

        internal static void ApplyCurrentStageFromGame()
        {
            if (!IsReady()) return;

            int totalHours = GetTotalHoursAliveFromGame();
            if (totalHours < 0) return;

            _lastProcessedHour = totalHours;

            if (!Settings.options.EnableRequiemStages)
            {
                if (GetAppliedStage() != RequiemStage.None)
                {
                    CureAllStageAfflictions();
                }

                ForceRefreshEffects();
                return;
            }

            TimeOfDay tod = GameManager.GetTimeOfDayComponent();
            if (tod != null)
            {
                int day = tod.GetDayNumber();
                int hour = Mathf.FloorToInt(tod.GetHour());
                float daysAlive = totalHours / 24f;

                Core.Log($"load time check: day={day} hour={hour} totalHours={totalHours} days={daysAlive:0.###}");
            }

            RequiemStage stage = GetStageFromTotalHours(totalHours);
            RequiemStage appliedStage = GetAppliedStage();

            if (appliedStage == stage)
            {
                Core.Log($"load stage unchanged: {stage}");
                ForceRefreshEffects();
                return;
            }

            CureAllStageAfflictions();
            ApplyStage(stage);
            ForceRefreshEffects();
            ClampConditionToCurrentStageMax(stage);

            Core.Log($"stage synced: {appliedStage} -> {stage}");
        }

        internal static void ForceRefreshEffects()
        {
            _lastRefreshUnscaledTime = -999f;
            RefreshEffectsIfNeeded(force: true);
        }

        // ============================================================================
        //                              Requiem stages
        // ============================================================================

        internal static RequiemStage GetCurrentStage()
        {
            RefreshEffectsIfNeeded();

            if (_cache.Requiem) return RequiemStage.Requiem;
            if (_cache.Knell) return RequiemStage.Knell;
            if (_cache.Dirge) return RequiemStage.Dirge;
            if (_cache.Omen) return RequiemStage.Omen;

            return RequiemStage.None;
        }

        private static RequiemStage GetStageFromTotalHours(int totalHours)
        {
            float daysAlive = totalHours / 24f;

            GetConfiguredStageThresholds(out float omenThreshold, out float dirgeThreshold, out float knellThreshold, out float requiemThreshold);

            if (daysAlive >= requiemThreshold) return RequiemStage.Requiem;
            if (daysAlive >= knellThreshold) return RequiemStage.Knell;
            if (daysAlive >= dirgeThreshold) return RequiemStage.Dirge;
            if (daysAlive >= omenThreshold) return RequiemStage.Omen;

            return RequiemStage.None;
        }

        private static void GetConfiguredStageThresholds(out float omenThreshold, out float dirgeThreshold, out float knellThreshold, out float requiemThreshold)
        {
            omenThreshold = Settings.options.OmenThreshold;
            dirgeThreshold = Mathf.Max(omenThreshold, Settings.options.DirgeThreshold);
            knellThreshold = Mathf.Max(dirgeThreshold, Settings.options.KnellThreshold);
            requiemThreshold = Mathf.Max(knellThreshold, Settings.options.RequiemThreshold);
        }

        private static RequiemStage GetAppliedStage()
        {
            if (HasAffliction<RequiemAffliction>()) return RequiemStage.Requiem;
            if (HasAffliction<KnellAffliction>()) return RequiemStage.Knell;
            if (HasAffliction<DirgeAffliction>()) return RequiemStage.Dirge;
            if (HasAffliction<OmenAffliction>()) return RequiemStage.Omen;

            return RequiemStage.None;
        }

        private static void ApplyStage(RequiemStage stage)
        {
            if (!Settings.options.EnableRequiemStages) return;

            switch (stage)
            {
                case RequiemStage.Omen:
                    new OmenAffliction(AfflictionBodyArea.Head).Start();
                    Core.Log("omen applied");
                    break;

                case RequiemStage.Dirge:
                    new DirgeAffliction(AfflictionBodyArea.Head).Start();
                    Core.Log("dirge applied");
                    break;

                case RequiemStage.Knell:
                    new KnellAffliction(AfflictionBodyArea.Head).Start();
                    Core.Log("knell applied");
                    break;

                case RequiemStage.Requiem:
                    new RequiemAffliction(AfflictionBodyArea.Head).Start();
                    Core.Log("requiem applied");
                    break;
            }
        }

        private static void ClampConditionToCurrentStageMax(RequiemStage stage)
        {
            if (stage == RequiemStage.None) return;

            Condition? condition = GameManager.GetConditionComponent();
            if (condition == null) return;

            float before = condition.m_CurrentHP;

            float adjustedMaxHp = Mathf.Max(1f, VANILLA_MAX_CONDITION_HP + condition.GetAdjustedMaxHPModifier());

            if (before <= adjustedMaxHp) return;

            condition.m_CurrentHP = adjustedMaxHp;

            Core.Log($"stage condition clamp -> {stage}: {before:0.##} -> {condition.m_CurrentHP:0.##} / {adjustedMaxHp:0.##}");
        }

        private static void CureAllStageAfflictions()
        {
            var mgr = AfflictionManager.GetAfflictionManagerInstance();
            if (mgr?.m_Afflictions == null) return;

            for (int i = mgr.m_Afflictions.Count - 1; i >= 0; i--)
            {
                var affliction = mgr.m_Afflictions[i];
                if (affliction == null) continue;

                if (affliction is OmenAffliction || affliction is DirgeAffliction || affliction is KnellAffliction || affliction is RequiemAffliction)
                {
                    affliction.Cure();
                }
            }
        }

        private static int GetPopupStageNumber(RequiemStage stage)
        {
            return stage switch
            {
                RequiemStage.Omen => 1,
                RequiemStage.Dirge => 2,
                RequiemStage.Knell => 3,
                RequiemStage.Requiem => 4,
                _ => 0
            };
        }

        private static string? GetPopupStageLocId(RequiemStage stage)
        {
            return stage switch
            {
                RequiemStage.Omen => "GAMEPLAY_Stage1Name",
                RequiemStage.Dirge => "GAMEPLAY_Stage2Name",
                RequiemStage.Knell => "GAMEPLAY_Stage3Name",
                RequiemStage.Requiem => "GAMEPLAY_Stage4Name",
                _ => null
            };
        }

        // =======================================================================================
        //                              PredatorHostility Logic
        // =======================================================================================

        private const float PREDATOR_HOSTILITY_DECAY_DELAY_HOURS = 168f; // a week
        private const float PREDATOR_HOSTILITY_DECAY_PER_DAY = 2f;
        private const float PREDATOR_HOSTILITY_DECAY_PER_HOUR = PREDATOR_HOSTILITY_DECAY_PER_DAY / 24f;

        internal static bool IsPredatorHostilityEnabled()
        {
            RequiemStage stage = GetCurrentStage();
            return RequiemStagesEffects.IsPredatorHostilityEnabled(stage);
        }

        internal static int GetBasePredatorThreatLevel()
        {
            RequiemStage stage = GetCurrentStage();
            return RequiemStagesEffects.GetBasePredatorThreat(stage);
        }

        private static int GetDynamicPredatorThreatLevelFromHeat(float heat)
        {
            if (heat >= 30f) return 8;
            if (heat >= 25f) return 7;
            if (heat >= 20f) return 6;
            if (heat >= 16f) return 5;
            if (heat >= 12f) return 4;
            if (heat >= 9f) return 3;
            if (heat >= 6f) return 2;
            if (heat >= 3f) return 1;
            return 0;
        }

        internal static int GetDynamicPredatorThreatLevel()
        {
            return GetDynamicPredatorThreatLevelFromHeat(Core.State.PredatorHostility);
        }

        internal static int GetTotalPredatorThreatLevel()
        {
            if (!IsPredatorHostilityEnabled()) return 0;

            return GetBasePredatorThreatLevel() + GetDynamicPredatorThreatLevel();
        }

        internal static void RegisterPredatorKill(float hostilityAdded)
        {
            if (hostilityAdded <= 0f) return;

            if (!IsPredatorHostilityEnabled()) return;

            float before = Core.State.PredatorHostility;

            Core.State.PredatorHostility += hostilityAdded;
            Core.State.HoursSinceLastPredatorKill = 0f;

            Core.Instance?.MarkDirty();

            Core.Log($"predator kill -> hostility +{hostilityAdded:0.##} ({before:0.##} -> {Core.State.PredatorHostility:0.##})");
        }

        internal static float GetPredatorSmellDistanceMultiplier()
        {
            int threat = GetTotalPredatorThreatLevel();
            return threat > 0 ? 1f + (0.2f * threat) : 1f;
        }

        internal static float GetPredatorRushDistanceMultiplier()
        {
            int threat = GetTotalPredatorThreatLevel();
            return threat > 0 ? 1f + (0.2f * threat) : 1f;
        }

        internal static int GetAdditionalPredatorSpawnQuantity()
        {
            int threat = GetTotalPredatorThreatLevel();
            return threat > 0 ? threat / 2 : 0;
        }

        internal static void UpdatePredatorHostilityDecay(float gameHoursPassed)
        {
            if (gameHoursPassed <= 0f) return;

            if (Core.State.PredatorHostility <= 0f)
            {
                Core.State.PredatorHostility = 0f;
                Core.State.HoursSinceLastPredatorKill = 0f;
                return;
            }

            Core.State.HoursSinceLastPredatorKill += gameHoursPassed;
            Core.Instance?.MarkDirty();

            if (Core.State.HoursSinceLastPredatorKill < PREDATOR_HOSTILITY_DECAY_DELAY_HOURS) return;

            float before = Core.State.PredatorHostility;
            int dynamicThreatBefore = GetDynamicPredatorThreatLevelFromHeat(before);

            float decay = PREDATOR_HOSTILITY_DECAY_PER_HOUR * gameHoursPassed;

            Core.State.PredatorHostility = Mathf.Max(0f, Core.State.PredatorHostility - decay);

            if (!Mathf.Approximately(before, Core.State.PredatorHostility))
            {
                Core.Instance?.MarkDirty();

                int dynamicThreatAfter = GetDynamicPredatorThreatLevelFromHeat(Core.State.PredatorHostility);

                if (dynamicThreatBefore != dynamicThreatAfter || Core.State.PredatorHostility <= 0f)
                {
                    Core.Log($"predator hostility decay -> {before:0.##} -> {Core.State.PredatorHostility:0.##} | DynamicThreat:{dynamicThreatBefore}->{dynamicThreatAfter}");
                }
            }

            if (Core.State.PredatorHostility <= 0f)
            {
                Core.State.PredatorHostility = 0f;
                Core.State.HoursSinceLastPredatorKill = 0f;
                Core.Instance?.MarkDirty();
            }
        }

        // =======================================================================================
        //           BloodLoss converted to SevereLacerations Logic + ScarredFlesh
        // =======================================================================================

        private static bool _applyingSevere;
        private static bool _pendingSevere;
        private static bool _severeWasActive;
        private static bool _loggedPendingSevereSuppression;

        internal static bool IsApplyingSevereLacerationConversion => _applyingSevere;

        internal static void TryConvertPredatorBloodLossToSevereLaceration(BloodLoss bloodLoss, string cause)
        {
            RefreshEffectsIfNeeded();

            if (!RequiemStagesEffects.ShouldConvertPredatorBloodLossToSevere(GetCurrentStage())) return;

            PlayerManager player = GameManager.GetPlayerManagerComponent();
            if (player == null || player.PlayerIsDead()) return;

            string forwardedCause = string.IsNullOrWhiteSpace(cause) ? "Predator Attack" : cause;

            if (!IsPredatorBloodLossCause(forwardedCause))
            {
                Core.Log($"blood loss conversion ignored -> non-predator cause:'{forwardedCause}'");
                return;
            }

            SevereLacerations severe = GameManager.GetSevereLacerations();
            if (severe == null)
            {
                Core.Log($"blood loss conversion failed -> SevereLacerations component missing | cause:'{forwardedCause}'");
                return;
            }

            bool bloodLossStopped = TryStopConvertedBloodLoss(bloodLoss, forwardedCause);

            if (severe.HasAffliction())
            {
                Core.Log($"blood loss conversion consumed -> SevereLacerations already active | cause:'{forwardedCause}' | bloodLossStopped:{bloodLossStopped}");
                return;
            }

            if (_pendingSevere)
            {
                if (!_loggedPendingSevereSuppression)
                {
                    Core.Log($"blood loss conversion consumed -> SevereLacerations already pending | cause:'{forwardedCause}' | bloodLossStopped:{bloodLossStopped}");
                    _loggedPendingSevereSuppression = true;
                }

                return;
            }

            _pendingSevere = true;
            _loggedPendingSevereSuppression = false;

            Core.Log($"blood loss conversion queued -> SevereLacerations | cause:'{forwardedCause}' | bloodLossStopped:{bloodLossStopped} | stagesEnabled:{Settings.options.EnableRequiemStages} | stage:{GetCurrentStage()} | mode:{Settings.options.PredatorBloodLossToSevereLacerationsMode}");

            MelonCoroutines.Start(ApplySevereNextFrame(forwardedCause));
        }

        private static bool IsPredatorBloodLossCause(string cause)
        {
            if (string.IsNullOrWhiteSpace(cause)) return false;

            string lowerCause = cause.ToLowerInvariant();

            return lowerCause.Contains("wolf")
                || lowerCause.Contains("timberwolf")
                || lowerCause.Contains("bear")
                || lowerCause.Contains("cougar")
                || lowerCause.Contains("predator")
                || lowerCause.Contains("loup")
                || lowerCause.Contains("ours")
                || lowerCause.Contains("puma");
        }

        private static bool TryStopConvertedBloodLoss(BloodLoss bloodLoss, string cause)
        {
            if (bloodLoss == null) return false;

            string[] stopMethodNames =
            {
                "BloodLossStop",
                "BloodLossEnd",
                "StopBloodLoss",
                "Stop",
                "Cure",
                "Reset"
            };

            Type type = bloodLoss.GetType();

            for (int i = 0; i < stopMethodNames.Length; i++)
            {
                MethodInfo? method = type.GetMethod(stopMethodNames[i], BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (method == null) continue;

                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length > 0) continue;

                try
                {
                    method.Invoke(bloodLoss, null);
                    Core.Log($"blood loss conversion -> stopped vanilla BloodLoss via {stopMethodNames[i]}() | cause:'{cause}'");
                    return true;
                }
                catch (Exception e)
                {
                    Core.Log($"blood loss conversion -> failed to call {stopMethodNames[i]}(): {e.Message}");
                }
            }

            string[] activeFieldNames =
            {
                "m_Active",
                "m_IsActive",
                "m_BloodLossActive",
                "m_HasBloodLoss"
            };

            for (int i = 0; i < activeFieldNames.Length; i++)
            {
                FieldInfo? field = type.GetField(activeFieldNames[i], BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field == null || field.FieldType != typeof(bool)) continue;

                try
                {
                    field.SetValue(bloodLoss, false);
                    Core.Log($"blood loss conversion -> disabled vanilla BloodLoss field {activeFieldNames[i]} | cause:'{cause}'");
                    return true;
                }
                catch (Exception e)
                {
                    Core.Log($"blood loss conversion -> failed to disable {activeFieldNames[i]}: {e.Message}");
                }
            }

            Core.Log($"blood loss conversion warning -> could not stop vanilla BloodLoss after conversion | cause:'{cause}'");
            return false;
        }

        internal static void TryHandleSevereLacerationHealing(SevereLacerations severe)
        {
            PlayerManager player = GameManager.GetPlayerManagerComponent();
            if (player == null || player.PlayerIsDead())
            {
                _severeWasActive = false;
                return;
            }

            if (severe == null) return;

            bool isActive = severe.HasAffliction();

            if (isActive)
            {
                _severeWasActive = true;
                return;
            }

            if (!_severeWasActive) return;

            _severeWasActive = false;

            AfflictionManager mgr = AfflictionManager.GetAfflictionManagerInstance();
            if (mgr == null) return;

            AddScarredFleshStack("SevereLacerations healed");
        }

        private static IEnumerator ApplySevereNextFrame(string cause)
        {
            yield return null;

            try
            {
                PlayerManager player = GameManager.GetPlayerManagerComponent();
                if (player == null || player.PlayerIsDead()) yield break;

                SevereLacerations severe = GameManager.GetSevereLacerations();
                if (severe == null || severe.HasAffliction()) yield break;

                Condition condition = GameManager.GetConditionComponent();

                float hpBefore = condition != null ? condition.m_CurrentHP : -1f;
                float normalizedBefore = condition != null ? condition.GetNormalizedCondition() : -1f;

                _applyingSevere = true;

                severe.ApplySevereLacerations(cause);

                float hpAfter = condition != null ? condition.m_CurrentHP : -1f;
                float normalizedAfter = condition != null ? condition.GetNormalizedCondition() : -1f;

                Core.Log($"blood loss converted -> SevereLacerations applied | cause:'{cause}' | active:{severe.HasAffliction()} | condition:{hpBefore:0.##}->{hpAfter:0.##} HP | normalized:{normalizedBefore * 100f:0.#}%->{normalizedAfter * 100f:0.#}%");
            }
            catch (Exception e)
            {
                Core.Log($"blood loss conversion failed while applying SevereLacerations: {e}");
            }
            finally
            {
                _applyingSevere = false;
                _pendingSevere = false;
                _loggedPendingSevereSuppression = false;
            }
        }

        internal static int GetScarredFleshCount()
        {
            Core.State ??= new MMState();
            return Settings.options.EnableScarredFlesh ? Mathf.Max(0, Core.State.ScarredFleshHistoryCount) : 0;
        }

        internal static void AddScarredFleshStack(string source)
        {
            Core.State ??= new MMState();

            Core.State.ScarredFleshHistoryCount = Mathf.Max(0, Core.State.ScarredFleshHistoryCount) + 1;
            Core.Instance?.MarkDirty();

            if (!Settings.options.EnableScarredFlesh)
            {
                Core.Log($"{source} -> ScarredFlesh history increased to {Core.State.ScarredFleshHistoryCount}, but the system is disabled.");
                ForceRefreshEffects();
                return;
            }

            Core.Log($"{source} -> ScarredFlesh stack is now x{Core.State.ScarredFleshHistoryCount}.");

            EnsureScarredFleshDisplay();
            ForceRefreshEffects();
        }

        internal static void SetScarredFleshStack(int value)
        {
            Core.State ??= new MMState();

            Core.State.ScarredFleshHistoryCount = Mathf.Max(0, value);
            Core.Instance?.MarkDirty();

            EnsureScarredFleshDisplay();
            ForceRefreshEffects();
        }

        private static void EnsureScarredFleshDisplay()
        {
            Core.State ??= new MMState();

            if (!Settings.options.EnableScarredFlesh || Core.State.ScarredFleshHistoryCount <= 0)
            {
                CureAllAfflictionsOfType<ScarredFleshAffliction>();
                return;
            }

            ScarredFleshAffliction? activeScarredFlesh = null;

            AfflictionManager mgr = AfflictionManager.GetAfflictionManagerInstance();
            var list = mgr?.m_Afflictions;

            if (list != null)
            {
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    if (list[i] is ScarredFleshAffliction scarredFlesh)
                    {
                        if (activeScarredFlesh == null)
                        {
                            activeScarredFlesh = scarredFlesh;
                        }
                        else
                        {
                            scarredFlesh.Cure();
                        }
                    }
                }
            }

            if (activeScarredFlesh != null)
            {
                activeScarredFlesh.RefreshStackDisplay();
                return;
            }

            new ScarredFleshAffliction(AfflictionBodyArea.Chest).Start();
        }

        internal static void SyncSettingsControlledAfflictions()
        {
            if (!IsReady()) return;

            SyncScarredFleshFromHistory();
            SyncSepsisSystem();
            SyncCarbonMonoxideSystem();
            SyncBlackLungSystem();
            SyncCorpseSicknessSystem();
            SevereSprainLogic.SyncFromState();
            ForceRefreshEffects();
        }

        private static void SyncScarredFleshFromHistory()
        {
            EnsureScarredFleshDisplay();
        }

        private static void SyncSepsisSystem()
        {
            if (Settings.options.EnableSepsis) return;

            CureAllAfflictionsOfType<Afflictions.SepsisRisk.SepsisRiskAffliction>();
            CureAllAfflictionsOfType<Afflictions.Sepsis.SepsisAffliction>();
        }

        private static void SyncCarbonMonoxideSystem()
        {
            if (Settings.options.EnableCarbonMonoxide) return;

            _coSceneStates.Clear();
            CureAllAfflictionsOfType<COExposureAffliction>();
            CureAllAfflictionsOfType<COPoisoningAffliction>();
        }

        private static void SyncBlackLungSystem()
        {
            if (Settings.options.EnableBlackLung) return;

            Core.State ??= new MMState();
            if (!Mathf.Approximately(Core.State.BlackLungExposure, 0f))
            {
                Core.State.BlackLungExposure = 0f;
                Core.Instance?.MarkDirty();
            }

            CureAllAfflictionsOfType<BlackLungRiskAffliction>();
            CureAllAfflictionsOfType<BlackLungAffliction>();
        }

        private static void SyncCorpseSicknessSystem()
        {
            ResetCorpseTracking();

            if (Settings.options.EnableCorpseSickness) return;

            Core.State ??= new MMState();

            if (!Mathf.Approximately(Core.State.CorpseExposure, 0f))
            {
                Core.State.CorpseExposure = 0f;
                Core.Instance?.MarkDirty();
            }

            CureAllAfflictionsOfType<CorpseSicknessRiskAffliction>();
            CureAllAfflictionsOfType<CorpseSicknessAffliction>();
        }

        internal static void CureAllAfflictionsOfType<TAffliction>() where TAffliction : class
        {
            AfflictionManager mgr = AfflictionManager.GetAfflictionManagerInstance();
            if (mgr?.m_Afflictions == null) return;

            for (int i = mgr.m_Afflictions.Count - 1; i >= 0; i--)
            {
                if (mgr.m_Afflictions[i] is TAffliction afflictionObject && afflictionObject is CustomAffliction affliction)
                {
                    affliction.Cure();
                }
            }
        }

        // ======================================================================================
        //                               Broken Leg & Arm Logic
        // ======================================================================================

        private const int BROKEN_LEG_MIN_HOURS = 1008;
        private const int BROKEN_LEG_MAX_HOURS = 2016;
        private const int BROKEN_ARM_MIN_HOURS = 1008;
        private const int BROKEN_ARM_MAX_HOURS = 1344;

        private enum BrokenLegCause
        {
            Generic = 0,
            FallDamage = 1,
            BearAttack = 2,
            MooseAttack = 3
        }

        private static string GetBrokenLegCauseKey(BrokenLegCause cause)
        {
            return cause switch
            {
                BrokenLegCause.FallDamage => "GAMEPLAY_BrokenLegCause_FallDamage",
                BrokenLegCause.BearAttack => "GAMEPLAY_BrokenLegCause_BearAttack",
                BrokenLegCause.MooseAttack => "GAMEPLAY_BrokenLegCause_MooseAttack",
                _ => "GAMEPLAY_BrokenLegCause"
            };
        }

        internal static void TryApplyBearStruggleFracture()
        {
            if (!IsReady()) return;

            PlayerManager player = GameManager.GetPlayerManagerComponent();
            if (player == null || player.PlayerIsDead()) return;

            float chance = Settings.options.BearBrokenLimbChance;

            Fatigue fatigue = GameManager.GetFatigueComponent();
            if (fatigue != null && fatigue.m_CurrentFatigue <= 40f) chance += 10f;

            if (!RollChance(chance))
            {
                Core.Log($"bear struggle fracture avoided ({chance:0.#}%)");
                return;
            }

            Core.Log($"bear struggle fracture triggered ({chance:0.#}%)");
            ApplyRandomBrokenLimb("Bear Attack", BrokenLegCause.BearAttack);
        }

        internal static void TryApplyMooseStruggleFracture()
        {
            if (!IsReady()) return;

            PlayerManager player = GameManager.GetPlayerManagerComponent();
            if (player == null || player.PlayerIsDead()) return;

            float chance = Settings.options.MooseBrokenLimbChance;

            Fatigue fatigue = GameManager.GetFatigueComponent();
            if (fatigue != null && fatigue.m_CurrentFatigue <= 40f) chance += 10f;

            if (!RollChance(chance))
            {
                Core.Log($"moose struggle fracture avoided ({chance:0.#}%)");
                return;
            }

            Core.Log($"moose struggle fracture triggered ({chance:0.#}%)");
            ApplyRandomBrokenLimb("Moose Attack", BrokenLegCause.MooseAttack);
        }

        private static void ApplyRandomBrokenLimb(string causeText, BrokenLegCause brokenLegCause)
        {
            if (Settings.options.AllowDoubleBrokenLimb)
            {
                AfflictionBodyArea armArea = Random.Range(0, 2) == 0 ? AfflictionBodyArea.ArmLeft : AfflictionBodyArea.ArmRight;

                AfflictionBodyArea legArea = Random.Range(0, 2) == 0 ? AfflictionBodyArea.LegLeft : AfflictionBodyArea.LegRight;

                float armDuration = Random.Range(BROKEN_ARM_MIN_HOURS, BROKEN_ARM_MAX_HOURS + 1);
                float legDuration = Random.Range(BROKEN_LEG_MIN_HOURS, BROKEN_LEG_MAX_HOURS + 1);

                if (Settings.options.BrokenLimbDurationMode == 1)
                {
                    armDuration /= 10f;
                    legDuration /= 10f;
                }

                Core.Log($"{causeText} -> applying BrokenArm ({armArea}) for {armDuration:0.#}h and BrokenLeg ({legArea}) for {legDuration:0.#}h");

                new BrokenArmAffliction(armArea, armDuration).Start();
                new BrokenLegAffliction(legArea, legDuration, GetBrokenLegCauseKey(brokenLegCause)).Start();
                return;
            }

            bool applyArm = Random.Range(0, 2) == 0;

            if (applyArm)
            {
                AfflictionBodyArea armArea = Random.Range(0, 2) == 0 ? AfflictionBodyArea.ArmLeft : AfflictionBodyArea.ArmRight;

                float armDuration = Random.Range(BROKEN_ARM_MIN_HOURS, BROKEN_ARM_MAX_HOURS + 1);

                if (Settings.options.BrokenLimbDurationMode == 1)
                {
                    armDuration /= 10f;
                }

                Core.Log($"{causeText} -> applying BrokenArm ({armArea}) for {armDuration:0.#}h");
                new BrokenArmAffliction(armArea, armDuration).Start();
            }
            else
            {
                AfflictionBodyArea legArea = Random.Range(0, 2) == 0 ? AfflictionBodyArea.LegLeft : AfflictionBodyArea.LegRight;

                float legDuration = Random.Range(BROKEN_LEG_MIN_HOURS, BROKEN_LEG_MAX_HOURS + 1);

                if (Settings.options.BrokenLimbDurationMode == 1)
                {
                    legDuration /= 10f;
                }

                Core.Log($"{causeText} -> applying BrokenLeg ({legArea}) for {legDuration:0.#}h");
                new BrokenLegAffliction(legArea, legDuration, GetBrokenLegCauseKey(brokenLegCause)).Start();
            }
        }

        internal static float GetBrokenLegCarryCapacityMultiplier()
        {
            RefreshEffectsIfNeeded();

            return _cache.BrokenLegCount switch
            {
                >= 2 => 0.50f,
                1 => 0.75f,
                _ => 1f
            };
        }

        internal static bool HasBrokenArm()
        {
            RefreshEffectsIfNeeded();
            return _cache.BrokenArmCount > 0;
        }

        internal static bool HasBrokenLeg()
        {
            RefreshEffectsIfNeeded();
            return _cache.BrokenLegCount > 0;
        }

        internal static bool HasBothBrokenLegs()
        {
            RefreshEffectsIfNeeded();
            return _cache.BrokenLegLeft && _cache.BrokenLegRight;
        }

        internal static bool HasBothBrokenArms()
        {
            RefreshEffectsIfNeeded();
            return _cache.BrokenArmLeft && _cache.BrokenArmRight;
        }

        // ===========================================================================
        //                          BrokenLeg fall logic
        // ===========================================================================

        private static bool _processingFallInjury;
        private static float _conditionBeforeFall = -1f;

        private const float FALL_BROKEN_LEG_CHANCE_THRESHOLD = 0.15f;
        private const float FALL_BROKEN_LEG_GUARANTEED_THRESHOLD = 0.25f;
        private const float FALL_BROKEN_LEG_CHANCE = 35f;

        internal static void BeginFallDamageEvaluation()
        {
            if (!IsReady())
            {
                _conditionBeforeFall = -1f;
                return;
            }

            Condition cond = GameManager.GetConditionComponent();
            if (cond == null)
            {
                _conditionBeforeFall = -1f;
                return;
            }

            _conditionBeforeFall = cond.GetNormalizedCondition();
        }

        internal static void EndFallDamageEvaluation()
        {
            if (_processingFallInjury) return;

            if (!IsReady()) return;

            if (_conditionBeforeFall < 0f) return;

            MelonCoroutines.Start(EvaluateFallDamageNextFrame());
        }

        private static IEnumerator EvaluateFallDamageNextFrame()
        {
            yield return null;

            if (_processingFallInjury) yield break;

            if (!IsReady()) yield break;

            PlayerManager player = GameManager.GetPlayerManagerComponent();
            if (player == null || player.PlayerIsDead()) yield break;

            Condition cond = GameManager.GetConditionComponent();
            if (cond == null) yield break;

            if (_conditionBeforeFall < 0f) yield break;

            float conditionAfter = cond.GetNormalizedCondition();
            float lost = Mathf.Max(0f, _conditionBeforeFall - conditionAfter);

            if (lost >= FALL_BROKEN_LEG_CHANCE_THRESHOLD) Core.Log($"fall damage delayed end -> conditionBefore={_conditionBeforeFall:0.###}, conditionAfter={conditionAfter:0.###}, lost={lost:0.###}");

            _conditionBeforeFall = -1f;

            TryApplyBrokenLegFromFallDamage(lost);
        }

        private static void TryApplyBrokenLegFromFallDamage(float normalizedConditionLost)
        {
            if (_processingFallInjury) return;

            if (!IsReady()) return;

            PlayerManager player = GameManager.GetPlayerManagerComponent();
            if (player == null || player.PlayerIsDead()) return;

            RefreshEffectsIfNeeded(force: true);

            if (_cache.BrokenLegLeft && _cache.BrokenLegRight)
            {
                Core.Log("fall fracture skipped -> both legs already broken");
                return;
            }

            bool guaranteed = normalizedConditionLost >= FALL_BROKEN_LEG_GUARANTEED_THRESHOLD;
            bool eligibleChance = normalizedConditionLost >= FALL_BROKEN_LEG_CHANCE_THRESHOLD;

            if (!guaranteed && !eligibleChance)
            {
                if (normalizedConditionLost > 0.001f) Core.Log($"fall fracture skipped -> damage too low ({normalizedConditionLost:0.###})");
                return;
            }

            if (!guaranteed && !RollChance(FALL_BROKEN_LEG_CHANCE))
            {
                Core.Log($"fall fracture avoided -> rolled against {FALL_BROKEN_LEG_CHANCE:0.#}%");
                return;
            }

            _processingFallInjury = true;

            try
            {
                ApplyBrokenLegFromFall("Fall Damage", normalizedConditionLost);
            }
            finally
            {
                _processingFallInjury = false;
            }
        }

        private static void ApplyBrokenLegFromFall(string cause, float normalizedConditionLost)
        {
            RefreshEffectsIfNeeded(force: true);

            AfflictionBodyArea legArea;

            if (_cache.BrokenLegLeft && !_cache.BrokenLegRight) legArea = AfflictionBodyArea.LegRight;
            else if (_cache.BrokenLegRight && !_cache.BrokenLegLeft) legArea = AfflictionBodyArea.LegLeft;
            else legArea = Random.Range(0, 2) == 0 ? AfflictionBodyArea.LegLeft : AfflictionBodyArea.LegRight;

            float duration = Random.Range(BROKEN_LEG_MIN_HOURS, BROKEN_LEG_MAX_HOURS + 1);

            if (Settings.options.BrokenLimbDurationMode == 1) duration /= 10f;

            Core.Log($"{cause} -> applying BrokenLeg ({legArea}) for {duration:0.#}h after losing {normalizedConditionLost * 100f:0.#}% condition");

            new BrokenLegAffliction(legArea, duration, GetBrokenLegCauseKey(BrokenLegCause.FallDamage)).Start();
            ForceRefreshEffects();
        }

        // ==========================================================================
        //    Afflictions Effects (see AfflictionEffects.cs for Harmony patches)
        // ==========================================================================

        internal static float GetMaxHpPenalty()
        {
            RefreshEffectsIfNeeded();

            float penalty = 0f;

            if (_cache.Requiem) penalty += 50f;
            else if (_cache.Knell) penalty += 30f;
            else if (_cache.Dirge) penalty += 20f;
            else if (_cache.Omen) penalty += 10f;

            penalty += 2f * GetScarredFleshCount();

            return penalty;
        }

        internal static bool HasStageEffect(RequiemStage minimumStage)
        {
            return GetCurrentStage() >= minimumStage;
        }

        internal static float GetSleepFatigueRecoveryMultiplier()
        {
            RefreshEffectsIfNeeded();

            float multiplier = HasStageEffect(RequiemStage.Omen) ? 0.5f : 1f;

            if (_cache.HomeComfort) multiplier *= RegionalAfflictionLogic.HOME_COMFORT_SLEEP_RECOVERY_MULTIPLIER;

            if (_cache.HomeSickness) multiplier *= RegionalAfflictionLogic.HOME_SICKNESS_SLEEP_RECOVERY_MULTIPLIER;

            if (_cache.RegionalDistress) multiplier *= RegionalAfflictionLogic.REGIONAL_DISTRESS_SLEEP_RECOVERY_MULTIPLIER;

            return multiplier;
        }

        internal static int GetAdjustedMaxSleepHours(int vanillaMaxHours)
        {
            if (!HasStageEffect(RequiemStage.Omen)) return vanillaMaxHours;

            return Mathf.Max(1, vanillaMaxHours - 4);
        }

        internal static float GetBodyTemperatureModifierCelsius()
        {
            return HasStageEffect(RequiemStage.Dirge) ? -5f : 0f;
        }

        internal static bool ShouldDisableNaturalConditionRecovery()
        {
            return HasStageEffect(RequiemStage.Requiem);
        }

        internal static float ApplyIncomingDamageMultiplier(float healthDelta)
        {
            if (_applyingSevere && healthDelta < 0f)
            {
                Core.Log($"blocked condition damage during SevereLacerations conversion -> hpDelta:{healthDelta:0.###}");
                return 0f;
            }

            if (!Settings.options.EnableRequiemStages) return healthDelta;
            if (!HasStageEffect(RequiemStage.Requiem) || healthDelta >= 0f) return healthDelta;

            return healthDelta * 2f;
        }

        internal static float ApplyChunkedConditionDrain(Condition condition, float rawHpLoss, DamageSource damageSource = DamageSource.Unspecified)
        {
            if (condition == null || rawHpLoss <= 0f) return 0f;

            if (condition.m_CurrentHP <= 0f) return 0f;

            float damageMultiplier = Mathf.Abs(ApplyIncomingDamageMultiplier(-1f));
            if (damageMultiplier <= 0f) damageMultiplier = 1f;

            const float SAFE_EFFECTIVE_CHUNK = 0.02f;

            float safeRawChunk = SAFE_EFFECTIVE_CHUNK / damageMultiplier;
            if (safeRawChunk <= 0f) safeRawChunk = 0.01f;

            float remainingRawLoss = rawHpLoss;
            float appliedEffectiveLoss = 0f;

            int chunks = 0;
            const int MAX_CHUNKS_PER_TICK = 128;

            while (remainingRawLoss > 0.0001f && condition.m_CurrentHP > 0f)
            {
                float rawChunk = Mathf.Min(remainingRawLoss, safeRawChunk);

                float hpBefore = condition.m_CurrentHP;

                condition.AddHealth(-rawChunk, damageSource);

                float hpAfter = condition.m_CurrentHP;
                float actualLoss = Mathf.Max(0f, hpBefore - hpAfter);

                appliedEffectiveLoss += actualLoss;
                remainingRawLoss -= rawChunk;

                chunks++;

                if (chunks >= MAX_CHUNKS_PER_TICK)
                {
                    Core.Log($"[ChunkedConditionDrain] Aborted after {MAX_CHUNKS_PER_TICK} chunks | " + $"rawHpLoss:{rawHpLoss:0.#####} | " + $"remainingRaw:{remainingRawLoss:0.#####} | " + $"safeRawChunk:{safeRawChunk:0.#####} | " + $"damageMultiplier:{damageMultiplier:0.###}");

                    break;
                }
            }

            return appliedEffectiveLoss;
        }

        internal static float ApplySprintSpeedPenaltyToFinalMultiplier(float multiplier)
        {
            if (!HasStageEffect(RequiemStage.Knell)) return multiplier;

            if (HasWeakJoints()) return multiplier;

            return multiplier * 0.75f;
        }

        internal static bool ShouldBlockSprint()
        {
            RefreshEffectsIfNeeded();

            return _cache.BrokenLegCount > 0 || _cache.SevereAnkleSprainCount > 0;
        }

        internal static bool ShouldBlockClimbing()
        {
            RefreshEffectsIfNeeded();

            return _cache.BrokenLegCount > 0 || _cache.BrokenArmCount > 0 || _cache.SevereAnkleSprainCount > 0 || _cache.SevereWristSprainCount > 0;
        }

        internal static float GetMovementSpeedMultiplier()
        {
            RefreshEffectsIfNeeded();

            float multiplier = 1f;

            if (_cache.BrokenLegLeft && _cache.BrokenLegRight) multiplier *= 0.45f;
            else if (_cache.BrokenLegCount > 0) multiplier *= 0.65f;
            else if (_cache.SevereAnkleSprainCount > 0) multiplier *= 0.75f;

            if (HasStageEffect(RequiemStage.Knell)) multiplier *= 0.9f;

            return multiplier;
        }

        internal static float GetMovementFatigueMultiplier()
        {
            RefreshEffectsIfNeeded();

            float multiplier = 1f;

            if (_cache.BrokenLegLeft && _cache.BrokenLegRight) multiplier *= 2.0f;
            else if (_cache.BrokenLegCount > 0) multiplier *= 1.5f;
            else if (_cache.SevereAnkleSprainCount > 0) multiplier *= 1.35f;

            if (_cache.HomeComfort) multiplier *= RegionalAfflictionLogic.HOME_COMFORT_MOVEMENT_FATIGUE_MULTIPLIER;

            if (_cache.HomeSickness) multiplier *= RegionalAfflictionLogic.HOME_SICKNESS_MOVEMENT_FATIGUE_MULTIPLIER;

            if (_cache.RegionalDistress) multiplier *= RegionalAfflictionLogic.REGIONAL_DISTRESS_MOVEMENT_FATIGUE_MULTIPLIER;

            return multiplier;
        }

        internal static float GetCraftingTimeMultiplier()
        {
            RefreshEffectsIfNeeded();

            float multiplier = 1f;

            if (_cache.BrokenArmLeft && _cache.BrokenArmRight) multiplier = Mathf.Max(multiplier, 2.0f);
            else if (_cache.BrokenArmCount > 0) multiplier = Mathf.Max(multiplier, 1.5f);

            if (_cache.SevereWristSprainCount > 0) multiplier = Mathf.Max(multiplier, 1.35f);

            return multiplier;
        }

        internal static float GetAimSwayIncreaseMultiplier()
        {
            RefreshEffectsIfNeeded();

            float multiplier = 1f;

            if (_cache.BrokenArmLeft && _cache.BrokenArmRight) multiplier = Mathf.Max(multiplier, 3f);
            else if (_cache.BrokenArmCount > 0) multiplier = Mathf.Max(multiplier, 2f);

            if (_cache.SevereWristSprainCount > 0) multiplier = Mathf.Max(multiplier, 2.5f);

            return multiplier;
        }

        internal static float GetAimSwayDecreaseMultiplier()
        {
            RefreshEffectsIfNeeded();

            float multiplier = 1f;

            if (_cache.BrokenArmLeft && _cache.BrokenArmRight) multiplier = Mathf.Min(multiplier, 0.45f);
            else if (_cache.BrokenArmCount > 0) multiplier = Mathf.Min(multiplier, 0.65f);

            if (_cache.SevereWristSprainCount > 0) multiplier = Mathf.Min(multiplier, 0.5f);

            return multiplier;
        }

        // ====================================================================
        //                  Affliction cache & refresh
        // ====================================================================

        private struct Cache
        {
            public bool Omen;
            public bool Dirge;
            public bool Knell;
            public bool Requiem;

            public int BrokenArmCount;
            public int BrokenLegCount;

            public bool BrokenArmLeft;
            public bool BrokenArmRight;
            public bool BrokenLegLeft;
            public bool BrokenLegRight;

            public int SevereWristSprainCount;
            public int SevereAnkleSprainCount;

            public bool HomeComfort;
            public bool HomeSickness;
            public bool RegionalDistress;

            public void Reset()
            {
                this = default;
            }
        }

        private static Cache _cache;

        private static void RefreshEffectsIfNeeded(bool force = false)
        {
            float now = Time.unscaledTime;
            if (!force && (now - _lastRefreshUnscaledTime) < REFRESH_INTERVAL_SECONDS) return;

            _lastRefreshUnscaledTime = now;
            _cache.Reset();

            var mgr = AfflictionManager.GetAfflictionManagerInstance();
            var list = mgr?.m_Afflictions;
            if (list == null) return;

            for (int i = 0; i < list.Count; i++)
            {
                object? a;
                try { a = list[i]; }
                catch { break; }

                if (a == null) continue;

                switch (a)
                {
                    case OmenAffliction:
                        _cache.Omen = true;
                        break;

                    case DirgeAffliction:
                        _cache.Dirge = true;
                        break;

                    case KnellAffliction:
                        _cache.Knell = true;
                        break;

                    case RequiemAffliction:
                        _cache.Requiem = true;
                        break;

                    case SevereWristSprainAffliction:
                        _cache.SevereWristSprainCount++;
                        break;

                    case SevereAnkleSprainAffliction:
                        _cache.SevereAnkleSprainCount++;
                        break;

                    case HomeComfortBuff:
                        _cache.HomeComfort = true;
                        break;

                    case HomeSicknessAffliction:
                        _cache.HomeSickness = true;
                        break;

                    case RegionalDistressAffliction:
                        _cache.RegionalDistress = true;
                        break;

                    case BrokenArmAffliction brokenArm:
                        _cache.BrokenArmCount++;
                        if (brokenArm.m_Location == AfflictionBodyArea.ArmLeft) _cache.BrokenArmLeft = true;
                        if (brokenArm.m_Location == AfflictionBodyArea.ArmRight) _cache.BrokenArmRight = true;
                        break;

                    case BrokenLegAffliction brokenLeg:
                        _cache.BrokenLegCount++;
                        if (brokenLeg.m_Location == AfflictionBodyArea.LegLeft) _cache.BrokenLegLeft = true;
                        if (brokenLeg.m_Location == AfflictionBodyArea.LegRight) _cache.BrokenLegRight = true;
                        break;
                }
            }
        }

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

        private static readonly Dictionary<string, CORiskSceneState> _coSceneStates = new();

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

        // =======================================================================================
        //                                Corpse Sickness logic
        // =======================================================================================

        private const float CORPSE_EXPOSURE_MAX = 100f;
        private const float CORPSE_RISK_START_THRESHOLD = 100f;

        // hidden exposure -> risk
        private const float CORPSE_EXPOSURE_DECAY_SLOWDOWN_MULTIPLIER = 2f;

        // risk -> sickness
        private const float CORPSE_RISK_TIME_TO_SICKNESS_HOURS = 12f;
        private const float CORPSE_RISK_DECAY_SLOWDOWN_MULTIPLIER = 2f;

        private const float CORPSE_RISK_GAIN_PER_HOUR = 100f / CORPSE_RISK_TIME_TO_SICKNESS_HOURS;
        private const float CORPSE_RISK_DECAY_PER_HOUR = CORPSE_RISK_GAIN_PER_HOUR / CORPSE_RISK_DECAY_SLOWDOWN_MULTIPLIER;

        private static bool s_WasPlayerNearCorpseSource = false;
        private static string s_LastCorpseSourceLabel = string.Empty;

        // Animal reseed queue
        private const float CORPSE_ANIMAL_RESEED_DELAY_REALTIME_SECONDS = 1.5f;
        private static bool s_AnimalCarcassReseedQueued = false;
        private static float s_AnimalCarcassReseedDueTime = -999f;
        private static string s_AnimalCarcassReseedReason = string.Empty;

        // Human corpses = static per scene
        private static readonly List<Container> s_HumanCorpseContainers = new();
        private static bool s_HumanCorpseSceneCacheBuilt = false;
        private static string s_HumanCorpseSceneCacheName = string.Empty;

        // Animal carcasses = scene-seeded cache + deferred reseed on death events
        private static readonly List<BodyHarvest> s_AnimalCarcasses = new();
        private static readonly HashSet<int> s_AnimalCarcassIds = new();
        private static bool s_AnimalCarcassSceneSeeded = false;
        private static string s_AnimalCarcassSceneSeedName = string.Empty;

        internal static float GetCorpseExposureMax() => CORPSE_EXPOSURE_MAX;
        internal static float GetCorpseRiskGainPerHour() => CORPSE_RISK_GAIN_PER_HOUR;
        internal static float GetCorpseRiskDecayPerHour() => CORPSE_RISK_DECAY_PER_HOUR;

        private static void LogCorpseDebug(string message)
        {
            Core.Log($"{message}");
        }

        private static void UpdateCorpseSourceLoggingState(bool nearSource, string sourceLabel, float closestDistance, float gainPerHour)
        {
            if (nearSource)
            {
                if (!s_WasPlayerNearCorpseSource || !string.Equals(s_LastCorpseSourceLabel, sourceLabel, StringComparison.Ordinal))
                {
                    LogCorpseDebug($"Entered exposure range of {sourceLabel} ({closestDistance:0.##}m, gain {gainPerHour:0.##}/h).");
                }

                s_WasPlayerNearCorpseSource = true;
                s_LastCorpseSourceLabel = sourceLabel ?? string.Empty;
            }
            else if (s_WasPlayerNearCorpseSource)
            {
                LogCorpseDebug("Left corpse exposure range.");
                s_WasPlayerNearCorpseSource = false;
                s_LastCorpseSourceLabel = string.Empty;
            }
        }

        internal static void ResetCorpseTracking()
        {
            s_HumanCorpseContainers.Clear();
            s_HumanCorpseSceneCacheBuilt = false;
            s_HumanCorpseSceneCacheName = string.Empty;

            s_AnimalCarcasses.Clear();
            s_AnimalCarcassIds.Clear();
            s_AnimalCarcassSceneSeeded = false;
            s_AnimalCarcassSceneSeedName = string.Empty;

            s_AnimalCarcassReseedQueued = false;
            s_AnimalCarcassReseedDueTime = -999f;
            s_AnimalCarcassReseedReason = string.Empty;

            s_WasPlayerNearCorpseSource = false;
            s_LastCorpseSourceLabel = string.Empty;
        }

        internal static void RebuildHumanCorpseSceneCache()
        {
            s_HumanCorpseContainers.Clear();

            string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name ?? string.Empty;

            try
            {
                Container[] containers = UnityEngine.Resources.FindObjectsOfTypeAll<Container>();
                if (containers != null)
                {
                    for (int i = 0; i < containers.Length; i++)
                    {
                        Container container = containers[i];
                        if (container == null) continue;

                        bool isCorpse;
                        try
                        {
                            isCorpse = container.m_IsCorpse;
                        }
                        catch
                        {
                            continue;
                        }

                        if (!isCorpse) continue;

                        GameObject go;
                        try
                        {
                            go = container.gameObject;
                        }
                        catch
                        {
                            continue;
                        }

                        if (go == null || !go.activeInHierarchy) continue;

                        UnityEngine.SceneManagement.Scene scene = go.scene;
                        if (!scene.IsValid() || !scene.isLoaded) continue;

                        s_HumanCorpseContainers.Add(container);
                    }
                }
            }
            catch (Exception e)
            {
                LogCorpseDebug($"RebuildHumanCorpseSceneCache failed: {e.Message}");
            }

            s_HumanCorpseSceneCacheBuilt = true;
            s_HumanCorpseSceneCacheName = currentScene;

            LogCorpseDebug($"Human corpse cache rebuilt for scene '{currentScene}' -> {s_HumanCorpseContainers.Count} corpse container(s).");
        }

        private static void EnsureHumanCorpseSceneCache()
        {
            string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name ?? string.Empty;

            if (!s_HumanCorpseSceneCacheBuilt || s_HumanCorpseSceneCacheName != currentScene)
            {
                RebuildHumanCorpseSceneCache();
            }
        }

        private static bool IsBodyHarvestManaged(BodyHarvest bodyHarvest)
        {
            if (bodyHarvest == null) return false;

            if (BodyHarvestManager.m_BodyHarvestList == null) return false;

            for (int i = 0; i < BodyHarvestManager.m_BodyHarvestList.Count; i++)
            {
                if (BodyHarvestManager.m_BodyHarvestList[i] == bodyHarvest) return true;
            }

            return false;
        }

        private static bool IsDeadWildlifeBodyHarvest(BodyHarvest bodyHarvest)
        {
            if (bodyHarvest == null) return false;

            GameObject go;
            try
            {
                go = bodyHarvest.gameObject;
            }
            catch
            {
                return false;
            }

            if (go == null) return false;

            BaseAi ai = go.GetComponent<BaseAi>() ?? go.GetComponentInParent<BaseAi>();
            if (ai == null) return true;

            try
            {
                return ai.m_CurrentMode == AiMode.Dead;
            }
            catch
            {
                return true;
            }
        }

        private static bool IsValidAnimalCarcassForCorpseSickness(BodyHarvest bodyHarvest)
        {
            if (bodyHarvest == null) return false;

            GameObject go;
            try
            {
                go = bodyHarvest.gameObject;
            }
            catch
            {
                return false;
            }

            if (go == null || !go.activeInHierarchy) return false;

            string objectName = go.name ?? string.Empty;

            if (objectName.StartsWith("WILDLIFE_Rabbit", StringComparison.OrdinalIgnoreCase) ||
                objectName.StartsWith("WILDLIFE_Ptarmigan", StringComparison.OrdinalIgnoreCase) ||
                objectName.StartsWith("GEAR_RabbitCarcass", StringComparison.OrdinalIgnoreCase) ||
                objectName.StartsWith("GEAR_PtarmiganCarcass", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            UnityEngine.SceneManagement.Scene scene = go.scene;
            if (!scene.IsValid() || !scene.isLoaded) return false;

            Transform playerTransform = GameManager.GetPlayerTransform();
            if (playerTransform != null && go.transform.IsChildOf(playerTransform)) return false;

            if (!IsBodyHarvestManaged(bodyHarvest)) return false;

            if (objectName.StartsWith("WILDLIFE_", StringComparison.OrdinalIgnoreCase)) return IsDeadWildlifeBodyHarvest(bodyHarvest);

            return true;
        }

        internal static void SeedAnimalCarcassCacheFromScene()
        {
            s_AnimalCarcasses.Clear();
            s_AnimalCarcassIds.Clear();

            int added = 0;
            string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name ?? string.Empty;

            try
            {
                BodyHarvest[] bodyHarvests = UnityEngine.Resources.FindObjectsOfTypeAll<BodyHarvest>();
                if (bodyHarvests != null)
                {
                    for (int i = 0; i < bodyHarvests.Length; i++)
                    {
                        BodyHarvest bodyHarvest = bodyHarvests[i];
                        if (!IsValidAnimalCarcassForCorpseSickness(bodyHarvest)) continue;

                        GameObject go;
                        try
                        {
                            go = bodyHarvest.gameObject;
                        }
                        catch
                        {
                            continue;
                        }

                        int id = go.GetInstanceID();
                        if (!s_AnimalCarcassIds.Add(id)) continue;

                        s_AnimalCarcasses.Add(bodyHarvest);
                        added++;
                    }
                }
            }
            catch (Exception e)
            {
                LogCorpseDebug($"SeedAnimalCarcassCacheFromScene failed: {e.Message}");
            }

            s_AnimalCarcassSceneSeeded = true;
            s_AnimalCarcassSceneSeedName = currentScene;

            LogCorpseDebug($"Animal carcass cache seeded from scene '{currentScene}' -> {added} carcass(es).");
        }

        private static void EnsureAnimalCarcassSceneSeeded()
        {
            string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name ?? string.Empty;

            if (string.IsNullOrEmpty(currentScene) || currentScene == "MainMenu_DLC01") return;

            if (s_AnimalCarcassSceneSeeded && s_AnimalCarcassSceneSeedName == currentScene) return;

            SeedAnimalCarcassCacheFromScene();

            LogCorpseDebug($"Animal carcass cache ensured for scene '{currentScene}' -> {s_AnimalCarcasses.Count} carcass(es).");
        }

        internal static void QueueAnimalCarcassReseed(string reason)
        {
            bool alreadyQueued = s_AnimalCarcassReseedQueued;

            s_AnimalCarcassReseedQueued = true;
            s_AnimalCarcassReseedDueTime = Time.unscaledTime + CORPSE_ANIMAL_RESEED_DELAY_REALTIME_SECONDS;
            s_AnimalCarcassReseedReason = reason ?? string.Empty;

            if (!alreadyQueued) LogCorpseDebug($"Animal carcass reseed queued in {CORPSE_ANIMAL_RESEED_DELAY_REALTIME_SECONDS:0.##}s | reason='{s_AnimalCarcassReseedReason}'");
        }

        private static void MaybeProcessQueuedAnimalCarcassReseed()
        {
            if (!s_AnimalCarcassReseedQueued) return;

            if (Time.unscaledTime < s_AnimalCarcassReseedDueTime) return;

            s_AnimalCarcassReseedQueued = false;
            s_AnimalCarcassReseedDueTime = -999f;

            LogCorpseDebug($"Processing queued animal carcass reseed | reason='{s_AnimalCarcassReseedReason}'");
            SeedAnimalCarcassCacheFromScene();

            s_AnimalCarcassReseedReason = string.Empty;
        }

        private static void RemoveAnimalCarcassAt(int index)
        {
            if (index < 0 || index >= s_AnimalCarcasses.Count) return;

            BodyHarvest bodyHarvest = s_AnimalCarcasses[index];
            int id = 0;

            try
            {
                GameObject go = bodyHarvest?.gameObject;
                if (go != null) id = go.GetInstanceID();
            }
            catch { }

            s_AnimalCarcasses.RemoveAt(index);

            if (id != 0) s_AnimalCarcassIds.Remove(id);
        }

        internal static void UpdateCorpseExposure(float gameHoursPassed)
        {
            if (!Settings.options.EnableCorpseSickness) return;

            if (gameHoursPassed <= 0f) return;

            Core.State ??= new MMState();

            if (HasAffliction<CorpseSicknessAffliction>()) return;

            bool nearSource = TryGetCurrentCorpseExposureGainPerHour(out float gainPerHour, out string sourceLabel, out float closestDistance);

            UpdateCorpseSourceLoggingState(nearSource, sourceLabel, closestDistance, gainPerHour);

            if (HasAffliction<CorpseSicknessRiskAffliction>())
            {
                if (!Mathf.Approximately(Core.State.CorpseExposure, CORPSE_RISK_START_THRESHOLD))
                {
                    Core.State.CorpseExposure = CORPSE_RISK_START_THRESHOLD;
                    Core.Instance?.MarkDirty();
                }

                return;
            }

            float oldExposure = Core.State.CorpseExposure;

            if (nearSource)
            {
                Core.State.CorpseExposure = Mathf.Clamp(Core.State.CorpseExposure + (gameHoursPassed * gainPerHour), 0f, CORPSE_EXPOSURE_MAX);
            }
            else
            {
                float decayPerHour = GetCorpseExposureDecayPerHour();

                Core.State.CorpseExposure = Mathf.Clamp(Core.State.CorpseExposure - (gameHoursPassed * decayPerHour), 0f, CORPSE_EXPOSURE_MAX);
            }

            if (!Mathf.Approximately(oldExposure, Core.State.CorpseExposure))
            {
                Core.Instance?.MarkDirty();
            }

            if (nearSource && Core.State.CorpseExposure >= CORPSE_RISK_START_THRESHOLD)
            {
                LogCorpseDebug($"Exposure reached risk threshold near {sourceLabel} ({closestDistance:0.##}m). Starting CorpseSicknessRisk.");
                new CorpseSicknessRiskAffliction(AfflictionBodyArea.Head).Start();
            }
        }

        internal static bool IsPlayerNearCorpseSource()
        {
            return TryGetCurrentCorpseExposureGainPerHour(out _, out _, out _);
        }

        internal static bool TryGetCurrentCorpseExposureGainPerHour(out float gainPerHour, out string sourceLabel, out float closestDistance)
        {
            gainPerHour = 0f;
            sourceLabel = string.Empty;
            closestDistance = float.MaxValue;

            MaybeProcessQueuedAnimalCarcassReseed();

            if (!TryGetPlayerPosition(out Vector3 playerPosition))
            {
                return false;
            }

            if (Settings.options.EnableAnimalCarcassExposure)
            {
                EnsureAnimalCarcassSceneSeeded();
            }

            bool found = false;

            if (Settings.options.EnableHumanCorpseExposure && TryFindNearestHumanCorpseDistance(playerPosition, out float humanDistance))
            {
                gainPerHour = GetHumanCorpseExposureGainPerHour();
                sourceLabel = "human corpse";
                closestDistance = humanDistance;
                found = true;
            }

            if (Settings.options.EnableAnimalCarcassExposure && TryFindNearestAnimalCarcassDistance(playerPosition, out float animalDistance))
            {
                float animalGain = GetAnimalCarcassExposureGainPerHour();

                if (!found || animalGain > gainPerHour || (Mathf.Approximately(animalGain, gainPerHour) && animalDistance < closestDistance))
                {
                    gainPerHour = animalGain;
                    sourceLabel = "animal carcass";
                    closestDistance = animalDistance;
                    found = true;
                }
            }

            return found;
        }

        private static float GetHumanCorpseExposureGainPerHour()
        {
            float hoursToRisk = Mathf.Max(1f, Settings.options.HumanCorpseHoursToRisk);
            return CORPSE_EXPOSURE_MAX / hoursToRisk;
        }

        private static float GetAnimalCarcassExposureGainPerHour()
        {
            float hoursToRisk = Mathf.Max(1f, Settings.options.AnimalCarcassHoursToRisk);
            return CORPSE_EXPOSURE_MAX / hoursToRisk;
        }

        private static float GetCorpseExposureDecayPerHour()
        {
            float slowestGain = float.MaxValue;
            bool hasEnabledSource = false;

            if (Settings.options.EnableHumanCorpseExposure)
            {
                slowestGain = Mathf.Min(slowestGain, GetHumanCorpseExposureGainPerHour());
                hasEnabledSource = true;
            }

            if (Settings.options.EnableAnimalCarcassExposure)
            {
                slowestGain = Mathf.Min(slowestGain, GetAnimalCarcassExposureGainPerHour());
                hasEnabledSource = true;
            }

            if (!hasEnabledSource) slowestGain = CORPSE_EXPOSURE_MAX / 24f;

            return slowestGain / CORPSE_EXPOSURE_DECAY_SLOWDOWN_MULTIPLIER;
        }

        private static bool TryGetPlayerPosition(out Vector3 playerPosition)
        {
            playerPosition = default;

            Transform playerTransform = GameManager.GetPlayerTransform();
            if (playerTransform != null)
            {
                playerPosition = playerTransform.position;
                return true;
            }

            PlayerManager player = GameManager.GetPlayerManagerComponent();
            if (player == null) return false;

            playerPosition = player.transform.position;
            return true;
        }

        private static bool TryFindNearestHumanCorpseDistance(Vector3 playerPosition, out float closestDistance)
        {
            closestDistance = float.MaxValue;

            EnsureHumanCorpseSceneCache();

            float radius = Settings.options.HumanCorpseExposureRadiusMeters;
            float radiusSq = radius * radius;
            float closestSq = float.MaxValue;
            bool found = false;

            for (int i = s_HumanCorpseContainers.Count - 1; i >= 0; i--)
            {
                Container container = s_HumanCorpseContainers[i];
                if (container == null)
                {
                    s_HumanCorpseContainers.RemoveAt(i);
                    continue;
                }

                GameObject sourceObject;
                try
                {
                    sourceObject = container.gameObject;
                }
                catch
                {
                    s_HumanCorpseContainers.RemoveAt(i);
                    continue;
                }

                if (sourceObject == null || !sourceObject.activeInHierarchy) continue;

                Vector3 delta = sourceObject.transform.position - playerPosition;
                float distanceSq = delta.sqrMagnitude;

                if (distanceSq > radiusSq) continue;

                if (distanceSq < closestSq)
                {
                    closestSq = distanceSq;
                    found = true;
                }
            }

            if (!found) return false;

            closestDistance = Mathf.Sqrt(closestSq);
            return true;
        }

        private static bool TryFindNearestAnimalCarcassDistance(Vector3 playerPosition, out float closestDistance)
        {
            closestDistance = float.MaxValue;

            float radius = Settings.options.AnimalCarcassExposureRadiusMeters;
            float radiusSq = radius * radius;
            float closestSq = float.MaxValue;
            bool found = false;

            for (int i = s_AnimalCarcasses.Count - 1; i >= 0; i--)
            {
                BodyHarvest bodyHarvest = s_AnimalCarcasses[i];
                if (!IsValidAnimalCarcassForCorpseSickness(bodyHarvest))
                {
                    RemoveAnimalCarcassAt(i);
                    continue;
                }

                GameObject sourceObject;
                try
                {
                    sourceObject = bodyHarvest.gameObject;
                }
                catch
                {
                    RemoveAnimalCarcassAt(i);
                    continue;
                }

                Vector3 delta = sourceObject.transform.position - playerPosition;
                float distanceSq = delta.sqrMagnitude;

                if (distanceSq > radiusSq) continue;

                if (distanceSq < closestSq)
                {
                    closestSq = distanceSq;
                    found = true;
                }
            }

            if (!found) return false;

            closestDistance = Mathf.Sqrt(closestSq);
            return true;
        }

        // ===========================================================================
        //                               Utilities
        // ===========================================================================

        private static int GetTotalHoursAliveFromGame()
        {
            TimeOfDay tod = GameManager.GetTimeOfDayComponent();
            if (tod == null) return -1;

            int day = tod.GetDayNumber();
            int hour = Mathf.FloorToInt(tod.GetHour());

            return Mathf.Max(0, ((day - 1) * 24) + hour);
        }

        internal static bool HasAffliction<T>() where T : class
        {
            var mgr = AfflictionManager.GetAfflictionManagerInstance();
            if (mgr?.m_Afflictions == null) return false;

            for (int i = 0; i < mgr.m_Afflictions.Count; i++)
            {
                if (mgr.m_Afflictions[i] is T) return true;
            }

            return false;
        }

        internal static void ProcessBlackLungSleepRecovery(float hoursSlept)
        {
            if (hoursSlept <= 0f) return;

            BlackLungAffliction? blackLung = GetAffliction<BlackLungAffliction>();
            if (blackLung == null) return;

            float reductionHours = hoursSlept * BLACK_LUNG_SLEEP_RECOVERY_MULTIPLIER;
            float now = GameManager.GetTimeOfDayComponent()?.GetHoursPlayedNotPaused() ?? 0f;

            blackLung.EndTime -= reductionHours;

            Core.Log($"BlackLung improved through sleep -> -{reductionHours:0.###}h remaining.");

            if (blackLung.EndTime <= now)
            {
                Core.Log("BlackLung duration reduced to zero by sleep.");
                blackLung.Cure();
            }
        }

        internal static T? GetAffliction<T>() where T : class
        {
            var mgr = AfflictionManager.GetAfflictionManagerInstance();
            if (mgr?.m_Afflictions == null) return null;

            for (int i = 0; i < mgr.m_Afflictions.Count; i++)
            {
                if (mgr.m_Afflictions[i] is T affliction) return affliction;
            }

            return null;
        }

        internal static bool HasWeakJoints()
        {
            Condition? condition = GameManager.GetConditionComponent();
            return condition != null && condition.HasSpecificAffliction(AfflictionType.WeakJoints);
        }

        internal static bool HasSevereAnkleSprain()
        {
            RefreshEffectsIfNeeded();
            return _cache.SevereAnkleSprainCount > 0;
        }

        internal static bool HasSevereWristSprain()
        {
            RefreshEffectsIfNeeded();
            return _cache.SevereWristSprainCount > 0;
        }

        internal static int GetSevereWristSprainCount()
        {
            RefreshEffectsIfNeeded();
            return _cache.SevereWristSprainCount;
        }

        internal static bool ShouldBlockWeaponEquip(GearItem? gearItem)
        {
            if (gearItem == null) return false;

            RefreshEffectsIfNeeded();

            int wristCount = _cache.SevereWristSprainCount;
            if (wristCount <= 0) return false;

            if (!IsWeapon(gearItem)) return false;

            if (wristCount >= 2) return true;

            return IsTwoHandedWeapon(gearItem);
        }

        private static readonly HashSet<string> s_OneHandedWeaponGearNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "GEAR_RevolverGreen",
            "GEAR_RevolverFancy",
            "GEAR_RevolverStubNosed",
            "GEAR_Revolver",
        };

        private static readonly HashSet<string> s_TwoHandedWeaponGearNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "GEAR_Rifle_Barbs",
            "GEAR_Rifle_Trader",
            "GEAR_Rifle_Curators",
            "GEAR_Rifle_Vaughns",
            "GEAR_Rifle",

            "GEAR_Bow_Woodwrights",
            "GEAR_Bow_Bushcraft",
            "GEAR_Bow",

            "GEAR_Shotgun", // yep, I look at you guys from the team shotgun
        };

        private static bool IsWeapon(GearItem gearItem)
        {
            string gearName = GetCleanGearName(gearItem);

            return s_OneHandedWeaponGearNames.Contains(gearName) || s_TwoHandedWeaponGearNames.Contains(gearName) || gearItem.m_GunItem != null || gearItem.m_BowItem != null;
        }

        private static bool IsTwoHandedWeapon(GearItem gearItem)
        {
            string gearName = GetCleanGearName(gearItem);

            if (s_OneHandedWeaponGearNames.Contains(gearName)) return false;

            if (s_TwoHandedWeaponGearNames.Contains(gearName)) return true;

            if (gearItem.m_BowItem != null) return true;

            if (gearItem.m_GunItem != null) return true;

            return false;
        }

        private static string GetCleanGearName(GearItem gearItem)
        {
            string name = gearItem.name ?? "";
            return name.Replace("(Clone)", "").Trim();
        }

        private static bool RollChance(float chance)
        {
            return Random.Range(0f, 100f) < chance;
        }
    }
}