using Newtonsoft.Json;
using ModData;
using MajorMiseries.Managers;

namespace MajorMiseries.Persistence
{
    internal static class SaveDataManager
    {
        private static readonly ModDataManager _manager = new("MajorMiseries", false);
        private const string SUFFIX = "mmdata";
        private const string SETTINGS_SUFFIX = "mmsettings";

        private static bool _slotSettingsLoaded = false;
        private static bool _majorMEnabledForCurrentSlot = false;

        internal static bool IsMajorMEnabledForCurrentSlot => _slotSettingsLoaded && _majorMEnabledForCurrentSlot;

        private static string GetCurrentSceneLogName()
        {
            string scene = GameManager.m_ActiveScene;
            return string.IsNullOrEmpty(scene) ? "Unknown" : scene;
        }

        private static void EnsureState()
        {
            Core.State ??= new MMState();
        }

        internal static void OnSave()
        {
            if (!IsMajorMEnabledForCurrentSlot) return;

            EnsureState();

            string json = JsonConvert.SerializeObject(Core.State);
            _manager.Save(json, SUFFIX);

            LogStateSnapshot("Saved");
        }

        internal static void OnLoad()
        {
            string json = _manager.Load(SUFFIX);

            if (string.IsNullOrEmpty(json))
            {
                Core.State = new MMState();

                LogStateSnapshot("Loaded", "empty data / fresh slot");

                return;
            }

            MMState? loaded = null;
            try
            {
                loaded = JsonConvert.DeserializeObject<MMState>(json);
            }
            catch
            {
                // corrupted data -> reset safe
            }

            Core.State = loaded ?? new MMState();
            ClampAndFix();

            LogStateSnapshot(loaded == null ? "Loaded" : "Loaded", loaded == null ? "corrupted data / reset safe" : null);
        }

        internal static void OnNewGame()
        {
            Core.State = new MMState();

            Core.Log("Clearing data for new game");
        }

        internal static void LoadSettingsForCurrentSlot()
        {
            _slotSettingsLoaded = false;
            _majorMEnabledForCurrentSlot = false;

            string json;

            try
            {
                json = _manager.Load(SETTINGS_SUFFIX);
            }
            catch (Exception ex)
            {
                Core.Warn($"Settings : failed to load save-slot settings ({ex.Message}).", false);
                return;
            }

            if (string.IsNullOrEmpty(json))
            {
                _slotSettingsLoaded = true;

                if (HasExistingGameplayData())
                {
                    Settings.options.EnableMajorMiseries = true;
                    _majorMEnabledForCurrentSlot = true;
                    SaveSettingsForCurrentSlot(force: true);

                    Settings.RefreshVisibility();
                    Settings.options.RefreshGUI();

                    Core.Log("Settings : no save-slot settings found, existing MajorMiseries gameplay data detected; migrated current ModSettings values and enabled Major Miseries for this slot.", false);
                    return;
                }

                Settings.options.EnableMajorMiseries = false;
                _majorMEnabledForCurrentSlot = false;

                SaveDisabledSettingsForCurrentSlot();

                Settings.RefreshVisibility();
                Settings.options.RefreshGUI();

                Core.Log("Settings : no save-slot settings found, created disabled save-slot settings only.", false);
                return;
            }

            MMSaveSlotSettings? loaded = null;

            try
            {
                loaded = JsonConvert.DeserializeObject<MMSaveSlotSettings>(json);
            }
            catch
            {
                // corrupted slot settings -> disable safely for this slot
            }

            if (loaded == null)
            {
                _slotSettingsLoaded = true;
                Settings.options.EnableMajorMiseries = false;
                _majorMEnabledForCurrentSlot = false;

                SaveDisabledSettingsForCurrentSlot();

                Settings.RefreshVisibility();
                Settings.options.RefreshGUI();

                Core.Warn("Settings : corrupted save-slot settings, disabled Major Miseries for this slot.", false);
                return;
            }

            loaded.Apply();
            _slotSettingsLoaded = true;
            _majorMEnabledForCurrentSlot = Settings.options.EnableMajorMiseries;

            Settings.RefreshVisibility();
            Settings.options.RefreshGUI();

            Core.Log($"Settings : save-slot settings loaded. Major Miseries enabled:{_majorMEnabledForCurrentSlot}.");
        }

