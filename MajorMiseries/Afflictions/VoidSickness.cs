using static MajorMiseries.Afflictions.AuroraExposureRisk;
using AfflictionComponent.Components;
using AfflictionComponent.Enums;
using AfflictionComponent.Interfaces;
using MajorMiseries.Managers;
using MajorMiseries.Resources.Localization;
using System.Collections;

namespace MajorMiseries.Afflictions
{
    internal class VoidSickness
    {
        public class VoidSicknessAffliction : CustomAffliction, IRemedies, IInstance, ILocalizableAffliction
        {
            private const string NAME_KEY = "GAMEPLAY_VoidSicknessName";
            private const string CAUSE_KEY = "GAMEPLAY_VoidSicknessCause";
            private const string DESC_KEY = "GAMEPLAY_VoidSicknessDescription";
            private const string AURORA_DESC_KEY = "GAMEPLAY_VoidSicknessDescriptionAurora";

            private const string ICON = "MajorMiseries.Resources.Icons.Afflictions.Classic.VoidSickness.png";
            private const string ALT_ICON = "MajorMiseries.Resources.Icons.Afflictions.Alt.VoidSickness_ALT.png";

            private const float VOID_SICKNESS_THRESHOLD = 99f;
            private const uint CAVE_WATERFALL_AMBIENT_LOOP_EVENT_ID = 3864630299U; // PLAY_SNDAMBIENTCAVEWATERFALLLOOP1
            private const uint WATER_DRIP_AMBIENT_LOOP_EVENT_ID = 139846757U; // PLAY_WATERDRIPLOOP1

            public static bool IsActive { get; private set; } = false;

            private static uint s_CaveWaterfallAmbientLoopPlayingId = 0U;
            private static uint s_WaterDripAmbientLoopPlayingId = 0U;
            private static bool s_WaterAmbientLoopLoggedActive = false;
            private static bool s_WaterAmbientLoopsPaused = false;
            private static string s_WaterAmbientLoopsPausedSource = string.Empty;
            private static float s_LastCaveWaterfallAmbientLoopFailureRealtime = -999f;
            private static float s_LastWaterDripAmbientLoopFailureRealtime = -999f;

            private bool? m_LastAuroraDescriptionState = null;

            public InstanceType Type { get; set; } = InstanceType.Single;

            public Tuple<string, int, int>[] RemedyItems { get; set; } = [];
            public Tuple<string, int, int>[] AltRemedyItems { get; set; } = [];

            public bool InstantHeal { get; set; } = true;

            public VoidSicknessAffliction(AfflictionBodyArea bodyArea) : base(NAME_KEY, CAUSE_KEY, DESC_KEY, null, UnityEngine.Random.Range(0f, 100f) < Settings.options.AltAfflictionIconChance ? ALT_ICON : ICON, bodyArea, true)
            {
                RefreshLocalization();
            }

            public void OnFoundExistingInstance(CustomAffliction existingAffliction)
            {
                if (existingAffliction is VoidSicknessAffliction voidSickness) voidSickness.RefreshLocalization();
            }

            public void CureSymptoms()
            {
            }

            public void OnCure()
            {
                IsActive = false;
                StopWaterAmbientLoops("cure");
            }

            public override void OnUpdate()
            {
                IsActive = true;
                UpdateAmbientAudio();
                RefreshLocalizationIfNeeded();

                if (!Settings.options.EnableAuroraInfluence)
                {
                    Cure();
                    return;
                }

                Core.State ??= new Persistence.MMState();

                if (Core.State.AuroraInfluenceExposure >= VOID_SICKNESS_THRESHOLD) return;

                Core.Log($"VoidSickness receded -> exposure={Core.State.AuroraInfluenceExposure:0.##}/100, AuroraExposure risk will return.");
                Cure(false);

                if (Core.State.AuroraInfluenceExposure > 0f)
                {
                    MelonCoroutines.Start(StartAuroraExposureRiskNextFrame);
                }
            }

            internal static void UpdateAmbientAudio()
            {
                bool shouldPlay = Core.IsGameplayEnabled && Settings.options.EnableAuroraInfluence && AfflictionLogic.HasAffliction<VoidSicknessAffliction>();
                IsActive = shouldPlay;

                if (!shouldPlay)
                {
                    StopWaterAmbientLoops("void sickness inactive");
                    return;
                }

                if (ShouldPauseAmbientAudio(out string pauseSource))
                {
                    PauseWaterAmbientLoopsIfNeeded(pauseSource);
                    return;
                }

                ResumeWaterAmbientLoopsIfNeeded();
                StartWaterAmbientLoopsIfNeeded();
            }

