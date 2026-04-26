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

            if (Settings.options.IsLogging && Core.Instance != null)
            {
                Core.Instance.LoggerInstance.Msg(
                    $"Saved -> " +
                    $"Scene:{GetCurrentSceneLogName()} | " +
                    $"Region:{RegionalAfflictionLogic.GetRegionLogName(Core.State.CurrentLogicalRegion)} | " +
                    $"LastKnownRegion:{RegionalAfflictionLogic.GetRegionLogName(Core.State.LastKnownLogicalRegion)} | " +
                    $"HomeAway:{Core.State.HomeSicknessHoursAway:0.###} | " +
                    $"RegionalDistress:{Core.State.RegionalDistressHoursInRegion:0.###} | " +
                    $"PredatorHostility:{Core.State.PredatorHostility:0.###} | " +
                    $"HrsSinceLastPredatorKill:{Core.State.HoursSinceLastPredatorKill:0.###} | " +
                    $"BlackLungExposure:{Core.State.BlackLungExposure:0.###} | " +
                    $"CorpseExposure:{Core.State.CorpseExposure:0.###} | " +
                    $"ScarredFleshHistory:{Core.State.ScarredFleshHistoryCount} | " +
                    $"Sprains:LW={Core.State.LeftWristSprainCount},RW={Core.State.RightWristSprainCount},LA={Core.State.LeftAnkleSprainCount},RA={Core.State.RightAnkleSprainCount}"
                );
            }
        }

        internal static void OnLoad()
        {
            string json = _manager.Load(SUFFIX);

            if (string.IsNullOrEmpty(json))
            {
                Core.State = new MMState();

                if (Settings.options.IsLogging && Core.Instance != null)
                    Core.Instance.LoggerInstance.Msg("Loaded -> empty data (fresh slot)");

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

            if (Settings.options.IsLogging && Core.Instance != null)
            {
                Core.Instance.LoggerInstance.Msg(
                    $"Loaded -> " +
                    $"Scene:{GetCurrentSceneLogName()} | " +
                    $"Region:{RegionalAfflictionLogic.GetRegionLogName(Core.State.CurrentLogicalRegion)} | " +
                    $"LastKnownRegion:{RegionalAfflictionLogic.GetRegionLogName(Core.State.LastKnownLogicalRegion)} | " +
                    $"HomeAway:{Core.State.HomeSicknessHoursAway:0.###} | " +
                    $"RegionalDistress:{Core.State.RegionalDistressHoursInRegion:0.###} | " +
                    $"PredatorHostility:{Core.State.PredatorHostility:0.###} | " +
                    $"HrsSinceLastPredatorKill:{Core.State.HoursSinceLastPredatorKill:0.###} | " +
                    $"BlackLungExposure:{Core.State.BlackLungExposure:0.###} | " +
                    $"CorpseExposure:{Core.State.CorpseExposure:0.###} | " +
                    $"ScarredFleshHistory:{Core.State.ScarredFleshHistoryCount} | " +
                    $"Sprains:LW={Core.State.LeftWristSprainCount},RW={Core.State.RightWristSprainCount},LA={Core.State.LeftAnkleSprainCount},RA={Core.State.RightAnkleSprainCount}"
                );
            }
        }

        internal static void OnNewGame()
        {
            Core.State = new MMState();

            if (Settings.options.IsLogging && Core.Instance != null)
                Core.Instance.LoggerInstance.Msg("Clearing data for new game");
        }

        private static void ClampAndFix()
        {
            EnsureState();

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