        private static bool HasExistingGameplayData()
        {
            try
            {
                return !string.IsNullOrEmpty(_manager.Load(SUFFIX));
            }
            catch
            {
                return false;
            }
        }

        private static void SaveDisabledSettingsForCurrentSlot()
        {
            try
            {
                string json = JsonConvert.SerializeObject(new MMDisabledSaveSlotSettings());
                _manager.Save(json, SETTINGS_SUFFIX);
            }
            catch (Exception ex)
            {
                Core.Warn($"Settings : failed to save disabled save-slot settings ({ex.Message}).", false);
                return;
            }

            Core.Log("Settings : disabled save-slot settings saved.");
        }

        internal static void SaveSettingsForCurrentSlot(bool force = false)
        {
            if (!force && !_slotSettingsLoaded) return;

            _majorMEnabledForCurrentSlot = Settings.options.EnableMajorMiseries;

            if (!Settings.options.EnableMajorMiseries)
            {
                SaveDisabledSettingsForCurrentSlot();
                return;
            }

            try
            {
                string json = JsonConvert.SerializeObject(MMSaveSlotSettings.FromCurrent());
                _manager.Save(json, SETTINGS_SUFFIX);
            }
            catch (Exception ex)
            {
                Core.Warn($"Settings : failed to save save-slot settings ({ex.Message}).", false);
                return;
            }

            Core.Log("Settings : save-slot settings saved.");
        }

        internal static void ApplySettingsConfirmedState()
        {
            if (!_slotSettingsLoaded) return;

            bool wasEnabled = _majorMEnabledForCurrentSlot;
            bool enabledNow = Settings.options.EnableMajorMiseries;

            _majorMEnabledForCurrentSlot = enabledNow;

            if (!enabledNow)
            {
                Core.Instance?.ResetRuntime();
                Core.State = new MMState();
                SaveSettingsForCurrentSlot(force: true);

                Core.Log("Settings : Major Miseries disabled for this save slot.", false);
                return;
            }

            if (!wasEnabled)
            {
                Core.Instance?.ResetRuntime();
                OnLoad();
                Core.Instance?.OnStateLoaded();

                Core.Log("Settings : Major Miseries enabled for this save slot.", false);
            }
        }

        internal static void InitializeDisabledSettingsForCurrentSlot()
        {
            _slotSettingsLoaded = true;
            _majorMEnabledForCurrentSlot = false;

            Settings.options.EnableMajorMiseries = false;
            SaveDisabledSettingsForCurrentSlot();

            Settings.RefreshVisibility();
            Settings.options.RefreshGUI();
        }

        internal static void InitializeSettingsForCurrentSlot()
        {
            _slotSettingsLoaded = true;
            _majorMEnabledForCurrentSlot = Settings.options.EnableMajorMiseries;
            SaveSettingsForCurrentSlot(force: true);
        }

        internal static void ForgetLoadedSlotSettings()
        {
            _slotSettingsLoaded = false;
            _majorMEnabledForCurrentSlot = false;
        }

