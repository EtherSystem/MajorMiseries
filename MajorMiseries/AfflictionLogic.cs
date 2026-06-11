using AfflictionComponent.Components;
using MajorMiseries.Managers;
using MajorMiseries.Patches;
using MajorMiseries.Persistence;
using static MajorMiseries.Afflictions.BlackLung;
using static MajorMiseries.Afflictions.BlackLungRisk;
using static MajorMiseries.Afflictions.BrokenArm;
using static MajorMiseries.Afflictions.BrokenLeg;
using static MajorMiseries.Afflictions.Buffs.HomeComfort;
using static MajorMiseries.Afflictions.COExposure;
using static MajorMiseries.Afflictions.COPoisoning;
using static MajorMiseries.Afflictions.CorpseSickness;
using static MajorMiseries.Afflictions.CorpseSicknessRisk;
using static MajorMiseries.Afflictions.HomeSickness;
using static MajorMiseries.Afflictions.RegionalDistress;
using static MajorMiseries.Afflictions.RequiemStagesAfflictions.Dirge;
using static MajorMiseries.Afflictions.RequiemStagesAfflictions.Knell;
using static MajorMiseries.Afflictions.RequiemStagesAfflictions.Omen;
using static MajorMiseries.Afflictions.RequiemStagesAfflictions.Requiem;
using static MajorMiseries.Afflictions.SevereAnkleSprain;
using static MajorMiseries.Afflictions.SevereWristSprain;
using Random = UnityEngine.Random;

namespace MajorMiseries
{
    internal static partial class AfflictionLogic
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
            ResetSepsisInfectionRiskRollTracking();

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

        private static bool RollChance(float chance)
        {
            return Random.Range(0f, 100f) < chance;
        }

        // =======================================================================================
        //                        Settings-controlled affliction sync
        // =======================================================================================

        internal static void SyncSettingsControlledAfflictions()
        {
            if (!IsReady()) return;

            SyncScarredFleshFromHistory();
            SyncSepsisSystem();
            SyncCarbonMonoxideSystem();
            SyncBlackLungSystem();
            SyncCorpseSicknessSystem();
            SyncAuroraInfluenceSystem();
            SevereSprainManager.SyncFromState();
            ForceRefreshEffects();
        }

        private static void SyncScarredFleshFromHistory()
        {
            EnsureScarredFleshDisplay();
        }

        private static void SyncSepsisSystem()
        {
            if (Settings.options.EnableSepsis) return;

            ResetSepsisInfectionRiskRollTracking();
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

        private static void SyncAuroraInfluenceSystem()
        {
            AuroraInfluenceManager.SyncAfflictionDisplayFromState();
        }
    }
}