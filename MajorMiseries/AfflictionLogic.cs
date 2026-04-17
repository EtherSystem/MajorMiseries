using System.Collections;
using UnitySceneManager = UnityEngine.SceneManagement.SceneManager;
using AfflictionComponent.Components;
using MajorMiseries.Patches;
using MajorMiseries.Persistence;
using static MajorMiseries.Afflictions.BrokenArm;
using static MajorMiseries.Afflictions.BrokenLeg;
using static MajorMiseries.Afflictions.Dirge;
using static MajorMiseries.Afflictions.Knell;
using static MajorMiseries.Afflictions.Omen;
using static MajorMiseries.Afflictions.Requiem;
using static MajorMiseries.Afflictions.ScarredFlesh;
using static MajorMiseries.Afflictions.BlackLungRisk;
using static MajorMiseries.Afflictions.BlackLung;
using static MajorMiseries.Afflictions.COExposure;
using static MajorMiseries.Afflictions.COPoisoning;
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

        private const float DEFAULT_OMEN_THRESHOLD_DAYS = 1f;
        private const float DEFAULT_DIRGE_THRESHOLD_DAYS = 2f;
        private const float DEFAULT_KNELL_THRESHOLD_DAYS = 3f;
        private const float DEFAULT_REQUIEM_THRESHOLD_DAYS = 4f;

        internal static void ResetRuntime()
        {
            _lastProcessedHour = -1;
            _lastRefreshUnscaledTime = -999f;
            _coSceneStates.Clear();

            s_LastBlackLungExposureLogTime = -999f;
            s_LastBlackLungExposureLogWasIncrease = null;
            _blackLungSleepTrackingActive = false;
            _blackLungTrackedSleepHours = 0f;

            StageGaugeLockVisuals.ResetRuntime();

            _applyingSevere = false;
            _pendingSevere = false;
            _severeWasActive = false;

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
            if (!IsReady())
                return;

            int totalHours = GetTotalHoursAliveFromGame();
            if (totalHours < 0)
                return;

            if (totalHours == _lastProcessedHour)
                return;

            _lastProcessedHour = totalHours;

            if (!Settings.options.EnableRequiemStages)
            {
                RequiemStage appliedStageDisabled = GetAppliedStage();

                if (appliedStageDisabled != RequiemStage.None)
                {
                    CureAllStageAfflictions();
                    ForceRefreshEffects();
                    Core.Log($"requiem stages disabled -> cured {appliedStageDisabled}");
                }
                else
                {
                    Core.Log("requiem stages disabled");
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

        internal static void ApplyCurrentStageFromGame()
        {
            if (!IsReady()) return;

            int totalHours = GetTotalHoursAliveFromGame();
            if (totalHours < 0) return;

            _lastProcessedHour = totalHours;

            if (!Settings.options.EnableRequiemStages)
            {
                RequiemStage appliedStageDisabled = GetAppliedStage();

                if (appliedStageDisabled != RequiemStage.None)
                {
                    CureAllStageAfflictions();
                    ForceRefreshEffects();
                    Core.Log($"requiem stages disabled -> cured {appliedStageDisabled}");
                }
                else
                {
                    ForceRefreshEffects();
                    Core.Log("requiem stages disabled");
                }

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

            GetConfiguredStageThresholds(
                out float omenThreshold,
                out float dirgeThreshold,
                out float knellThreshold,
                out float requiemThreshold);

            if (daysAlive >= requiemThreshold) return RequiemStage.Requiem;
            if (daysAlive >= knellThreshold) return RequiemStage.Knell;
            if (daysAlive >= dirgeThreshold) return RequiemStage.Dirge;
            if (daysAlive >= omenThreshold) return RequiemStage.Omen;

            return RequiemStage.None;
        }

        private static void GetConfiguredStageThresholds(
            out float omenThreshold,
            out float dirgeThreshold,
            out float knellThreshold,
            out float requiemThreshold)
        {
            if (!Settings.options.CustomizeStageThresholds)
            {
                omenThreshold = DEFAULT_OMEN_THRESHOLD_DAYS;
                dirgeThreshold = DEFAULT_DIRGE_THRESHOLD_DAYS;
                knellThreshold = DEFAULT_KNELL_THRESHOLD_DAYS;
                requiemThreshold = DEFAULT_REQUIEM_THRESHOLD_DAYS;
                return;
            }

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
            if (!Settings.options.EnableRequiemStages)
                return;

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

        private static void CureAllStageAfflictions()
        {
            var mgr = AfflictionManager.GetAfflictionManagerInstance();
            if (mgr?.m_Afflictions == null)
                return;

            for (int i = mgr.m_Afflictions.Count - 1; i >= 0; i--)
            {
                var affliction = mgr.m_Afflictions[i];
                if (affliction == null)
                    continue;

                if (affliction is OmenAffliction
                    || affliction is DirgeAffliction
                    || affliction is KnellAffliction
                    || affliction is RequiemAffliction)
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

        internal static int GetDynamicPredatorThreatLevel()
        {
            float heat = Core.State.PredatorHostility;

            if (heat >= 30f) return 8;
            if (heat >= 20f) return 7;
            if (heat >= 15f) return 6;
            if (heat >= 10f) return 5;
            if (heat >= 8f) return 4;
            if (heat >= 6f) return 3;
            if (heat >= 4f) return 2;
            if (heat >= 2f) return 1;
            return 0;
        }

        internal static int GetTotalPredatorThreatLevel()
        {
            if (!IsPredatorHostilityEnabled()) return 0;

            return GetBasePredatorThreatLevel() + GetDynamicPredatorThreatLevel();
        }

        internal static void RegisterPredatorKill(float hostilityAdded)
        {
            if (hostilityAdded <= 0f)
                return;

            if (Settings.options.PredatorHostilityMode == 2)
                return;

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
            float decay = PREDATOR_HOSTILITY_DECAY_PER_HOUR * gameHoursPassed;

            Core.State.PredatorHostility = Mathf.Max(0f, Core.State.PredatorHostility - decay);

            if (!Mathf.Approximately(before, Core.State.PredatorHostility))
            {
                Core.Instance?.MarkDirty();
                Core.Log($"predator hostility decay -> {before:0.##} -> {Core.State.PredatorHostility:0.##}");
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

        internal static void TryConvertPredatorBloodLossToSevereLaceration(string cause)
        {
            RefreshEffectsIfNeeded();

            if (!RequiemStagesEffects.ShouldConvertPredatorBloodLossToSevere(GetCurrentStage()))
                return;

            PlayerManager player = GameManager.GetPlayerManagerComponent();
            if (player == null || player.PlayerIsDead())
                return;

            if (_applyingSevere || _pendingSevere)
                return;

            SevereLacerations severe = GameManager.GetSevereLacerations();
            if (severe == null || severe.HasAffliction())
                return;

            string forwardedCause = string.IsNullOrWhiteSpace(cause) ? "Predator Attack" : cause;
            string lowerCause = forwardedCause.ToLowerInvariant();

            bool isPredatorCause =
                lowerCause.Contains("wolf") ||
                lowerCause.Contains("bear") ||
                lowerCause.Contains("cougar") ||
                lowerCause.Contains("predator");

            if (!isPredatorCause)
                return;

            _pendingSevere = true;
            MelonCoroutines.Start(ApplySevereNextFrame(forwardedCause));
        }

        internal static void TryHandleSevereLacerationHealing(SevereLacerations severe)
        {
            PlayerManager player = GameManager.GetPlayerManagerComponent();
            if (player == null || player.PlayerIsDead())
            {
                _severeWasActive = false;
                return;
            }

            if (severe == null)
                return;

            bool isActive = severe.HasAffliction();

            if (isActive)
            {
                _severeWasActive = true;
                return;
            }

            if (!_severeWasActive)
                return;

            _severeWasActive = false;

            AfflictionManager mgr = AfflictionManager.GetAfflictionManagerInstance();
            if (mgr == null)
                return;

            Core.State ??= new MMState();
            Core.State.ScarredFleshHistoryCount++;
            Core.Instance?.MarkDirty();

            if (!Settings.options.EnableScarredFlesh)
            {
                Core.Log($"SevereLacerations healed -> ScarredFlesh history increased to {Core.State.ScarredFleshHistoryCount}, but the system is disabled.");
                ForceRefreshEffects();
                return;
            }

            Core.Log($"SevereLacerations healed -> applying ScarredFlesh ({Core.State.ScarredFleshHistoryCount} total recorded).");

            new ScarredFleshAffliction(AfflictionBodyArea.Chest).Start();
            ForceRefreshEffects();
        }

        private static IEnumerator ApplySevereNextFrame(string cause)
        {
            yield return null;

            try
            {
                PlayerManager player = GameManager.GetPlayerManagerComponent();
                if (player == null || player.PlayerIsDead())
                    yield break;

                SevereLacerations severe = GameManager.GetSevereLacerations();
                if (severe == null || severe.HasAffliction())
                    yield break;

                _applyingSevere = true;
                severe.ApplySevereLacerations(cause);
            }
            finally
            {
                _applyingSevere = false;
                _pendingSevere = false;
            }
        }

        internal static int GetScarredFleshCount()
        {
            Core.State ??= new MMState();
            return Settings.options.EnableScarredFlesh ? Mathf.Max(0, Core.State.ScarredFleshHistoryCount) : 0;
        }

        internal static void SyncSettingsControlledAfflictions()
        {
            if (!IsReady())
                return;

            SyncScarredFleshFromHistory();
            SyncSepsisSystem();
            SyncCarbonMonoxideSystem();
            SyncBlackLungSystem();
            ForceRefreshEffects();
        }

        private static void SyncScarredFleshFromHistory()
        {
            Core.State ??= new MMState();

            if (!Settings.options.EnableScarredFlesh)
            {
                CureAllAfflictionsOfType<ScarredFleshAffliction>();
                return;
            }

            RefreshEffectsIfNeeded(force: true);

            int targetCount = Mathf.Max(0, Core.State.ScarredFleshHistoryCount);
            int activeCount = _cache.ScarredFleshCount;

            if (activeCount > targetCount)
            {
                Core.State.ScarredFleshHistoryCount = activeCount;
                Core.Instance?.MarkDirty();
                targetCount = activeCount;
                Core.Log($"ScarredFlesh sync -> migrated existing active count ({activeCount}) into persistent history.");
            }

            if (activeCount >= targetCount)
                return;

            int missingCount = targetCount - activeCount;
            Core.Log($"ScarredFlesh sync -> restoring {missingCount} missing instance(s) from history.");

            for (int i = 0; i < missingCount; i++)
            {
                new ScarredFleshAffliction(AfflictionBodyArea.Chest).Start();
            }
        }

        private static void SyncSepsisSystem()
        {
            if (Settings.options.EnableSepsis)
                return;

            CureAllAfflictionsOfType<MajorMiseries.Afflictions.SepsisRisk.SepsisRiskAffliction>();
            CureAllAfflictionsOfType<MajorMiseries.Afflictions.Sepsis.SepsisAffliction>();
        }

        private static void SyncCarbonMonoxideSystem()
        {
            if (Settings.options.EnableCarbonMonoxide)
                return;

            _coSceneStates.Clear();
            CureAllAfflictionsOfType<COExposureAffliction>();
            CureAllAfflictionsOfType<COPoisoningAffliction>();
        }

        private static void SyncBlackLungSystem()
        {
            if (Settings.options.EnableBlackLung)
                return;

            Core.State ??= new MMState();
            if (!Mathf.Approximately(Core.State.BlackLungExposure, 0f))
            {
                Core.State.BlackLungExposure = 0f;
                Core.Instance?.MarkDirty();
            }

            CureAllAfflictionsOfType<BlackLungRiskAffliction>();
            CureAllAfflictionsOfType<BlackLungAffliction>();
        }

        private static void CureAllAfflictionsOfType<TAffliction>() where TAffliction : class
        {
            AfflictionManager mgr = AfflictionManager.GetAfflictionManagerInstance();
            if (mgr?.m_Afflictions == null)
                return;

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
            if (player == null || player.PlayerIsDead())
                return;

            float chance = Settings.options.BearBrokenLimbChance;

            Fatigue fatigue = GameManager.GetFatigueComponent();
            if (fatigue != null && fatigue.m_CurrentFatigue <= 40f)
                chance += 10f;

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
            if (player == null || player.PlayerIsDead())
                return;

            float chance = Settings.options.MooseBrokenLimbChance;

            Fatigue fatigue = GameManager.GetFatigueComponent();
            if (fatigue != null && fatigue.m_CurrentFatigue <= 40f)
                chance += 10f;

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
                AfflictionBodyArea armArea = Random.Range(0, 2) == 0
                    ? AfflictionBodyArea.ArmLeft
                    : AfflictionBodyArea.ArmRight;

                AfflictionBodyArea legArea = Random.Range(0, 2) == 0
                    ? AfflictionBodyArea.LegLeft
                    : AfflictionBodyArea.LegRight;

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
                AfflictionBodyArea armArea = Random.Range(0, 2) == 0
                    ? AfflictionBodyArea.ArmLeft
                    : AfflictionBodyArea.ArmRight;

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
                AfflictionBodyArea legArea = Random.Range(0, 2) == 0
                    ? AfflictionBodyArea.LegLeft
                    : AfflictionBodyArea.LegRight;

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
            Core.Log($"fall damage begin -> conditionBefore={_conditionBeforeFall:0.###}");
        }

        internal static void EndFallDamageEvaluation()
        {
            if (_processingFallInjury)
                return;

            if (!IsReady())
                return;

            if (_conditionBeforeFall < 0f)
                return;

            MelonCoroutines.Start(EvaluateFallDamageNextFrame());
        }

        private static IEnumerator EvaluateFallDamageNextFrame()
        {
            yield return null;

            if (_processingFallInjury)
                yield break;

            if (!IsReady())
                yield break;

            PlayerManager player = GameManager.GetPlayerManagerComponent();
            if (player == null || player.PlayerIsDead())
                yield break;

            Condition cond = GameManager.GetConditionComponent();
            if (cond == null)
                yield break;

            if (_conditionBeforeFall < 0f)
                yield break;

            float conditionAfter = cond.GetNormalizedCondition();
            float lost = Mathf.Max(0f, _conditionBeforeFall - conditionAfter);

            Core.Log($"fall damage delayed end -> conditionBefore={_conditionBeforeFall:0.###}, conditionAfter={conditionAfter:0.###}, lost={lost:0.###}");

            _conditionBeforeFall = -1f;

            TryApplyBrokenLegFromFallDamage(lost);
        }

        private static void TryApplyBrokenLegFromFallDamage(float normalizedConditionLost)
        {
            if (_processingFallInjury)
                return;

            if (!IsReady())
                return;

            PlayerManager player = GameManager.GetPlayerManagerComponent();
            if (player == null || player.PlayerIsDead())
                return;

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
                Core.Log($"fall fracture skipped -> damage too low ({normalizedConditionLost:0.###})");
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

            if (_cache.BrokenLegLeft && !_cache.BrokenLegRight)
            {
                legArea = AfflictionBodyArea.LegRight;
            }
            else if (_cache.BrokenLegRight && !_cache.BrokenLegLeft)
            {
                legArea = AfflictionBodyArea.LegLeft;
            }
            else
            {
                legArea = Random.Range(0, 2) == 0
                    ? AfflictionBodyArea.LegLeft
                    : AfflictionBodyArea.LegRight;
            }

            float duration = Random.Range(BROKEN_LEG_MIN_HOURS, BROKEN_LEG_MAX_HOURS + 1);

            if (Settings.options.BrokenLimbDurationMode == 1)
            {
                duration /= 10f;
            }

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

            penalty += 2f * _cache.ScarredFleshCount;

            return penalty;
        }

        internal static bool HasStageEffect(RequiemStage minimumStage)
        {
            return GetCurrentStage() >= minimumStage;
        }

        internal static float GetSleepFatigueRecoveryMultiplier()
        {
            return HasStageEffect(RequiemStage.Omen) ? 0.5f : 1f;
        }

        internal static int GetAdjustedMaxSleepHours(int vanillaMaxHours)
        {
            if (!HasStageEffect(RequiemStage.Omen))
                return vanillaMaxHours;

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
            if (!HasStageEffect(RequiemStage.Requiem) || healthDelta >= 0f)
                return healthDelta;

            return healthDelta * 2f;
        }

        internal static float ApplySprintSpeedPenaltyToFinalMultiplier(float multiplier)
        {
            if (!HasStageEffect(RequiemStage.Knell))
                return multiplier;

            if (HasWeakJoints())
                return multiplier;

            return multiplier * 0.75f;
        }

        internal static bool ShouldBlockSprint()
        {
            RefreshEffectsIfNeeded();
            return _cache.BrokenLegCount > 0;
        }

        internal static bool ShouldBlockClimbing()
        {
            RefreshEffectsIfNeeded();
            return _cache.BrokenLegCount > 0 || _cache.BrokenArmCount > 0;
        }

        internal static float GetMovementSpeedMultiplier()
        {
            RefreshEffectsIfNeeded();

            float multiplier = 1f;

            if (_cache.BrokenLegLeft && _cache.BrokenLegRight)
                multiplier *= 0.45f;
            else if (_cache.BrokenLegCount > 0)
                multiplier *= 0.65f;

            if (HasStageEffect(RequiemStage.Knell))
                multiplier *= 0.9f;

            return multiplier;
        }

        internal static float GetMovementFatigueMultiplier()
        {
            RefreshEffectsIfNeeded();

            if (_cache.BrokenLegLeft && _cache.BrokenLegRight)
                return 2.0f;

            if (_cache.BrokenLegCount > 0)
                return 1.5f;

            return 1f;
        }

        internal static float GetCraftingTimeMultiplier()
        {
            RefreshEffectsIfNeeded();

            if (_cache.BrokenArmLeft && _cache.BrokenArmRight)
                return 2.0f;

            if (_cache.BrokenArmCount > 0)
                return 1.5f;

            return 1f;
        }

        internal static float GetAimSwayIncreaseMultiplier()
        {
            RefreshEffectsIfNeeded();

            if (_cache.BrokenArmLeft && _cache.BrokenArmRight)
                return 3f;

            if (_cache.BrokenArmCount > 0)
                return 2f;

            return 1f;
        }

        internal static float GetAimSwayDecreaseMultiplier()
        {
            RefreshEffectsIfNeeded();

            if (_cache.BrokenArmLeft && _cache.BrokenArmRight)
                return 0.45f;

            if (_cache.BrokenArmCount > 0)
                return 0.65f;

            return 1f;
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

            public int ScarredFleshCount;

            public int BrokenArmCount;
            public int BrokenLegCount;

            public bool BrokenArmLeft;
            public bool BrokenArmRight;
            public bool BrokenLegLeft;
            public bool BrokenLegRight;

            public void Reset()
            {
                this = default;
            }
        }

        private static Cache _cache;

        private static void RefreshEffectsIfNeeded(bool force = false)
        {
            float now = Time.unscaledTime;
            if (!force && (now - _lastRefreshUnscaledTime) < REFRESH_INTERVAL_SECONDS)
                return;

            _lastRefreshUnscaledTime = now;
            _cache.Reset();

            var mgr = AfflictionManager.GetAfflictionManagerInstance();
            var list = mgr?.m_Afflictions;
            if (list == null)
                return;

            for (int i = 0; i < list.Count; i++)
            {
                object? a;
                try { a = list[i]; }
                catch { break; }

                if (a == null)
                    continue;

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

                    case ScarredFleshAffliction:
                        _cache.ScarredFleshCount++;
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
            "AshMine",
            "AshCaveA",
            "AshCaveB",
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

        private static bool _blackLungSleepTrackingActive = false;
        private static float _blackLungTrackedSleepHours = 0f;

        private const float BLACK_LUNG_LOG_INTERVAL_HOURS = 10f / 60f; // 10 in-game minutes
        private static float s_LastBlackLungExposureLogTime = -999f;
        private static bool? s_LastBlackLungExposureLogWasIncrease = null;

        internal static float GetBlackLungExposureGainPerHour() => BLACK_LUNG_EXPOSURE_GAIN_PER_HOUR;
        internal static float GetBlackLungExposureDecayPerHour() => BLACK_LUNG_EXPOSURE_DECAY_PER_HOUR;
        internal static float GetBlackLungExposureMax() => BLACK_LUNG_EXPOSURE_MAX;
        internal static float GetBlackLungRiskGainPerHour() => BLACK_LUNG_RISK_GAIN_PER_HOUR;
        internal static float GetBlackLungRiskDecayPerHour() => BLACK_LUNG_RISK_DECAY_PER_HOUR;

        internal static void UpdateBlackLungExposure(float gameHoursPassed)
        {
            if (!Settings.options.EnableBlackLung)
                return;

            if (gameHoursPassed <= 0f)
                return;

            string sceneName = GameManager.m_ActiveScene;
            if (string.IsNullOrEmpty(sceneName))
                return;

            bool inCoalScene = IsBlackLungScene(sceneName);
            float now = GameManager.GetTimeOfDayComponent()?.GetHoursPlayedNotPaused() ?? 0f;

            BlackLungAffliction? activeBlackLung = GetAffliction<BlackLungAffliction>();
            if (activeBlackLung != null)
            {
                if (inCoalScene)
                {
                    float addedHours = gameHoursPassed * BLACK_LUNG_COAL_SCENE_WORSENING_MULTIPLIER;
                    activeBlackLung.EndTime += addedHours;
                    Core.Log($"BlackLung worsened by coal exposure -> +{addedHours:0.###}h remaining.");
                }

                return;
            }

            if (HasAffliction<BlackLungRiskAffliction>())
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

            if (inCoalScene)
            {
                Core.State.BlackLungExposure = Mathf.Clamp(
                    Core.State.BlackLungExposure + (gameHoursPassed * BLACK_LUNG_EXPOSURE_GAIN_PER_HOUR),
                    0f,
                    BLACK_LUNG_EXPOSURE_MAX);
            }
            else
            {
                Core.State.BlackLungExposure = Mathf.Clamp(
                    Core.State.BlackLungExposure - (gameHoursPassed * BLACK_LUNG_EXPOSURE_DECAY_PER_HOUR),
                    0f,
                    BLACK_LUNG_EXPOSURE_MAX);
            }

            if (!Mathf.Approximately(oldExposure, Core.State.BlackLungExposure))
            {
                Core.Instance?.MarkDirty();

                bool isIncrease = Core.State.BlackLungExposure > oldExposure;

                bool shouldLog =
                    (now - s_LastBlackLungExposureLogTime) >= BLACK_LUNG_LOG_INTERVAL_HOURS
                    || s_LastBlackLungExposureLogWasIncrease == null
                    || s_LastBlackLungExposureLogWasIncrease.Value != isIncrease;

                if (shouldLog)
                {
                    Core.Log($"BlackLung exposure {(isIncrease ? "increased" : "decreased")} in scene '{sceneName}' -> {oldExposure:0.###} => {Core.State.BlackLungExposure:0.###}");
                    s_LastBlackLungExposureLogTime = now;
                    s_LastBlackLungExposureLogWasIncrease = isIncrease;
                }
            }

            if (inCoalScene && Core.State.BlackLungExposure >= BLACK_LUNG_RISK_START_THRESHOLD)
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

            if (gameHoursPassed <= 0f)
                return;

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
        private const float CO_EXPOSURE_ROLL_CHANCE = 10f;           // percent per roll
        private const float CO_LINGER_AFTER_FIRE_OUT_HOURS = 2f;     // contaminated scene lingers for 2h after last valid fire

        private sealed class CORiskSceneState
        {
            public float LastRollTimeHours = -999f;
            public float LastValidFireSeenTimeHours = -999f;
            public bool SceneContaminated = false;
        }

        private static readonly Dictionary<string, CORiskSceneState> _coSceneStates = new();

        internal static void UpdateCOExposure(float gameHoursPassed)
        {
            if (!Settings.options.EnableCarbonMonoxide)
                return;

            if (gameHoursPassed <= 0f)
                return;

            TimeOfDay tod = GameManager.GetTimeOfDayComponent();
            if (tod == null)
                return;

            if (HasAffliction<COExposureAffliction>())
                return;

            if (HasAffliction<COPoisoningAffliction>())
                return;

            if (!IsPlayerInIndoorScene())
                return;

            string sceneName = GetCurrentSceneName();
            if (string.IsNullOrEmpty(sceneName))
                return;

            float nowHours = tod.GetHoursPlayedNotPaused();
            CORiskSceneState state = GetOrCreateCOSceneState(sceneName);

            bool hasValidFire = TryGetIndoorValidCOFireInfo(nowHours, out float qualifyingSinceHours);

            if (hasValidFire)
            {
                state.LastValidFireSeenTimeHours = nowHours;

                if (state.LastRollTimeHours < 0f)
                {
                    state.LastRollTimeHours = qualifyingSinceHours;
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

            if (!hasValidFire)
                return;

            float elapsed = nowHours - state.LastRollTimeHours;
            if (elapsed < CO_ROLL_INTERVAL_HOURS)
                return;

            int rollCount = Mathf.FloorToInt(elapsed / CO_ROLL_INTERVAL_HOURS);
            if (rollCount <= 0)
                return;

            for (int i = 0; i < rollCount; i++)
            {
                float roll = Random.Range(0f, 100f);
                Core.Log($"CO roll -> chance={CO_EXPOSURE_ROLL_CHANCE:0.##}% roll={roll:0.##} scene='{sceneName}'");

                if (roll <= CO_EXPOSURE_ROLL_CHANCE)
                {
                    state.SceneContaminated = true;
                    state.LastValidFireSeenTimeHours = nowHours;
                    state.LastRollTimeHours += (i + 1) * CO_ROLL_INTERVAL_HOURS;

                    Core.Log($"CO roll succeeded -> scene '{sceneName}' is now contaminated, applying COExposure.");
                    new COExposureAffliction(AfflictionBodyArea.Head).Start();
                    return;
                }
            }

            state.LastRollTimeHours += rollCount * CO_ROLL_INTERVAL_HOURS;
        }

        internal static bool IsPlayerStillInActiveCOScene()
        {
            if (!IsPlayerInIndoorScene())
                return false;

            TimeOfDay tod = GameManager.GetTimeOfDayComponent();
            if (tod == null)
                return false;

            string sceneName = GetCurrentSceneName();
            if (string.IsNullOrEmpty(sceneName))
                return false;

            if (!_coSceneStates.TryGetValue(sceneName, out CORiskSceneState? state) || state == null || !state.SceneContaminated)
                return false;

            float nowHours = tod.GetHoursPlayedNotPaused();
            bool hasValidFire = TryGetIndoorValidCOFireInfo(nowHours, out _);

            if (hasValidFire)
            {
                state.LastValidFireSeenTimeHours = nowHours;
                return true;
            }

            if (IsSceneStillCOContaminated(state, nowHours, hasValidFire: false))
                return true;

            Core.Log($"CO active scene check expired -> scene '{sceneName}' is no longer contaminated.");
            ResetCOSceneState(state);
            return false;
        }

        private static bool TryGetIndoorValidCOFireInfo(float nowHours, out float qualifyingSinceHours)
        {
            qualifyingSinceHours = -1f;

            if (!IsPlayerInIndoorScene())
                return false;

            if (FireManager.m_Fires == null)
                return false;

            int count = FireManager.m_Fires.Count;
            float longestBurnHours = -1f;

            for (int i = 0; i < count; i++)
            {
                Fire? fire = FireManager.m_Fires[i];
                if (fire == null)
                    continue;

                if (!fire.IsBurning())
                    continue;

                if (!IsCOEligibleFire(fire))
                    continue;

                float burnHours = fire.GetBurningTimeTODHours();
                if (burnHours < CO_MIN_FIRE_BURN_HOURS)
                    continue;

                if (burnHours > longestBurnHours)
                    longestBurnHours = burnHours;
            }

            if (longestBurnHours < CO_MIN_FIRE_BURN_HOURS)
                return false;

            qualifyingSinceHours = nowHours - (longestBurnHours - CO_MIN_FIRE_BURN_HOURS);
            return true;
        }

        private static bool IsCOEligibleFire(Fire fire)
        {
            if (fire == null || !fire.IsBurning())
                return false;

            bool isWoodStoveFire = IsWoodStoveFire(fire);
            bool isCampfireFire = IsCampfireFire(fire);

            if (isCampfireFire)
                return true;

            if (isWoodStoveFire)
                return IsFireBarrelWoodStove(fire);

            return false;
        }

        private static bool IsWoodStoveFire(Fire fire)
        {
            if (fire == null)
                return false;

            var woodStoves = FireManager.m_WoodStoves;
            if (woodStoves == null)
                return false;

            for (int i = 0; i < woodStoves.Count; i++)
            {
                WoodStove? woodStove = woodStoves[i];
                if (woodStove == null || woodStove.Fire == null)
                    continue;

                if (SameFire(woodStove.Fire, fire))
                    return true;
            }

            return false;
        }

        private static bool IsCampfireFire(Fire fire)
        {
            if (fire == null)
                return false;

            if (fire.m_Campfire != null)
                return true;

            var campfires = FireManager.m_Campfires;
            if (campfires == null)
                return false;

            for (int i = 0; i < campfires.Count; i++)
            {
                Campfire? campfire = campfires[i];
                if (campfire == null || campfire.Fire == null)
                    continue;

                if (SameFire(campfire.Fire, fire))
                    return true;
            }

            return false;
        }

        private static bool IsFireBarrelWoodStove(Fire fire)
        {
            WoodStove? woodStove = FindWoodStoveForFire(fire);
            if (woodStove == null)
                return false;

            string objectName = woodStove.gameObject != null ? woodStove.gameObject.name ?? string.Empty : string.Empty;
            if (objectName.Contains("FireBarrel", StringComparison.OrdinalIgnoreCase))
                return true;

            string path = GetTransformPath(woodStove.transform);
            if (path.Contains("FireBarrel", StringComparison.OrdinalIgnoreCase))
                return true;

            return false;
        }

        private static WoodStove? FindWoodStoveForFire(Fire fire)
        {
            if (fire == null)
                return null;

            var woodStoves = FireManager.m_WoodStoves;
            if (woodStoves == null)
                return null;

            for (int i = 0; i < woodStoves.Count; i++)
            {
                WoodStove? woodStove = woodStoves[i];
                if (woodStove == null || woodStove.Fire == null)
                    continue;

                if (SameFire(woodStove.Fire, fire))
                    return woodStove;
            }

            return null;
        }

        private static bool SameFire(Fire a, Fire b)
        {
            if (a == null || b == null)
                return false;

            return a.GetInstanceID() == b.GetInstanceID();
        }

        private static string GetTransformPath(Transform t)
        {
            if (t == null)
                return "<null>";

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
            if (!state.SceneContaminated)
                return false;

            if (hasValidFire)
                return true;

            if (state.LastValidFireSeenTimeHours < 0f)
                return false;

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
            if (weather == null)
                return false;

            return weather.IsIndoorScene();
        }

        // ===========================================================================
        //                               Utilities
        // ===========================================================================

        private static int GetTotalHoursAliveFromGame()
        {
            TimeOfDay tod = GameManager.GetTimeOfDayComponent();
            if (tod == null)
                return -1;

            int day = tod.GetDayNumber();
            int hour = Mathf.FloorToInt(tod.GetHour());

            return Mathf.Max(0, ((day - 1) * 24) + hour);
        }

        private static bool HasAffliction<T>() where T : class
        {
            var mgr = AfflictionManager.GetAfflictionManagerInstance();
            if (mgr?.m_Afflictions == null)
                return false;

            for (int i = 0; i < mgr.m_Afflictions.Count; i++)
            {
                if (mgr.m_Afflictions[i] is T)
                    return true;
            }

            return false;
        }

        internal static void ProcessBlackLungSleepRecovery(float hoursSlept)
        {
            if (hoursSlept <= 0f)
                return;

            BlackLungAffliction? blackLung = GetAffliction<BlackLungAffliction>();
            if (blackLung == null)
                return;

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

        private static T? GetAffliction<T>() where T : class
        {
            var mgr = AfflictionManager.GetAfflictionManagerInstance();
            if (mgr?.m_Afflictions == null)
                return null;

            for (int i = 0; i < mgr.m_Afflictions.Count; i++)
            {
                if (mgr.m_Afflictions[i] is T affliction)
                    return affliction;
            }

            return null;
        }

        internal static bool HasWeakJoints()
        {
            Condition? condition = GameManager.GetConditionComponent();
            return condition != null && condition.HasSpecificAffliction(AfflictionType.WeakJoints);
        }

        private static bool RollChance(float chance)
        {
            return Random.Range(0f, 100f) < chance;
        }
    }
}