        private static void LogStateSnapshot(string title, string? note = null)
        {
            EnsureState();

            Core.Log($"================== {title} ==================");

            if (!string.IsNullOrEmpty(note))
            {
                Core.Log($"Data : {note}");
            }

            Core.Log(
                $"Region : " +
                $"Scene:{GetCurrentSceneLogName()} | " +
                $"Current:{RegionalAfflictionManager.GetRegionLogName(Core.State.CurrentLogicalRegion)} | " +
                $"LastKnown:{RegionalAfflictionManager.GetRegionLogName(Core.State.LastKnownLogicalRegion)} | " +
                $"Home:{RegionalAfflictionManager.GetRegionLogName(Core.State.ConfiguredHomeRegion)} | " +
                $"Distress:{RegionalAfflictionManager.GetRegionLogName(Core.State.ConfiguredRegionalDistressRegion)} | " +
                $"HomeAway:{Core.State.HomeSicknessHoursAway:0.###} | " +
                $"RegionalDistress:{Core.State.RegionalDistressHoursInRegion:0.###} | " +
                $"HomeMoveCooldown:{Core.State.HomeRegionRelocationCooldownHoursRemaining:0.###}");

            Core.Log(
                $"Immunity : " +
                $"Shield:{Core.State.ImmunityShield:0.###} | " +
                $"FeverOnset:{Core.State.FeverOnsetHours:0.###} | " +
                $"FeverLinger:{Core.State.FeverLingerHoursRemaining:0.###} | " +
                $"InternalBodyTemp:{Core.State.InternalBodyTemp:0.###}");

            Core.Log("Sprains :");
            Core.Log(
                $"  LeftWrist  : " +
                $"Count:{Core.State.LeftWristSprainCount} | " +
                $"Window:{Core.State.LeftWristSprainWindowHours:0.###} | " +
                $"Risk:{Core.State.LeftWristSevereSprainRisk:0.###}");

            Core.Log(
                $"  RightWrist : " +
                $"Count:{Core.State.RightWristSprainCount} | " +
                $"Window:{Core.State.RightWristSprainWindowHours:0.###} | " +
                $"Risk:{Core.State.RightWristSevereSprainRisk:0.###}");

            Core.Log(
                $"  LeftAnkle  : " +
                $"Count:{Core.State.LeftAnkleSprainCount} | " +
                $"Window:{Core.State.LeftAnkleSprainWindowHours:0.###} | " +
                $"Risk:{Core.State.LeftAnkleSevereSprainRisk:0.###}");

            Core.Log(
                $"  RightAnkle : " +
                $"Count:{Core.State.RightAnkleSprainCount} | " +
                $"Window:{Core.State.RightAnkleSprainWindowHours:0.###} | " +
                $"Risk:{Core.State.RightAnkleSevereSprainRisk:0.###}");

            Core.Log(
                $"Others : " +
                $"PredatorHostility:{Core.State.PredatorHostility:0.###} | " +
                $"HrsSinceLastPredatorKill:{Core.State.HoursSinceLastPredatorKill:0.###} | " +
                $"BlackLungExposure:{Core.State.BlackLungExposure:0.###} | " +
                $"CorpseExposure:{Core.State.CorpseExposure:0.###} | " +
                $"ScarredFleshHistory:{Core.State.ScarredFleshHistoryCount}");

            Core.Log("===========================================");
        }

        private static void ClampAndFix()
        {
            EnsureState();

            Core.State.ImmunityShield = Mathf.Clamp(Core.State.ImmunityShield, 0f, 100f);
            Core.State.FeverOnsetHours = Mathf.Clamp(Core.State.FeverOnsetHours, 0f, 1f);
            Core.State.FeverLingerHoursRemaining = Mathf.Clamp(Core.State.FeverLingerHoursRemaining, 0f, 3f);
            Core.State.InternalBodyTemp = NormalizeLoadedBodyTemperature(Core.State.InternalBodyTemp);

            Core.State.PredatorHostility = Mathf.Max(0f, Core.State.PredatorHostility);
            Core.State.HoursSinceLastPredatorKill = Mathf.Max(0f, Core.State.HoursSinceLastPredatorKill);
            Core.State.BlackLungExposure = Mathf.Clamp(Core.State.BlackLungExposure, 0f, AfflictionLogic.GetBlackLungExposureMax());
            Core.State.ScarredFleshHistoryCount = Mathf.Max(0, Core.State.ScarredFleshHistoryCount);
            Core.State.CorpseExposure = Mathf.Clamp(Core.State.CorpseExposure, 0f, AfflictionLogic.GetCorpseExposureMax());

            Core.State.LeftWristSprainCount = Mathf.Max(0, Core.State.LeftWristSprainCount);
            Core.State.RightWristSprainCount = Mathf.Max(0, Core.State.RightWristSprainCount);
            Core.State.LeftAnkleSprainCount = Mathf.Max(0, Core.State.LeftAnkleSprainCount);
            Core.State.RightAnkleSprainCount = Mathf.Max(0, Core.State.RightAnkleSprainCount);

            Core.State.LastKnownLogicalRegion ??= string.Empty;
            Core.State.CurrentLogicalRegion ??= string.Empty;
            Core.State.ConfiguredHomeRegion ??= string.Empty;
            Core.State.ConfiguredRegionalDistressRegion ??= string.Empty;
            Core.State.HomeSicknessHoursAway = Mathf.Max(0f, Core.State.HomeSicknessHoursAway);
            Core.State.RegionalDistressHoursInRegion = Mathf.Max(0f, Core.State.RegionalDistressHoursInRegion);
        }

