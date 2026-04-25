using LocalizationUtilities;
using MajorMiseries.Persistence;

[assembly: MelonInfo(typeof(MajorMiseries.Core), "Major Miseries", "1.0.0", "EtherSystem", null)]
[assembly: MelonGame("Hinterland", "TheLongDark")]

namespace MajorMiseries
{
    public class Core : MelonMod
    {
        public static Core? Instance { get; private set; }
        internal static MMState State = new();
        private bool _dirty = false;

        public static string? LoadEmbeddedJSON(string localization)
        {
            string? result = null;

            Stream? stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("MajorMiseries.Resources.Localization.Localization.json");

            if (stream != null)
            {
                using StreamReader reader = new(stream);
                result = reader.ReadToEnd();
            }

            return result;
        }

        // -------------------------log helper-----------------------------------
        internal static void Log(string message, bool onlyWhenDebugEnabled = true)
        {
            if (onlyWhenDebugEnabled && !Settings.options.IsLogging)
                return;

            Instance?.LoggerInstance.Msg(message);
        }

        internal void MarkDirty()
        {
            _dirty = true;
        }

        public override void OnInitializeMelon()
        {
            Instance = this;
            LocalizationManager.LoadJsonLocalization(LoadEmbeddedJSON("Localization.json"));
            Log("Initialized.", false);
            Settings.OnLoad();

            //dev console commands
            DevConsoleCommands.Register();
        }

        private float _gameplayUpdateTimer = 0f;
        private bool _pendingStageSync = false;

        public void SaveIfDirty()
        {
            if (!_dirty) return;
            SaveDataManager.OnSave();
            _dirty = false;
        }

        public void ResetRuntime()
        {
            _dirty = false;
            _pendingStageSync = false;
            _gameplayUpdateTimer = 0f;

            AfflictionLogic.ResetRuntime();
            Patches.WildlifePatches.ResetRuntime();
        }

        public void OnStateLoaded()
        {
            State ??= new MMState();

            bool changed = false;

            float oldHostility = State.PredatorHostility;
            float oldSinceKill = State.HoursSinceLastPredatorKill;
            float oldBlackLungExposure = State.BlackLungExposure;
            float oldCorpseExposure = State.CorpseExposure;
            int oldScarredFleshHistory = State.ScarredFleshHistoryCount;

            State.PredatorHostility = Mathf.Max(0f, State.PredatorHostility);
            State.HoursSinceLastPredatorKill = Mathf.Max(0f, State.HoursSinceLastPredatorKill);
            State.BlackLungExposure = Mathf.Clamp(State.BlackLungExposure, 0f, AfflictionLogic.GetBlackLungExposureMax());
            State.CorpseExposure = Mathf.Clamp(State.CorpseExposure, 0f, AfflictionLogic.GetCorpseExposureMax());
            State.ScarredFleshHistoryCount = Mathf.Max(0, State.ScarredFleshHistoryCount);

            if (!Mathf.Approximately(oldHostility, State.PredatorHostility)) changed = true;
            if (!Mathf.Approximately(oldSinceKill, State.HoursSinceLastPredatorKill)) changed = true;
            if (!Mathf.Approximately(oldBlackLungExposure, State.BlackLungExposure)) changed = true;
            if (!Mathf.Approximately(oldCorpseExposure, State.CorpseExposure)) changed = true;
            if (oldScarredFleshHistory != State.ScarredFleshHistoryCount) changed = true;

            if (changed) _dirty = true;

            _pendingStageSync = true;
        }

        public void ResetAll()
        {
            SaveDataManager.OnNewGame();
            ResetRuntime();
        }

        public override void OnUpdate()
        {
            if (GameManager.m_Instance == null || GameManager.m_IsPaused) return;

            string scene = GameManager.m_ActiveScene;
            if (scene == "MainMenu" || scene == "Boot" || scene == "Empty") return;

            if (_pendingStageSync)
            {
                if (!AfflictionLogic.IsReady()) return;

                AfflictionLogic.ApplyCurrentStageFromGame();
                AfflictionLogic.SyncSettingsControlledAfflictions();

                AfflictionLogic.RebuildHumanCorpseSceneCache();
                AfflictionLogic.SeedAnimalCarcassCacheFromScene();

                _pendingStageSync = false;
                Log("stage/settings sync executed");
            }

            AfflictionLogic.Tick();

            Resources.Localization.LocalizationRefresh.FlushPendingRefresh();

            _gameplayUpdateTimer += Time.deltaTime;
            if (_gameplayUpdateTimer < 1f) return;

            float realTimeElapsed = _gameplayUpdateTimer;
            _gameplayUpdateTimer = 0f;

            TimeOfDay tod = GameManager.GetTimeOfDayComponent();
            if (tod == null) return;

            float gameHoursPassed = tod.GetTODHours(realTimeElapsed);
            if (gameHoursPassed <= 0f) return;

            AfflictionLogic.UpdateBlackLungSleepTracking(gameHoursPassed);
            AfflictionLogic.UpdatePredatorHostilityDecay(gameHoursPassed);
            AfflictionLogic.UpdateBlackLungExposure(gameHoursPassed);
            AfflictionLogic.UpdateCOExposure(gameHoursPassed);
            AfflictionLogic.UpdateCorpseExposure(gameHoursPassed);
            SevereSprainLogic.Update(gameHoursPassed);
        }
    }
}