            internal static void StopAmbientAudio()
            {
                StopWaterAmbientLoops("runtime reset");
            }

            internal static void RefreshAmbientAudioAfterSceneLoad()
            {
                StopWaterAmbientLoops("scene load");
                UpdateAmbientAudio();
            }

            private static void StartWaterAmbientLoopsIfNeeded()
            {
                GameObject? playerObject = GameManager.GetPlayerObject();
                if (playerObject == null) return;

                s_WaterAmbientLoopsPaused = false;
                s_WaterAmbientLoopsPausedSource = string.Empty;

                StartWaterAmbientLoopIfNeeded(ref s_CaveWaterfallAmbientLoopPlayingId, CAVE_WATERFALL_AMBIENT_LOOP_EVENT_ID, "PLAY_SNDAMBIENTCAVEWATERFALLLOOP1", playerObject, ref s_LastCaveWaterfallAmbientLoopFailureRealtime);
                StartWaterAmbientLoopIfNeeded(ref s_WaterDripAmbientLoopPlayingId, WATER_DRIP_AMBIENT_LOOP_EVENT_ID, "PLAY_WATERDRIPLOOP1", playerObject, ref s_LastWaterDripAmbientLoopFailureRealtime);

                if (s_WaterAmbientLoopLoggedActive) return;
                if (s_CaveWaterfallAmbientLoopPlayingId == 0U && s_WaterDripAmbientLoopPlayingId == 0U) return;

                s_WaterAmbientLoopLoggedActive = true;
                Core.Log("VoidSickness water ambience active -> PLAY_SNDAMBIENTCAVEWATERFALLLOOP1 + PLAY_WATERDRIPLOOP1.");
            }

            private static void StartWaterAmbientLoopIfNeeded(ref uint playingId, uint eventId, string eventName, GameObject playerObject, ref float lastFailureRealtime)
            {
                if (playingId != 0U) return;
                if (Time.unscaledTime - lastFailureRealtime < 5f) return;

                try
                {
                    playingId = AkSoundEngine.PostEvent(eventId, playerObject);

                    if (playingId == 0U)
                    {
                        lastFailureRealtime = Time.unscaledTime;
                        Core.Warn($"VoidSickness water ambience failed to start -> {eventName} returned playingId 0.");
                        return;
                    }

                    Core.Log($"VoidSickness water ambience layer started -> {eventName}.");
                }
                catch (Exception e)
                {
                    playingId = 0U;
                    lastFailureRealtime = Time.unscaledTime;
                    Core.Warn($"VoidSickness water ambience failed to start -> {eventName} | {e.GetType().Name}: {e.Message}");
                }
            }

            private static bool ShouldPauseAmbientAudio(out string source)
            {
                source = string.Empty;

                if (GameManager.m_Instance == null)
                {
                    source = "game manager missing";
                    return true;
                }

                if (GameManager.m_IsPaused)
                {
                    source = "game paused";
                    return true;
                }

                if (GameManager.s_IsGameplaySuspended || GameManager.s_IsAISuspended)
                {
                    source = "gameplay suspended/loading";
                    return true;
                }

                string sceneName = GameManager.m_ActiveScene ?? string.Empty;
                if (string.IsNullOrEmpty(sceneName) || !RegionalAfflictionManager.IsGameplayScene(sceneName))
                {
                    source = "non-gameplay/loading scene";
                    return true;
                }

                if (GameManager.GetPlayerObject() == null)
                {
                    source = "player object missing";
                    return true;
                }

                return false;
            }

            private static void PauseWaterAmbientLoopsIfNeeded(string source)
            {
                if (s_WaterAmbientLoopsPaused) return;
                if (s_CaveWaterfallAmbientLoopPlayingId == 0U && s_WaterDripAmbientLoopPlayingId == 0U) return;

                PauseOrResumeWaterAmbientLoop(s_CaveWaterfallAmbientLoopPlayingId, "PLAY_SNDAMBIENTCAVEWATERFALLLOOP1", true);
                PauseOrResumeWaterAmbientLoop(s_WaterDripAmbientLoopPlayingId, "PLAY_WATERDRIPLOOP1", true);

                s_WaterAmbientLoopsPaused = true;
                s_WaterAmbientLoopsPausedSource = source;
                Core.Log($"VoidSickness water ambience paused -> {source}.");
            }