        private static float NormalizeLoadedBodyTemperature(float value)
        {
            if (value >= 30f && value <= 43f) return value;

            return 37f;
        }
    }

    internal sealed class MMSaveSlotSettings
    {
        public int Version = 2;
        public bool EnableMajorMiseries = false;

        public bool EnableRequiemStages = false;
        public int PredatorHostilityMode = 0;
        public int VitaminCDrainMode = 0;
        public int VitaminCDrainPreset = 1;
        public bool CustomizeStageThresholds = false;
        public int OmenThreshold = 10;
        public int DirgeThreshold = 20;
        public int KnellThreshold = 35;
        public int RequiemThreshold = 50;

        public float BearBrokenLimbChance = 20f;
        public float MooseBrokenLimbChance = 30f;
        public bool AllowDoubleBrokenLimb = false;
        public int BrokenLimbDurationMode = 0;
        public int PredatorBloodLossToSevereLacerationsMode = 0;

        public bool EnableBlackLung = true;
        public int BlackLungDurationMode = 0;
        public bool EnableCarbonMonoxide = true;

        public bool EnableSevereSprains = true;
        public int SevereSprainPreset = 1;

        public bool EnableScarredFlesh = true;
        public bool EnableSepsis = true;

        public bool EnableCorpseSickness = true;
        public bool EnableHumanCorpseExposure = true;
        public bool EnableAnimalCarcassExposure = true;
        public int HumanCorpseExposureRadiusMeters = 15;
        public int AnimalCarcassExposureRadiusMeters = 10;
        public int HumanCorpseHoursToRisk = 1;
        public int AnimalCarcassHoursToRisk = 5;

        public bool EnableRegionalAfflictions = true;
        public int HomeRegion = 0;
        public int HomeSicknessDelayHours = 72;
        public int RegionalDistressRegion = 0;
        public int RegionalDistressDelayHours = 72;

        public bool EnableImmunityShield = true;
        public bool MaxConditionPenaltiesBlockImmunityRegen = true;

        public bool EnableBodyHeat = true;
        public float PassiveHeatGain = 2.5f;
        public float WalkHeatGain = 0.25f;
        public float EncumberedHeatGain = 1.25f;
        public float SprintHeatGain = 5f;
        public float ClimbHeatGain = 10f;
        public float CoolingLoss = 0.5f;

