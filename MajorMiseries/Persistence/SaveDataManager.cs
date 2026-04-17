using Newtonsoft.Json;
using ModData;

namespace MajorMiseries.Persistence
{
    internal static class SaveDataManager
    {
        private static readonly ModDataManager _manager = new("MajorMiseries", false);
        private const string SUFFIX = "mmdata";

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
                    $"Saved -> PredatorHostility:{Core.State.PredatorHostility:0.###} | " +
                    $"HrsSinceLastPredatorKill:{Core.State.HoursSinceLastPredatorKill:0.###} | " +
                    $"BlackLungExposure:{Core.State.BlackLungExposure:0.###}"
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
                    $"Loaded -> PredatorHostility:{Core.State.PredatorHostility:0.###} | " +
                    $"HrsSinceLastPredatorKill:{Core.State.HoursSinceLastPredatorKill:0.###} | " +
                    $"BlackLungExposure:{Core.State.BlackLungExposure:0.###}"
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