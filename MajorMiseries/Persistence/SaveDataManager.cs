using Newtonsoft.Json;
using ModData;

namespace MajorMiseries.Persistence
{
    internal static class SaveDataManager
    {
        private static readonly ModDataManager _manager = new("MajorMiseries", false);
        private const string SUFFIX = "mmdata";

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
                $"Current:{RegionalAfflictionLogic.GetRegionLogName(Core.State.CurrentLogicalRegion)} | " +
                $"LastKnown:{RegionalAfflictionLogic.GetRegionLogName(Core.State.LastKnownLogicalRegion)} | " +
                $"Home:{RegionalAfflictionLogic.GetRegionLogName(Core.State.ConfiguredHomeRegion)} | " +
                $"Distress:{RegionalAfflictionLogic.GetRegionLogName(Core.State.ConfiguredRegionalDistressRegion)} | " +
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

    [HarmonyPatch(typeof(SaveGameSlots), nameof(SaveGameSlots.WriteSlotToDisk), new Type[] { typeof(SlotData), typeof(SaveGameSlots.Timestamp) })]
    internal class MajorMiseries_SavePatch
    {
        private static void Prefix()
        {
            Core.Instance?.SaveIfDirty();
        }
    }

    [HarmonyPatch(typeof(GameManager), nameof(GameManager.LoadSaveGameSlot), new Type[] { typeof(string), typeof(int) })]
    internal class MajorMiseries_LoadPatch
    {
        private static void Postfix()
        {
            Core.Instance?.ResetRuntime();
            SaveDataManager.OnLoad();
            Core.Instance?.OnStateLoaded();
        }
    }

    [HarmonyPatch(typeof(SaveGameSlots), nameof(SaveGameSlots.CreateSlot), new Type[] { typeof(string), typeof(SaveSlotType), typeof(uint), typeof(Episode) })]
    internal class MajorMiseries_NewGamePatch
    {
        private static void Postfix()
        {
            Core.Instance?.ResetAll();
        }
    }

    [HarmonyPatch(typeof(GameManager), nameof(GameManager.DoExitToMainMenu))]
    [HarmonyPatch(typeof(GameManager), nameof(GameManager.LoadMainMenu))]
    internal class MajorMiseries_MainMenuPatch
    {
        private static void Postfix()
        {
            Core.Instance?.ResetAll();
        }
    }
}