        internal static MMSaveSlotSettings FromCurrent()
        {
            MMSettings s = Settings.options;

            return new MMSaveSlotSettings
            {
                EnableMajorMiseries = s.EnableMajorMiseries,
                EnableRequiemStages = s.EnableRequiemStages,
                PredatorHostilityMode = s.PredatorHostilityMode,
                VitaminCDrainMode = s.VitaminCDrainMode,
                VitaminCDrainPreset = s.VitaminCDrainPreset,
                CustomizeStageThresholds = s.CustomizeStageThresholds,
                OmenThreshold = s.OmenThreshold,
                DirgeThreshold = s.DirgeThreshold,
                KnellThreshold = s.KnellThreshold,
                RequiemThreshold = s.RequiemThreshold,

                BearBrokenLimbChance = s.BearBrokenLimbChance,
                MooseBrokenLimbChance = s.MooseBrokenLimbChance,
                AllowDoubleBrokenLimb = s.AllowDoubleBrokenLimb,
                BrokenLimbDurationMode = s.BrokenLimbDurationMode,
                PredatorBloodLossToSevereLacerationsMode = s.PredatorBloodLossToSevereLacerationsMode,

                EnableBlackLung = s.EnableBlackLung,
                BlackLungDurationMode = s.BlackLungDurationMode,
                EnableCarbonMonoxide = s.EnableCarbonMonoxide,

                EnableSevereSprains = s.EnableSevereSprains,
                SevereSprainPreset = s.SevereSprainPreset,

                EnableScarredFlesh = s.EnableScarredFlesh,
                EnableSepsis = s.EnableSepsis,

                EnableCorpseSickness = s.EnableCorpseSickness,
                EnableHumanCorpseExposure = s.EnableHumanCorpseExposure,
                EnableAnimalCarcassExposure = s.EnableAnimalCarcassExposure,
                HumanCorpseExposureRadiusMeters = s.HumanCorpseExposureRadiusMeters,
                AnimalCarcassExposureRadiusMeters = s.AnimalCarcassExposureRadiusMeters,
                HumanCorpseHoursToRisk = s.HumanCorpseHoursToRisk,
                AnimalCarcassHoursToRisk = s.AnimalCarcassHoursToRisk,

                EnableRegionalAfflictions = s.EnableRegionalAfflictions,
                HomeRegion = s.HomeRegion,
                HomeSicknessDelayHours = s.HomeSicknessDelayHours,
                RegionalDistressRegion = s.RegionalDistressRegion,
                RegionalDistressDelayHours = s.RegionalDistressDelayHours,

                EnableImmunityShield = s.EnableImmunityShield,
                MaxConditionPenaltiesBlockImmunityRegen = s.MaxConditionPenaltiesBlockImmunityRegen,

                EnableBodyHeat = s.EnableBodyHeat,
                PassiveHeatGain = s.PassiveHeatGain,
                WalkHeatGain = s.WalkHeatGain,
                EncumberedHeatGain = s.EncumberedHeatGain,
                SprintHeatGain = s.SprintHeatGain,
                ClimbHeatGain = s.ClimbHeatGain,
                CoolingLoss = s.CoolingLoss
            };
        }