            private static void ResumeWaterAmbientLoopsIfNeeded()
            {
                if (!s_WaterAmbientLoopsPaused) return;

                PauseOrResumeWaterAmbientLoop(s_CaveWaterfallAmbientLoopPlayingId, "PLAY_SNDAMBIENTCAVEWATERFALLLOOP1", false);
                PauseOrResumeWaterAmbientLoop(s_WaterDripAmbientLoopPlayingId, "PLAY_WATERDRIPLOOP1", false);

                Core.Log(string.IsNullOrEmpty(s_WaterAmbientLoopsPausedSource)
                    ? "VoidSickness water ambience resumed."
                    : $"VoidSickness water ambience resumed -> was paused by {s_WaterAmbientLoopsPausedSource}.");

                s_WaterAmbientLoopsPaused = false;
                s_WaterAmbientLoopsPausedSource = string.Empty;
            }

            private static void PauseOrResumeWaterAmbientLoop(uint playingId, string eventName, bool pause)
            {
                if (playingId == 0U) return;

                try
                {
                    AkSoundEngine.ExecuteActionOnPlayingID(pause ? AkActionOnEventType.AkActionOnEventType_Pause : AkActionOnEventType.AkActionOnEventType_Resume, playingId, 100, AkCurveInterpolation.AkCurveInterpolation_Linear);
                }
                catch (Exception e)
                {
                    Core.Warn($"VoidSickness water ambience failed to {(pause ? "pause" : "resume")} -> {eventName} | {e.GetType().Name}: {e.Message}");
                }
            }

            private static void StopWaterAmbientLoops(string source)
            {
                bool stoppedAny = false;

                stoppedAny |= StopWaterAmbientLoop(ref s_CaveWaterfallAmbientLoopPlayingId, "PLAY_SNDAMBIENTCAVEWATERFALLLOOP1");
                stoppedAny |= StopWaterAmbientLoop(ref s_WaterDripAmbientLoopPlayingId, "PLAY_WATERDRIPLOOP1");

                s_WaterAmbientLoopsPaused = false;
                s_WaterAmbientLoopsPausedSource = string.Empty;

                if (!s_WaterAmbientLoopLoggedActive && !stoppedAny) return;

                s_WaterAmbientLoopLoggedActive = false;
                Core.Log($"VoidSickness water ambience stopped -> {source}.");
            }

            private static bool StopWaterAmbientLoop(ref uint playingId, string eventName)
            {
                uint id = playingId;
                if (id == 0U) return false;

                playingId = 0U;

                try
                {
                    AkSoundEngine.StopPlayingID(id);
                }
                catch (Exception e)
                {
                    Core.Warn($"VoidSickness water ambience failed to stop -> {eventName} | {e.GetType().Name}: {e.Message}");
                }

                return true;
            }

            private void RefreshLocalizationIfNeeded()
            {
                bool useAuroraDescription = ShouldUseAuroraDescription();
                if (m_LastAuroraDescriptionState.HasValue && m_LastAuroraDescriptionState.Value == useAuroraDescription) return;

                RefreshLocalization();
            }

            private static bool ShouldUseAuroraDescription()
            {
                return AuroraInfluenceManager.IsAuroraActive();
            }

            private static IEnumerator StartAuroraExposureRiskNextFrame
            {
                get
                {
                    yield return null;

                    if (!Settings.options.EnableAuroraInfluence) yield break;
                    if (Core.State == null || Core.State.AuroraInfluenceExposure <= 0f || Core.State.AuroraInfluenceExposure >= VOID_SICKNESS_THRESHOLD) yield break;

                    new AuroraExposureRiskAffliction(AfflictionBodyArea.Head).Start();
                }
            }

            public void RefreshLocalization()
            {
                string oldName = m_Name;

                bool useAuroraDescription = ShouldUseAuroraDescription();

                m_Name = Localization.Get(NAME_KEY);
                m_CauseText = Localization.Get(CAUSE_KEY);
                m_Description = Localization.Get(useAuroraDescription ? AURORA_DESC_KEY : DESC_KEY);
                m_DescriptionNoHeal = null;
                m_LastAuroraDescriptionState = useAuroraDescription;

                Core.Log($"VoidSickness refresh -> '{oldName}' => '{m_Name}' | AuroraDescription:{useAuroraDescription}");
            }
        }
    }
}