        internal void Apply()
        {
            MMSettings s = Settings.options;

            s.EnableMajorMiseries = EnableMajorMiseries;
            s.EnableRequiemStages = EnableRequiemStages;
            s.PredatorHostilityMode = PredatorHostilityMode;
            s.VitaminCDrainMode = VitaminCDrainMode;
            s.VitaminCDrainPreset = VitaminCDrainPreset;
            s.CustomizeStageThresholds = CustomizeStageThresholds;
            s.OmenThreshold = OmenThreshold;
            s.DirgeThreshold = DirgeThreshold;
            s.KnellThreshold = KnellThreshold;
            s.RequiemThreshold = RequiemThreshold;

            s.BearBrokenLimbChance = BearBrokenLimbChance;
            s.MooseBrokenLimbChance = MooseBrokenLimbChance;
            s.AllowDoubleBrokenLimb = AllowDoubleBrokenLimb;
            s.BrokenLimbDurationMode = BrokenLimbDurationMode;
            s.PredatorBloodLossToSevereLacerationsMode = PredatorBloodLossToSevereLacerationsMode;

            s.EnableBlackLung = EnableBlackLung;
            s.BlackLungDurationMode = BlackLungDurationMode;
            s.EnableCarbonMonoxide = EnableCarbonMonoxide;

            s.EnableSevereSprains = EnableSevereSprains;
            s.SevereSprainPreset = SevereSprainPreset;

            s.EnableScarredFlesh = EnableScarredFlesh;
            s.EnableSepsis = EnableSepsis;

            s.EnableCorpseSickness = EnableCorpseSickness;
            s.EnableHumanCorpseExposure = EnableHumanCorpseExposure;
            s.EnableAnimalCarcassExposure = EnableAnimalCarcassExposure;
            s.HumanCorpseExposureRadiusMeters = HumanCorpseExposureRadiusMeters;
            s.AnimalCarcassExposureRadiusMeters = AnimalCarcassExposureRadiusMeters;
            s.HumanCorpseHoursToRisk = HumanCorpseHoursToRisk;
            s.AnimalCarcassHoursToRisk = AnimalCarcassHoursToRisk;

            s.EnableRegionalAfflictions = EnableRegionalAfflictions;
            s.HomeRegion = HomeRegion;
            s.HomeSicknessDelayHours = HomeSicknessDelayHours;
            s.RegionalDistressRegion = RegionalDistressRegion;
            s.RegionalDistressDelayHours = RegionalDistressDelayHours;

            s.EnableImmunityShield = EnableImmunityShield;
            s.MaxConditionPenaltiesBlockImmunityRegen = MaxConditionPenaltiesBlockImmunityRegen;

            s.EnableBodyHeat = EnableBodyHeat;
            s.PassiveHeatGain = PassiveHeatGain;
            s.WalkHeatGain = WalkHeatGain;
            s.EncumberedHeatGain = EncumberedHeatGain;
            s.SprintHeatGain = SprintHeatGain;
            s.ClimbHeatGain = ClimbHeatGain;
            s.CoolingLoss = CoolingLoss;
        }
    }

    internal sealed class MMDisabledSaveSlotSettings
    {
        public int Version = 2;
        public bool EnableMajorMiseries = false;
    }

    [HarmonyPatch(typeof(SaveGameSlots), nameof(SaveGameSlots.WriteSlotToDisk), [typeof(SlotData), typeof(SaveGameSlots.Timestamp)])]
    internal class MajorMiseries_SavePatch
    {
        private static void Prefix()
        {
            Core.Instance?.SaveIfDirty();
        }
    }

    [HarmonyPatch(typeof(GameManager), nameof(GameManager.LoadSaveGameSlot), [typeof(string), typeof(int)])]
    internal class MajorMiseries_LoadPatch
    {
        private static void Postfix()
        {
            Core.Instance?.ResetRuntime();
            SaveDataManager.LoadSettingsForCurrentSlot();

            if (!SaveDataManager.IsMajorMEnabledForCurrentSlot)
            {
                Core.Log("Major Miseries is disabled for this save slot.", false);
                return;
            }

            SaveDataManager.OnLoad();
            Core.Instance?.OnStateLoaded();
        }
    }

    [HarmonyPatch(typeof(SaveGameSlots), nameof(SaveGameSlots.CreateSlot), [typeof(string), typeof(SaveSlotType), typeof(uint), typeof(Episode)])]
    internal class MajorMiseries_NewGamePatch
    {
        private static void Postfix()
        {
            Core.Instance?.ResetRuntime();
            Core.State = new MMState();

            SaveDataManager.InitializeDisabledSettingsForCurrentSlot();

            Core.Log("Major Miseries is disabled by default for this new save slot.", false);
        }
    }

    [HarmonyPatch(typeof(GameManager), nameof(GameManager.DoExitToMainMenu))]
    [HarmonyPatch(typeof(GameManager), nameof(GameManager.LoadMainMenu))]
    internal class MajorMiseries_MainMenuPatch
    {
        private static void Postfix()
        {
            Core.Instance?.ResetRuntime();
            Core.State = new MMState();
            SaveDataManager.ForgetLoadedSlotSettings();
        }
    }
}