using MajorMiseries.Persistence;
using static MajorMiseries.Afflictions.AuroraExposureRisk;
using static MajorMiseries.Afflictions.VoidSickness;
using System.Collections;
using UnityEngine.AI;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace MajorMiseries.Managers
{
    internal static class AuroraInfluenceManager
    {
        private const float EXPOSURE_MAX = 100f;
        private const float FIRST_SIGNS_THRESHOLD = 50f;
        private const float SLEEPWALKING_THRESHOLD = 75f;
        private const float WAKING_BLACKOUT_THRESHOLD = 99f;

        private const float MIN_SLEEP_HOURS_FOR_EVENTS = 4f;
        internal const int AURORA_INFLUENCE_HUD_DISPLAY_SECONDS = 6;

        private const float SAFE_SEARCH_PRIMARY_MIN_DISTANCE = 100f;
        private const float SAFE_SEARCH_PRIMARY_MAX_DISTANCE = 250f;
        private const float SAFE_SEARCH_FALLBACK_MIN_DISTANCE = 50f;
        private const float SAFE_SEARCH_FALLBACK_MAX_DISTANCE = 150f;
        private const float SAFE_SEARCH_LOCAL_MIN_DISTANCE = 20f;
        private const float SAFE_SEARCH_LOCAL_MAX_DISTANCE = 100f;

        private const float WAKING_BLACKOUT_TIME_ONLY_MIN_HOURS = 0.15f;
        private const float WAKING_BLACKOUT_TIME_ONLY_MAX_HOURS = 0.35f;
        private const float WAKING_BLACKOUT_DISTANCE_TIME_BASE_HOURS = 0.12f;
        private const float WAKING_BLACKOUT_HOURS_PER_PATH_METER = 0.008f;
        private const float WAKING_BLACKOUT_DISTANCE_TIME_RANDOM_MIN = 0.85f;
        private const float WAKING_BLACKOUT_DISTANCE_TIME_RANDOM_MAX = 1.15f;
        private const float WAKING_BLACKOUT_MIN_TIME_HOURS = 0.2f;
        private const float WAKING_BLACKOUT_MAX_TIME_HOURS = 3f;
        private const float WAKING_BLACKOUT_FATIGUE_LOSS_PER_HOUR = 10f;

        private const float DEBUG_ASTAR_PATH_DEFAULT_DURATION_SECONDS = 20f;
        private static GameObject? s_DebugAStarPathObject;
        private static float s_DebugAStarPathExpireRealtime = -1f;

        private static GameObject? s_BlackoutCanvas;
        private static Image? s_BlackoutImage;
        private static float s_BlackoutAlpha = 0f;
        private static object? s_BlackoutRoutine;
        private static bool s_BlackoutMovementLocked = false;
        private static bool s_SleepwalkingPendingTeleport = false;
        private static Vector3 s_PendingSleepwalkingWakePosition = Vector3.zero;
        private static float s_PendingSleepwalkingCameraPitch = 0f;
        private static float s_PendingSleepwalkingCameraYaw = 0f;
        private static string s_PendingSleepwalkingSource = string.Empty;
        private static string s_PendingSleepwalkingTargetScene = string.Empty;
        private static string s_PendingSleepwalkingWakePointSource = string.Empty;
        private static string s_PendingSleepwalkingLogicalRegion = string.Empty;

        private static bool s_WasSleeping = false;
        private static float s_SleepStartHoursPlayed = 0f;
        private static float s_SleepDurationHours = 0f;
        private static SleepEventKind s_CurrentSleepEvent = SleepEventKind.None;
        private static float s_ForcedWakeAfterHours = -1f;
        private static float s_SleepRecoveryMultiplier = 1f;
        private static bool s_SleepEventHudShown = false;
        private static float s_NightTerrorSleepBlockedUntilHours = -1f;
        private static bool s_NightTerrorSleepReadyHudShown = true;
        private static bool s_DebugForceNextSleepEvent = false;
        private static SleepEventKind s_DebugForcedNextSleepEvent = SleepEventKind.None;
        private static int s_LastExposureLogBucket = -1;
        private static bool s_AuroraExposureLogActive = false;
        private static string s_AuroraExposureLogSceneName = string.Empty;
        private static string s_AuroraExposureLogLogicalRegion = string.Empty;
        private static string s_AuroraExposureLogTier = string.Empty;
        private static string s_AuroraExposureLogContext = string.Empty;
        private static float s_AuroraExposureLogStartExposure = 0f;
        private static bool s_AuroraExposureLogIncreasing = false;
        private static string s_LastWakingBlackoutBlockReason = string.Empty;
        private static float s_WakingBlackoutBlockLogHours = 0f;
        private static readonly Dictionary<string, string> s_LastEffectDebugContextByName = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, float> s_LastEffectDebugRealtimeByName = new(StringComparer.OrdinalIgnoreCase);

        private enum SleepEventKind
        {
            None,
            RestlessSleep,
            NightTerror,
            LostTimeAfterSleep,
            Sleepwalking
        }

        private readonly struct SleepwalkingTarget
        {
            internal readonly string TargetScene;
            internal readonly string LogicalRegion;
            internal readonly Vector3 WakePosition;
            internal readonly bool NeedsSafeOffsetAfterLoad;
            internal readonly string WakePointSource;

            internal SleepwalkingTarget(string targetScene, string logicalRegion, Vector3 wakePosition, bool needsSafeOffsetAfterLoad, string wakePointSource)
            {
                TargetScene = targetScene;
                LogicalRegion = logicalRegion;
                WakePosition = wakePosition;
                NeedsSafeOffsetAfterLoad = needsSafeOffsetAfterLoad;
                WakePointSource = wakePointSource;
            }
        }

        private readonly struct AStarCoord : IEquatable<AStarCoord>
        {
            internal readonly int X;
            internal readonly int Z;
            internal readonly int Level;

            internal AStarCoord(int x, int z, int level)
            {
                X = x;
                Z = z;
                Level = level;
            }

            public bool Equals(AStarCoord other) => X == other.X && Z == other.Z && Level == other.Level;
            public override bool Equals(object? obj) => obj is AStarCoord other && Equals(other);
            public override int GetHashCode()
            {
                return HashCode.Combine(X, Z, Level);
            }
        }

        private readonly struct AStarGroundCandidate
        {
            internal readonly Vector3 Position;
            internal readonly Vector3 Normal;

            internal AStarGroundCandidate(Vector3 position, Vector3 normal)
            {
                Position = position;
                Normal = normal;
            }
        }

        private sealed class AStarNode
        {
            internal readonly AStarCoord Coord;
            internal readonly Vector3 Position;
            internal float G;
            internal float H;
            internal AStarNode? Parent;
            internal int HeapIndex;
            internal bool IsOpen;
            internal float F => G + H;

            internal AStarNode(AStarCoord coord, Vector3 position, float g, float h, AStarNode? parent = null)
            {
                Coord = coord;
                Position = position;
                G = g;
                H = h;
                Parent = parent;
                HeapIndex = -1;
                IsOpen = false;
            }
        }

        private sealed class AStarOpenHeap
        {
            private readonly List<AStarNode> _items = [];
            internal int Count => _items.Count;

            internal void Add(AStarNode item)
            {
                item.HeapIndex = _items.Count;
                item.IsOpen = true;
                _items.Add(item);
                SortUp(item);
            }

            internal AStarNode RemoveFirst()
            {
                AStarNode first = _items[0];
                int lastIndex = _items.Count - 1;
                AStarNode last = _items[lastIndex];
                _items.RemoveAt(lastIndex);

                first.IsOpen = false;
                first.HeapIndex = -1;

                if (_items.Count > 0)
                {
                    _items[0] = last;
                    last.HeapIndex = 0;
                    SortDown(last);
                }

                return first;
            }

            internal void UpdateItem(AStarNode item)
            {
                if (!item.IsOpen) return;
                SortUp(item);
            }

            private void SortDown(AStarNode item)
            {
                while (true)
                {
                    int leftChildIndex = item.HeapIndex * 2 + 1;
                    int rightChildIndex = item.HeapIndex * 2 + 2;
                    if (leftChildIndex < _items.Count)
                    {
                        int swapIndex = leftChildIndex;
                        if (rightChildIndex < _items.Count && HasHigherPriority(_items[rightChildIndex], _items[leftChildIndex]))
                        {
                            swapIndex = rightChildIndex;
                        }

                        if (HasHigherPriority(_items[swapIndex], item))
                        {
                            Swap(item, _items[swapIndex]);
                            continue;
                        }
                    }

                    return;
                }
            }

            private void SortUp(AStarNode item)
            {
                while (item.HeapIndex > 0)
                {
                    int parentIndex = (item.HeapIndex - 1) / 2;
                    AStarNode parent = _items[parentIndex];
                    if (!HasHigherPriority(item, parent)) return;

                    Swap(item, parent);
                }
            }

            private void Swap(AStarNode a, AStarNode b)
            {
                _items[a.HeapIndex] = b;
                _items[b.HeapIndex] = a;

                (b.HeapIndex, a.HeapIndex) = (a.HeapIndex, b.HeapIndex);
            }

            private static bool HasHigherPriority(AStarNode a, AStarNode b)
            {
                float fDelta = a.F - b.F;
                if (Mathf.Abs(fDelta) > 0.0001f) return fDelta < 0f;

                float hDelta = a.H - b.H;
                if (Mathf.Abs(hDelta) > 0.0001f) return hDelta < 0f;

                return a.G > b.G;
            }
        }

        private static readonly HashSet<string> AuroraShelteredScenes = new(StringComparer.OrdinalIgnoreCase)
        {
            "MiningRegionMine",
            "HubCave",
            "MountainPassCaveA",
            "MountainPassCaveB",
            "MountainCaveA",
            "MountainCaveB",
            "MountainTownCaveA",
            "MountainTownCaveB",
            "WhalingMine",
            "IceCaveA",
            "IceCaveB",
            "CaveB",
            "CaveC",
            "CaveD",
            "CanyonRoadCave",
            "CanneryMarshTransitionCave",
            "BlackrockCaveA",
            "BlackrockMineA",
            "BlackrockSteamTunnelsASurvival",
            "AshCaveA",
            "AshCaveB",
            "PrepperCacheA",
            "PrepperCacheAEmpty",
            "PrepperCacheB",
            "PrepperCacheBEmpty",
            "PrepperCacheBInterloper",
            "PrepperCacheC",
            "PrepperCacheCEmpty",
            "PrepperCacheD",
            "PrepperCacheDEmpty",
            "PrepperCacheE",
            "PrepperCacheEEmpty",
            "PrepperCacheEmpty",
            "PrepperCacheF",
            "PrepperCacheFEmpty",
            "HighwayMineTransitionZone",
            "DamCaveTransitionZone",
            "MineTransitionZone",
            "RiverValleyTransitionCave"
        };

        internal static void ResetRuntime()
        {
            VoidSicknessAffliction.StopAmbientAudio();

            s_WasSleeping = false;
            s_SleepStartHoursPlayed = 0f;
            s_SleepDurationHours = 0f;
            s_CurrentSleepEvent = SleepEventKind.None;
            s_ForcedWakeAfterHours = -1f;
            s_SleepRecoveryMultiplier = 1f;
            s_SleepEventHudShown = false;
            s_NightTerrorSleepBlockedUntilHours = -1f;
            s_NightTerrorSleepReadyHudShown = true;
            s_DebugForceNextSleepEvent = false;
            s_DebugForcedNextSleepEvent = SleepEventKind.None;
            s_BlackoutMovementLocked = false;
            s_BlackoutAlpha = 0f;
            if (s_BlackoutRoutine != null)
            {
                try { MelonCoroutines.Stop(s_BlackoutRoutine); }
                catch { }

                s_BlackoutRoutine = null;
            }

            ForceBlackoutClear("runtime reset");
            s_LastExposureLogBucket = -1;
            ResetAuroraExposureLogTracking();
            s_LastWakingBlackoutBlockReason = string.Empty;
            s_WakingBlackoutBlockLogHours = 0f;
            s_LastEffectDebugContextByName.Clear();
            s_LastEffectDebugRealtimeByName.Clear();
            ClearDebugAStarPath();
            ClearPendingSleepwalkingTeleport();
        }

        internal static float GetExposureMax() => EXPOSURE_MAX;

        internal static void DevSetExposure(float value)
        {
            Core.State ??= new MMState();

            Core.State.AuroraInfluenceExposure = Mathf.Clamp(value, 0f, EXPOSURE_MAX);
            Core.State.AuroraWakingBlackoutRollHours = 0f;
            s_LastExposureLogBucket = -1;
            ResetAuroraExposureLogTracking();
            Core.Instance?.MarkDirty();
            SyncAfflictionDisplayFromState();

            Core.Log($"[Aurora] set_AE -> exposure={Core.State.AuroraInfluenceExposure:0.##}/100", false);
        }

        internal static bool DevTriggerWakingBlackout(out string result)
        {
            Core.State ??= new MMState();

            LogWakingBlackoutDebug($"manual trigger requested | Exposure:{Core.State.AuroraInfluenceExposure:0.#}");

            if (!CanRunWakingBlackout(out string blockReason))
            {
                result = $"blocked: {blockReason}";
                LogWakingBlackoutDebug($"manual trigger blocked -> {blockReason} | Exposure:{Core.State.AuroraInfluenceExposure:0.#}");
                return false;
            }

            Core.State.AuroraWakingBlackoutRollHours = 0f;
            TriggerWakingBlackout("Manual");
            result = "triggered";
            return true;
        }

        internal static bool DevTriggerSleepwalking(out string result)
        {
            Core.State ??= new MMState();

            LogSleepwalkingDebug("manual trigger requested");

            if (!StartSleepwalking("Manual", true, out string startResult))
            {
                result = startResult;
                LogSleepwalkingDebug($"manual trigger blocked -> {startResult}");
                return false;
            }

            result = startResult;
            return true;
        }

        internal static bool DevForceNextSleepEvent(string eventName, out string result)
        {
            Core.State ??= new MMState();

            if (!TryParseDevSleepEvent(eventName, true, out SleepEventKind eventKind, out string parseResult))
            {
                result = parseResult;
                return false;
            }

            if (eventKind == SleepEventKind.None)
            {
                s_DebugForceNextSleepEvent = false;
                s_DebugForcedNextSleepEvent = SleepEventKind.None;
                result = "forced next sleep event cleared";
                LogSleepDebug("debug forced next sleep event cleared");
                return true;
            }

            s_DebugForceNextSleepEvent = true;
            s_DebugForcedNextSleepEvent = eventKind;
            result = $"next sleep event forced -> {eventKind}";
            LogSleepDebug(result);
            return true;
        }

        internal static bool DevTriggerSleepEvent(string eventName, out string result)
        {
            Core.State ??= new MMState();

            if (!TryParseDevSleepEvent(eventName, false, out SleepEventKind eventKind, out string parseResult))
            {
                result = parseResult;
                return false;
            }

            Rest rest = GameManager.GetRestComponent();
            if (rest != null && rest.IsSleeping())
            {
                s_CurrentSleepEvent = SleepEventKind.None;
                s_ForcedWakeAfterHours = -1f;
                s_SleepRecoveryMultiplier = 1f;
                rest.EndSleeping(true);
                LogSleepDebug($"manual sleep event ended active sleep first -> {eventKind}");
            }

            s_SleepEventHudShown = false;

            switch (eventKind)
            {
                case SleepEventKind.RestlessSleep:
                    ShowSleepEventHudMessage(SleepEventKind.RestlessSleep, "manual trigger");
                    result = "RestlessSleep triggered -> HUD message only; use force_AE_sleep_event restless to test recovery multiplier during real sleep";
                    return true;

                case SleepEventKind.NightTerror:
                    ShowSleepEventHudMessage(SleepEventKind.NightTerror, "manual trigger");
                    TimeOfDay tod = GameManager.GetTimeOfDayComponent();
                    if (tod != null) StartNightTerrorSleepBlock(tod.GetHoursPlayedNotPaused());
                    result = tod != null ? "NightTerror triggered -> sleep blocked for 30min" : "NightTerror triggered -> HUD shown, but sleep block skipped because TimeOfDay is missing";
                    return true;

                case SleepEventKind.LostTimeAfterSleep:
                    float lostHours = Random.Range(0.5f, 2f);
                    SkipTimeDry(lostHours, "ManualSleepLostTime");
                    ShowSleepEventHudMessage(SleepEventKind.LostTimeAfterSleep, "manual trigger");
                    result = $"LostTimeAfterSleep triggered -> skipped {lostHours:0.##}h";
                    return true;

                case SleepEventKind.Sleepwalking:
                    if (!StartSleepwalking("ManualSleepEvent", true, out string startResult))
                    {
                        result = startResult;
                        LogSleepwalkingDebug($"manual sleep event trigger blocked -> {startResult}");
                        return false;
                    }

                    result = $"Sleepwalking triggered -> {startResult}";
                    return true;

                default:
                    result = "unknown sleep event";
                    return false;
            }
        }

        private static bool TryParseDevSleepEvent(string eventName, bool allowNone, out SleepEventKind eventKind, out string result)
        {
            eventKind = SleepEventKind.None;

            if (string.IsNullOrWhiteSpace(eventName))
            {
                result = allowNone
                    ? "event required: restless, nightterror, losttime, sleepwalking, clear"
                    : "event required: restless, nightterror, losttime, sleepwalking";
                return false;
            }

            string normalized = eventName.Trim().Replace("_", string.Empty).Replace("-", string.Empty).Replace(" ", string.Empty).ToLowerInvariant();
            switch (normalized)
            {
                case "restless":
                case "restlesssleep":
                    eventKind = SleepEventKind.RestlessSleep;
                    result = string.Empty;
                    return true;

                case "nightterror":
                case "terror":
                    eventKind = SleepEventKind.NightTerror;
                    result = string.Empty;
                    return true;

                case "losttime":
                case "losttimeaftersleep":
                    eventKind = SleepEventKind.LostTimeAfterSleep;
                    result = string.Empty;
                    return true;

                case "sleepwalk":
                case "sleepwalking":
                    eventKind = SleepEventKind.Sleepwalking;
                    result = string.Empty;
                    return true;

                case "none":
                case "clear":
                case "reset":
                    if (allowNone)
                    {
                        eventKind = SleepEventKind.None;
                        result = string.Empty;
                        return true;
                    }
                    break;
            }

            result = allowNone
                ? "unknown event. Use: restless, nightterror, losttime, sleepwalking, clear"
                : "unknown event. Use: restless, nightterror, losttime, sleepwalking";
            return false;
        }

        internal static bool IsAuroraActive()
        {
            return GetCurrentWeatherStage() == WeatherStage.ClearAurora;
        }

        private static WeatherStage GetCurrentWeatherStage()
        {
            Weather weather = GameManager.GetWeatherComponent();
            if (weather == null) return WeatherStage.Undefined;

            try { return weather.GetWeatherStage(); }
            catch { return WeatherStage.Undefined; }
        }

        internal static bool IsAuroraShelteredScene(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) return false;
            if (sceneName.Equals("AFHangar", StringComparison.OrdinalIgnoreCase)) return IsPlayerInAFHangarShelterZone();

            return AuroraShelteredScenes.Contains(sceneName);
        }

        private static bool IsAuroraPartialShelterScene(string sceneName)
        {
            return !string.IsNullOrEmpty(sceneName) && sceneName.Equals("WhalingShipA", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsPlayerInAFHangarShelterZone()
        {
            try
            {
                Transform playerTransform = GameManager.GetPlayerTransform();
                return playerTransform != null && playerTransform.position.y <= 6f;
            }
            catch
            {
                return false;
            }
        }

        internal static void UpdateRealtime()
        {
            VoidSicknessAffliction.UpdateAmbientAudio();
            UpdateDebugAStarPathLifetime();
        }

        internal static void OnSceneWasInitialized(int buildIndex, string sceneName)
        {
            VoidSicknessAffliction.RefreshAmbientAudioAfterSceneLoad();
            ClearDebugAStarPath();

            if (!s_SleepwalkingPendingTeleport) return;
            if (string.IsNullOrEmpty(sceneName)) return;
            if (!RegionalAfflictionManager.IsGameplayScene(sceneName)) return;

            string targetScene = s_PendingSleepwalkingTargetScene;
            if (!string.Equals(sceneName, targetScene, StringComparison.OrdinalIgnoreCase))
            {
                LogSleepwalkingDebug($"arrival scene mismatch -> expected:{targetScene} actual:{sceneName} build:{buildIndex}");
                ClearPendingSleepwalkingTeleport();
                s_BlackoutRoutine = MelonCoroutines.Start(RecoverFromBlackoutRoutine("sleepwalking arrival scene mismatch"));
                return;
            }

            Vector3 wakePosition = s_PendingSleepwalkingWakePosition;
            float cameraPitch = s_PendingSleepwalkingCameraPitch;
            float cameraYaw = s_PendingSleepwalkingCameraYaw;
            string triggerSource = s_PendingSleepwalkingSource;
            string wakePointSource = s_PendingSleepwalkingWakePointSource;
            string logicalRegion = s_PendingSleepwalkingLogicalRegion;

            ClearPendingSleepwalkingTeleport();

            LogSleepwalkingDebug($"scene initialized -> snapping player | Scene:{sceneName} | LogicalRegion:{RegionalAfflictionManager.GetRegionLogName(logicalRegion)} | WakePoint:{wakePosition} | WakeSource:{wakePointSource} | CameraPitch:{cameraPitch:0.##} | CameraYaw:{cameraYaw:0.##}");
            s_BlackoutRoutine = MelonCoroutines.Start(CompleteSleepwalkingTeleport(GameManager.GetPlayerManagerComponent(), wakePosition, cameraPitch, cameraYaw, triggerSource, sceneName, logicalRegion, wakePointSource, 5f, 3f));
        }

        internal static void Update(float gameHoursPassed)
        {
            Core.State ??= new MMState();

            if (!Settings.options.EnableAuroraInfluence)
            {
                SyncAfflictionDisplayFromState();
                return;
            }

            if (gameHoursPassed <= 0f)
            {
                UpdateSleepState();
                return;
            }

            string sceneName = GameManager.m_ActiveScene ?? string.Empty;
            UpdateExposure(gameHoursPassed, sceneName);
            SyncAfflictionDisplayFromState();
            UpdateSleepState();
            UpdateWakingBlackout(gameHoursPassed);
        }

        private static void UpdateExposure(float gameHoursPassed, string sceneName)
        {
            float before = Core.State.AuroraInfluenceExposure;
            WeatherStage stage = GetCurrentWeatherStage();
            bool auroraActive = stage == WeatherStage.ClearAurora;
            bool sheltered = IsAuroraShelteredScene(sceneName);
            bool outdoor = IsOutdoorScene();
            bool sleeping = IsSleeping();
            bool inVehicle = IsPlayerInsideVehicle();
            bool partialShelter = IsAuroraPartialShelterScene(sceneName);

            string logicalRegion = GetCurrentLogicalRegionForAurora(sceneName);
            AuroraRegionExposureInfo regionExposure = AuroraRegionExposureSolver.Resolve(logicalRegion);
            float appliedRate = 0f;
            float vitaminCAmount = 500f;
            float vitaminCMultiplier = 1f;
            float partialProtectionMultiplier = inVehicle || partialShelter ? Mathf.Clamp01(Settings.options.AuroraInfluenceVehicleMultiplier) : 1f;

            if (auroraActive && !sheltered)
            {
                float baseRate = outdoor ? Settings.options.AuroraInfluenceOutdoorExposurePerHour : Settings.options.AuroraInfluenceIndoorExposurePerHour;
                vitaminCMultiplier = GetVitaminCExposureMultiplier(out vitaminCAmount);
                float modifiedAwakeRate = baseRate * regionExposure.Multiplier * vitaminCMultiplier * partialProtectionMultiplier;
                appliedRate = sleeping ? modifiedAwakeRate * 1.5f : modifiedAwakeRate;
                Core.State.AuroraInfluenceExposure = Mathf.Clamp(Core.State.AuroraInfluenceExposure + appliedRate * gameHoursPassed, 0f, EXPOSURE_MAX);
            }
            else if (Core.State.AuroraInfluenceExposure > 0f)
            {
                appliedRate = -1f / 24f;
                Core.State.AuroraInfluenceExposure = Mathf.Clamp(Core.State.AuroraInfluenceExposure + appliedRate * gameHoursPassed, 0f, EXPOSURE_MAX);
            }

            UpdateAuroraExposureLogTracking(auroraActive, stage, outdoor, sheltered, sleeping, inVehicle, partialShelter, appliedRate, sceneName, logicalRegion, regionExposure, before);

            if (!Mathf.Approximately(before, Core.State.AuroraInfluenceExposure))
            {
                Core.Instance?.MarkDirty();
                LogExposureBucketIfNeeded(auroraActive, sheltered, inVehicle, partialShelter, vitaminCAmount, vitaminCMultiplier, partialProtectionMultiplier, sceneName, logicalRegion, regionExposure);
            }
        }

        private static bool IsOutdoorScene()
        {
            Weather weather = GameManager.GetWeatherComponent();
            if (weather == null) return true;

            try { return !weather.IsIndoorEnvironment(); }
            catch { return true; }
        }

        private static bool IsSleeping()
        {
            Rest rest = GameManager.GetRestComponent();
            return rest != null && rest.IsSleeping();
        }

        private static bool IsPlayerInsideVehicle()
        {
            try
            {
                var piv = GameManager.GetPlayerInVehicle();
                return piv != null && piv.IsInside();
            }
            catch
            {
                return false;
            }
        }

        private static float GetVitaminCExposureMultiplier(out float vitaminCAmount)
        {
            if (!RequiemStagesEffects.TryGetCurrentVitaminCAmount(out vitaminCAmount)) return 1f;

            float maxMultiplier = Mathf.Max(1f, Settings.options.AuroraInfluenceVitaminCMaxMultiplier);
            float normalizedVitaminC = Mathf.Clamp01(vitaminCAmount / 500f);
            return Mathf.Lerp(maxMultiplier, 1f, normalizedVitaminC);
        }

        private static string GetCurrentLogicalRegionForAurora(string sceneName)
        {
            Core.State ??= new MMState();

            string fallbackRegion = Core.State.LastKnownLogicalRegion ?? string.Empty;
            string resolvedRegion = RegionalAfflictionManager.ResolveLogicalRegionForScene(sceneName, fallbackRegion);
            if (!string.IsNullOrEmpty(resolvedRegion)) return resolvedRegion;

            return Core.State.CurrentLogicalRegion ?? string.Empty;
        }

        private static void UpdateAuroraExposureLogTracking(bool auroraActive, WeatherStage stage, bool outdoor, bool sheltered, bool sleeping, bool inVehicle, bool partialShelter, float appliedRate, string sceneName, string logicalRegion, AuroraRegionExposureInfo regionExposure, float exposureBeforeUpdate)
        {
            if (!Settings.options.IsLogging)
            {
                ResetAuroraExposureLogTracking();
                return;
            }

            bool increasing = appliedRate > 0.0001f;
            bool decreasing = appliedRate < -0.0001f && exposureBeforeUpdate > 0f;

            if (!increasing && !decreasing)
            {
                EndAuroraExposureLogTracking(string.Empty, false, false);
                return;
            }

            string logicalRegionName = RegionalAfflictionManager.GetRegionLogName(logicalRegion);
            string context = $"{sceneName}|{logicalRegionName}|{regionExposure.Tier}|{increasing}|{auroraActive}|{stage}|{outdoor}|{sheltered}|{sleeping}|{inVehicle}|{partialShelter}";

            if (!s_AuroraExposureLogActive)
            {
                BeginAuroraExposureLogTracking(sceneName, logicalRegionName, regionExposure, context, increasing, auroraActive, stage, outdoor, sheltered, sleeping, inVehicle, partialShelter, appliedRate);
                return;
            }

            if (!string.Equals(context, s_AuroraExposureLogContext, StringComparison.Ordinal))
            {
                EndAuroraExposureLogTracking(sceneName, true, increasing);
                BeginAuroraExposureLogTracking(sceneName, logicalRegionName, regionExposure, context, increasing, auroraActive, stage, outdoor, sheltered, sleeping, inVehicle, partialShelter, appliedRate);
            }
        }

        private static void BeginAuroraExposureLogTracking(string sceneName, string logicalRegionName, AuroraRegionExposureInfo regionExposure, string context, bool increasing, bool auroraActive, WeatherStage stage, bool outdoor, bool sheltered, bool sleeping, bool inVehicle, bool partialShelter, float appliedRate)
        {
            s_AuroraExposureLogActive = true;
            s_AuroraExposureLogSceneName = sceneName;
            s_AuroraExposureLogLogicalRegion = logicalRegionName;
            s_AuroraExposureLogTier = regionExposure.Tier;
            s_AuroraExposureLogContext = context;
            s_AuroraExposureLogStartExposure = Core.State.AuroraInfluenceExposure;
            s_AuroraExposureLogIncreasing = increasing;

            string direction = increasing ? "increasing" : "decreasing";
            Core.Log($"Aurora exposure phase entered: '{sceneName}' ({logicalRegionName}, {regionExposure.Tier}) -> exposure will start {direction}. Rate:{appliedRate:0.###}/h | Aurora:{auroraActive} ({stage}) | Outdoor:{outdoor} | Sheltered:{sheltered} | Sleeping:{sleeping} | InVehicle:{inVehicle} | PartialShelter:{partialShelter} | Exposure:{Core.State.AuroraInfluenceExposure:0.###}/100.");
        }

        private static void EndAuroraExposureLogTracking(string nextSceneName, bool nextPhaseWillContinue, bool nextPhaseIncreasing)
        {
            if (!s_AuroraExposureLogActive) return;

            float totalExposure = Core.State.AuroraInfluenceExposure;
            float phaseChange = totalExposure - s_AuroraExposureLogStartExposure;

            string message = $"Aurora exposure phase exited: '{s_AuroraExposureLogSceneName}' ({s_AuroraExposureLogLogicalRegion}, {s_AuroraExposureLogTier}) -> total exposure {totalExposure:0.###}, phase change {phaseChange:+0.###;-0.###;0}.";

            if (nextPhaseWillContinue && !string.IsNullOrEmpty(nextSceneName))
            {
                string nextDirection = nextPhaseIncreasing ? "increasing" : "decreasing";
                message += $" Exposure will continue {nextDirection} in scene '{nextSceneName}'.";
            }
            else
            {
                message += s_AuroraExposureLogIncreasing ? " Exposure stops increasing." : " Exposure stops decreasing.";
            }

            Core.Log(message);
            ResetAuroraExposureLogTracking();
        }

        private static void ResetAuroraExposureLogTracking()
        {
            s_AuroraExposureLogActive = false;
            s_AuroraExposureLogSceneName = string.Empty;
            s_AuroraExposureLogLogicalRegion = string.Empty;
            s_AuroraExposureLogTier = string.Empty;
            s_AuroraExposureLogContext = string.Empty;
            s_AuroraExposureLogStartExposure = 0f;
            s_AuroraExposureLogIncreasing = false;
        }

        private static void LogSleepDebug(string message)
        {
            Core.Log($"[Aurora][Sleep] {message}");
        }

        private static void LogSleepwalkingDebug(string message)
        {
            Core.Log($"[Aurora][Sleepwalking] {message}");
        }

        private static void LogWakingBlackoutDebug(string message)
        {
            Core.Log($"[Aurora][WakingBlackout] {message}");
        }

        private static void LogBlackoutDebug(string message)
        {
            Core.Log($"[Aurora][Blackout] {message}");
        }

        internal static void LogEffectDebug(string effectName, string message)
        {
            if (!Settings.options.EnableAuroraInfluence || !Settings.options.IsLogging) return;
            if (Core.State.AuroraInfluenceExposure < FIRST_SIGNS_THRESHOLD) return;

            float actionTimeMultiplier = GetActionTimeMultiplier();
            float chancePenalty = GetChancePenaltyPercent();
            int exposureBucket = Mathf.FloorToInt(Core.State.AuroraInfluenceExposure / 5f) * 5;
            string context = $"{message}|ExposureBucket:{exposureBucket}|ActionTime:{actionTimeMultiplier:0.###}|ChancePenalty:{chancePenalty:0.##}";
            float now = Time.realtimeSinceStartup;

            if (s_LastEffectDebugContextByName.TryGetValue(effectName, out string? lastContext) &&
                s_LastEffectDebugRealtimeByName.TryGetValue(effectName, out float lastTime) &&
                string.Equals(lastContext, context, StringComparison.OrdinalIgnoreCase) &&
                now - lastTime < 10f)
            {
                return;
            }

            s_LastEffectDebugContextByName[effectName] = context;
            s_LastEffectDebugRealtimeByName[effectName] = now;

            Core.Log($"[Aurora][Effects][{effectName}] {message} | Exposure:{Core.State.AuroraInfluenceExposure:0.#}/100 | ActionTime:x{actionTimeMultiplier:0.###} | ChancePenalty:{chancePenalty:0.##}%");
        }

        internal static string DevLogCurrentContext()
        {
            Core.State ??= new MMState();

            string sceneName = GetCurrentSceneName();
            string lastOutdoorScene = GetLastOutdoorSceneName();
            string logicalRegion = GetCurrentLogicalRegionForAurora(sceneName);
            AuroraRegionExposureInfo regionExposure = AuroraRegionExposureSolver.Resolve(logicalRegion);
            WeatherStage stage = GetCurrentWeatherStage();
            bool auroraActive = stage == WeatherStage.ClearAurora;
            bool sheltered = IsAuroraShelteredScene(sceneName);
            bool outdoor = IsOutdoorScene();
            bool sleeping = IsSleeping();
            bool inVehicle = IsPlayerInsideVehicle();
            bool partialShelter = IsAuroraPartialShelterScene(sceneName);
            float vitaminCMultiplier = GetVitaminCExposureMultiplier(out float vitaminCAmount);
            float partialProtectionMultiplier = inVehicle || partialShelter ? Mathf.Clamp01(Settings.options.AuroraInfluenceVehicleMultiplier) : 1f;

            string context = $"Scene:{sceneName} | LastOutdoor:{lastOutdoorScene} | LogicalRegion:{RegionalAfflictionManager.GetRegionLogName(logicalRegion)} | Tier:{regionExposure.Tier} | Multiplier:x{regionExposure.Multiplier:0.###} | Aurora:{auroraActive} ({stage}) | Outdoor:{outdoor} | Sheltered:{sheltered} | Sleeping:{sleeping} | InVehicle:{inVehicle} | PartialShelter:{partialShelter} | VitaminC:{vitaminCAmount:0.###} | VitaminCMultiplier:x{vitaminCMultiplier:0.###} | PartialProtectionMultiplier:x{partialProtectionMultiplier:0.###} | Exposure:{Core.State.AuroraInfluenceExposure:0.##}/100";
            Core.Log($"[Aurora][Context] {context}", false);
            return context;
        }

        private static void LogExposureBucketIfNeeded(bool auroraActive, bool sheltered, bool inVehicle, bool partialShelter, float vitaminCAmount, float vitaminCMultiplier, float partialProtectionMultiplier, string sceneName, string logicalRegion, AuroraRegionExposureInfo regionExposure)
        {
            int bucket = Mathf.FloorToInt(Core.State.AuroraInfluenceExposure / 5f) * 5;
            if (bucket == s_LastExposureLogBucket) return;

            s_LastExposureLogBucket = bucket;
            Core.Log($"Aurora Influence -> {Core.State.AuroraInfluenceExposure:0.#}/100 | Aurora:{auroraActive} | Sheltered:{sheltered} | InVehicle:{inVehicle} | PartialShelter:{partialShelter} | VitaminC:{vitaminCAmount:0.###} | VitaminCMultiplier:x{vitaminCMultiplier:0.###} | PartialProtectionMultiplier:x{partialProtectionMultiplier:0.###} | Scene:{sceneName} | LogicalRegion:{RegionalAfflictionManager.GetRegionLogName(logicalRegion)} | Tier:{regionExposure.Tier} | RegionMultiplier:x{regionExposure.Multiplier:0.###}");
        }

        internal static void SyncAfflictionDisplayFromState()
        {
            Core.State ??= new MMState();

            if (!Core.IsGameplayEnabled || !Settings.options.EnableAuroraInfluence)
            {
                if (!Mathf.Approximately(Core.State.AuroraInfluenceExposure, 0f))
                {
                    Core.State.AuroraInfluenceExposure = 0f;
                    Core.State.AuroraWakingBlackoutRollHours = 0f;
                    Core.Instance?.MarkDirty();
                }

                AfflictionLogic.CureAllAfflictionsOfType<AuroraExposureRiskAffliction>();
                AfflictionLogic.CureAllAfflictionsOfType<VoidSicknessAffliction>();
                return;
            }

            float exposure = Mathf.Clamp(Core.State.AuroraInfluenceExposure, 0f, EXPOSURE_MAX);
            if (!Mathf.Approximately(exposure, Core.State.AuroraInfluenceExposure))
            {
                Core.State.AuroraInfluenceExposure = exposure;
                Core.Instance?.MarkDirty();
            }

            if (exposure >= WAKING_BLACKOUT_THRESHOLD)
            {
                AfflictionLogic.CureAllAfflictionsOfType<AuroraExposureRiskAffliction>();

                if (!AfflictionLogic.HasAffliction<VoidSicknessAffliction>())
                {
                    new VoidSicknessAffliction(AfflictionBodyArea.Head).Start();
                    Core.Log($"AuroraExposure evolved into VoidSickness -> exposure={exposure:0.##}/100.");
                }

                return;
            }

            if (AfflictionLogic.HasAffliction<VoidSicknessAffliction>())
            {
                AfflictionLogic.CureAllAfflictionsOfType<VoidSicknessAffliction>();
            }

            if (exposure > 0f)
            {
                if (!AfflictionLogic.HasAffliction<AuroraExposureRiskAffliction>())
                {
                    new AuroraExposureRiskAffliction(AfflictionBodyArea.Head).Start();
                    Core.Log($"AuroraExposure risk display applied -> exposure={exposure:0.##}/100.");
                }

                return;
            }

            AfflictionLogic.CureAllAfflictionsOfType<AuroraExposureRiskAffliction>();
            AfflictionLogic.CureAllAfflictionsOfType<VoidSicknessAffliction>();
        }

        private static void UpdateSleepState()
        {
            Rest rest = GameManager.GetRestComponent();
            TimeOfDay tod = GameManager.GetTimeOfDayComponent();
            if (rest == null || tod == null) return;

            UpdateNightTerrorSleepBlock(tod);

            bool sleeping = rest.IsSleeping();

            if (sleeping && !s_WasSleeping)
            {
                OnSleepStarted(rest, tod);
                sleeping = rest.IsSleeping();
            }
            else if (sleeping && s_WasSleeping)
            {
                MaybeForceWake(rest, tod);
                sleeping = rest.IsSleeping();
            }
            else if (!sleeping && s_WasSleeping)
            {
                OnSleepEnded(tod);
            }

            s_WasSleeping = sleeping;
        }

        private static void OnSleepStarted(Rest rest, TimeOfDay tod)
        {
            s_SleepStartHoursPlayed = tod.GetHoursPlayedNotPaused();
            s_SleepDurationHours = Mathf.Max(0f, rest.m_SleepDurationHours);

            bool debugForcedSleepEvent = TryConsumeDebugForcedSleepEvent(out SleepEventKind forcedSleepEvent);
            s_CurrentSleepEvent = debugForcedSleepEvent ? forcedSleepEvent : ChooseSleepEvent(s_SleepDurationHours);
            s_ForcedWakeAfterHours = -1f;
            s_SleepRecoveryMultiplier = 1f;
            s_SleepEventHudShown = false;

            switch (s_CurrentSleepEvent)
            {
                case SleepEventKind.RestlessSleep:
                    s_SleepRecoveryMultiplier = Mathf.Clamp(4f / Mathf.Max(1f, s_SleepDurationHours), 0.1f, 1f);
                    break;

                case SleepEventKind.NightTerror:
                    s_ForcedWakeAfterHours = debugForcedSleepEvent
                        ? Mathf.Clamp(Mathf.Min(0.25f, s_SleepDurationHours * 0.5f), 0.05f, Mathf.Max(0.05f, s_SleepDurationHours - 0.01f))
                        : Random.Range(1f, 3f);
                    s_SleepRecoveryMultiplier = 0.75f;
                    break;
            }

            if (debugForcedSleepEvent)
            {
                LogSleepDebug($"debug forced event consumed -> {s_CurrentSleepEvent} | Planned:{s_SleepDurationHours:0.##}h | ForcedWake:{s_ForcedWakeAfterHours:0.##}h");
            }

            if (s_CurrentSleepEvent != SleepEventKind.None)
            {
                LogSleepDebug($"event queued -> {s_CurrentSleepEvent} | Exposure:{Core.State.AuroraInfluenceExposure:0.#} | Planned:{s_SleepDurationHours:0.##}h | ForcedWake:{s_ForcedWakeAfterHours:0.##}h | Recovery x{s_SleepRecoveryMultiplier:0.##}");
            }
            else if (Core.State.AuroraInfluenceExposure >= FIRST_SIGNS_THRESHOLD && s_SleepDurationHours >= MIN_SLEEP_HOURS_FOR_EVENTS)
            {
                LogSleepDebug($"no event queued | Exposure:{Core.State.AuroraInfluenceExposure:0.#} | Planned:{s_SleepDurationHours:0.##}h");
            }
        }

        private static bool TryConsumeDebugForcedSleepEvent(out SleepEventKind eventKind)
        {
            eventKind = SleepEventKind.None;
            if (!s_DebugForceNextSleepEvent) return false;

            eventKind = s_DebugForcedNextSleepEvent;
            s_DebugForceNextSleepEvent = false;
            s_DebugForcedNextSleepEvent = SleepEventKind.None;
            return true;
        }

        private static SleepEventKind ChooseSleepEvent(float plannedSleepHours)
        {
            if (Core.State.AuroraInfluenceExposure < FIRST_SIGNS_THRESHOLD) return SleepEventKind.None;
            if (plannedSleepHours < MIN_SLEEP_HOURS_FOR_EVENTS)
            {
                LogSleepDebug($"event skipped -> planned sleep too short | Planned:{plannedSleepHours:0.##}h | Required:{MIN_SLEEP_HOURS_FOR_EVENTS:0.##}h | Exposure:{Core.State.AuroraInfluenceExposure:0.#}");
                return SleepEventKind.None;
            }

            float severity = Mathf.InverseLerp(FIRST_SIGNS_THRESHOLD, WAKING_BLACKOUT_THRESHOLD, Core.State.AuroraInfluenceExposure);
            float regularEventChance = Mathf.Lerp(5f, 30f, severity);
            float sleepwalkingChance = GetSleepwalkingEventChance(plannedSleepHours);
            float totalChance = Mathf.Clamp(regularEventChance + sleepwalkingChance, 0f, 100f);
            float chanceRoll = Random.Range(0f, 100f);

            if (chanceRoll > totalChance)
            {
                LogSleepDebug($"event roll failed -> Roll:{chanceRoll:0.##} > Chance:{totalChance:0.##}% | Regular:{regularEventChance:0.##}% | Sleepwalking:{sleepwalkingChance:0.##}% | Exposure:{Core.State.AuroraInfluenceExposure:0.#} | Planned:{plannedSleepHours:0.##}h");
                return SleepEventKind.None;
            }

            if (sleepwalkingChance > 0f && chanceRoll <= sleepwalkingChance)
            {
                LogSleepDebug($"event roll success -> Sleepwalking | Roll:{chanceRoll:0.##} <= SleepwalkingChance:{sleepwalkingChance:0.##}% | TotalChance:{totalChance:0.##}% | Exposure:{Core.State.AuroraInfluenceExposure:0.#} | Planned:{plannedSleepHours:0.##}h");
                return SleepEventKind.Sleepwalking;
            }

            int eventRoll = Random.Range(0, Core.State.AuroraInfluenceExposure >= SLEEPWALKING_THRESHOLD ? 3 : 2);
            SleepEventKind chosen = eventRoll switch
            {
                0 => SleepEventKind.RestlessSleep,
                1 => SleepEventKind.NightTerror,
                _ => SleepEventKind.LostTimeAfterSleep
            };

            LogSleepDebug($"event roll success -> {chosen} | Roll:{chanceRoll:0.##} <= Chance:{totalChance:0.##}% | Regular:{regularEventChance:0.##}% | Sleepwalking:{sleepwalkingChance:0.##}% | EventRoll:{eventRoll} | Exposure:{Core.State.AuroraInfluenceExposure:0.#} | Planned:{plannedSleepHours:0.##}h");
            return chosen;
        }

        private static float GetSleepwalkingEventChance(float plannedSleepHours)
        {
            if (Core.State.AuroraInfluenceExposure < SLEEPWALKING_THRESHOLD) return 0f;
            if (plannedSleepHours < 5f) return 0f;

            float minChance = Mathf.Clamp(Settings.options.AuroraSleepwalkingChanceMin, 0f, 100f);
            float maxChance = Mathf.Clamp(Settings.options.AuroraSleepwalkingChanceMax, minChance, 100f);
            return Mathf.Lerp(minChance, maxChance, Mathf.InverseLerp(SLEEPWALKING_THRESHOLD, EXPOSURE_MAX, Core.State.AuroraInfluenceExposure));
        }

        private static void MaybeForceWake(Rest rest, TimeOfDay tod)
        {
            if (s_ForcedWakeAfterHours <= 0f) return;

            float sleptHours = tod.GetHoursPlayedNotPaused() - s_SleepStartHoursPlayed;
            if (sleptHours < s_ForcedWakeAfterHours) return;

            s_ForcedWakeAfterHours = -1f;
            rest.EndSleeping(true);
            ShowSleepEventHudMessage(s_CurrentSleepEvent, "forced wake");

            if (s_CurrentSleepEvent == SleepEventKind.NightTerror)
            {
                StartNightTerrorSleepBlock(tod.GetHoursPlayedNotPaused());
            }

            LogSleepDebug($"forced wake -> {s_CurrentSleepEvent} after {sleptHours:0.##}h");
        }

        private static void OnSleepEnded(TimeOfDay tod)
        {
            float sleptHours = Mathf.Max(0f, tod.GetHoursPlayedNotPaused() - s_SleepStartHoursPlayed);
            LogSleepDebug($"ended -> Event:{s_CurrentSleepEvent} | Slept:{sleptHours:0.##}h | Planned:{s_SleepDurationHours:0.##}h | Exposure:{Core.State.AuroraInfluenceExposure:0.#} | Recovery x{s_SleepRecoveryMultiplier:0.##}");

            if (s_CurrentSleepEvent == SleepEventKind.RestlessSleep)
            {
                ShowSleepEventHudMessage(SleepEventKind.RestlessSleep, "sleep ended");
            }
            else if (s_CurrentSleepEvent == SleepEventKind.LostTimeAfterSleep)
            {
                float lostHours = Random.Range(0.5f, 2f);
                SkipTimeDry(lostHours, "SleepLostTime");
                ShowSleepEventHudMessage(SleepEventKind.LostTimeAfterSleep, "lost time applied");
                LogSleepDebug($"applied LostTimeAfterSleep -> TimeLost:{lostHours:0.##}h");
            }
            else if (s_CurrentSleepEvent == SleepEventKind.Sleepwalking)
            {
                if (!StartSleepwalking("SleepEvent", false, out string result))
                {
                    LogSleepwalkingDebug($"sleep event selected but sleepwalking did not start -> {result}");
                }
                else
                {
                    LogSleepwalkingDebug($"sleep event started -> {result}");
                }
            }

            s_CurrentSleepEvent = SleepEventKind.None;
            s_ForcedWakeAfterHours = -1f;
            s_SleepRecoveryMultiplier = 1f;
        }

        private static void StartNightTerrorSleepBlock(float currentHoursPlayed)
        {
            s_NightTerrorSleepBlockedUntilHours = currentHoursPlayed + 0.5f;
            s_NightTerrorSleepReadyHudShown = false;
            LogSleepDebug($"Night Terror sleep block started -> Until:{s_NightTerrorSleepBlockedUntilHours:0.##}h | Duration:30min");
        }

        internal static bool ShouldBlockSleepAfterNightTerror(out int minutesRemaining)
        {
            minutesRemaining = 0;
            if (!Core.IsGameplayEnabled) return false;

            TimeOfDay tod = GameManager.GetTimeOfDayComponent();
            if (tod == null) return false;

            UpdateNightTerrorSleepBlock(tod);
            return ShouldBlockSleepAfterNightTerror(tod, out minutesRemaining);
        }

        internal static void LogNightTerrorSleepBlocked(int minutesRemaining)
        {
            LogSleepDebug($"sleep start blocked by Night Terror cooldown -> Remaining:{minutesRemaining}min");
        }

        private static bool ShouldBlockSleepAfterNightTerror(TimeOfDay tod, out int minutesRemaining)
        {
            minutesRemaining = 0;
            if (s_NightTerrorSleepBlockedUntilHours <= 0f) return false;

            float remainingHours = s_NightTerrorSleepBlockedUntilHours - tod.GetHoursPlayedNotPaused();
            if (remainingHours <= 0f) return false;

            minutesRemaining = Mathf.Max(1, Mathf.CeilToInt(remainingHours * 60f));
            return true;
        }

        private static void UpdateNightTerrorSleepBlock(TimeOfDay tod)
        {
            if (s_NightTerrorSleepBlockedUntilHours <= 0f) return;
            if (tod.GetHoursPlayedNotPaused() < s_NightTerrorSleepBlockedUntilHours) return;

            s_NightTerrorSleepBlockedUntilHours = -1f;

            if (!s_NightTerrorSleepReadyHudShown)
            {
                HUDMessage.AddMessage(Localization.Get("GAMEPLAY_AuroraInfluenceNightTerrorSleepReady"), AURORA_INFLUENCE_HUD_DISPLAY_SECONDS, false);
                s_NightTerrorSleepReadyHudShown = true;
                LogSleepDebug("Night Terror sleep block ended");
            }
        }

        private static void ShowSleepEventHudMessage(SleepEventKind eventKind, string source)
        {
            if (s_SleepEventHudShown)
            {
                LogSleepDebug($"HUD message skipped -> already shown | Event:{eventKind} | Source:{source}");
                return;
            }

            string localizationKey = eventKind switch
            {
                SleepEventKind.RestlessSleep => "GAMEPLAY_AuroraInfluenceRestlessSleep",
                SleepEventKind.NightTerror => "GAMEPLAY_AuroraInfluenceNightTerror",
                SleepEventKind.LostTimeAfterSleep => "GAMEPLAY_AuroraInfluenceLostTime",
                _ => string.Empty
            };

            if (string.IsNullOrEmpty(localizationKey)) return;

            HUDMessage.AddMessage(Localization.Get(localizationKey), AURORA_INFLUENCE_HUD_DISPLAY_SECONDS, false);
            s_SleepEventHudShown = true;
            LogSleepDebug($"applied {eventKind} HUD message -> {source}");
        }

        internal static float GetSleepRecoveryMultiplier()
        {
            if (!Settings.options.EnableAuroraInfluence) return 1f;
            return Mathf.Max(0.05f, s_SleepRecoveryMultiplier);
        }

        internal static bool ShouldBlockBlackoutMovement()
        {
            return s_BlackoutMovementLocked || s_SleepwalkingPendingTeleport;
        }

        private static void SetBlackoutMovementLocked(bool locked, string reason)
        {
            if (s_BlackoutMovementLocked == locked) return;

            s_BlackoutMovementLocked = locked;
            LogBlackoutDebug($"movement {(locked ? "locked" : "unlocked")} -> {reason}");
        }

        internal static float GetFatigueDrainMultiplier()
        {
            if (!Settings.options.EnableAuroraInfluence) return 1f;
            if (Core.State.AuroraInfluenceExposure < FIRST_SIGNS_THRESHOLD) return 1f;

            return Mathf.Lerp(1f, 1.45f, Mathf.InverseLerp(FIRST_SIGNS_THRESHOLD, WAKING_BLACKOUT_THRESHOLD, Core.State.AuroraInfluenceExposure));
        }

        internal static float GetActionTimeMultiplier()
        {
            if (!Settings.options.EnableAuroraInfluence) return 1f;
            if (Core.State.AuroraInfluenceExposure < FIRST_SIGNS_THRESHOLD) return 1f;

            return Mathf.Lerp(1f, 1.35f, Mathf.InverseLerp(FIRST_SIGNS_THRESHOLD, WAKING_BLACKOUT_THRESHOLD, Core.State.AuroraInfluenceExposure));
        }

        internal static float GetResearchProgressMultiplier()
        {
            return 1f / GetActionTimeMultiplier();
        }

        internal static float GetChancePenaltyPercent()
        {
            if (!Settings.options.EnableAuroraInfluence) return 0f;
            if (Core.State.AuroraInfluenceExposure < FIRST_SIGNS_THRESHOLD) return 0f;

            return Mathf.Lerp(0f, 20f, Mathf.InverseLerp(FIRST_SIGNS_THRESHOLD, WAKING_BLACKOUT_THRESHOLD, Core.State.AuroraInfluenceExposure));
        }

        private static void UpdateWakingBlackout(float gameHoursPassed)
        {
            if (Core.State.AuroraInfluenceExposure < WAKING_BLACKOUT_THRESHOLD)
            {
                if (Core.State.AuroraWakingBlackoutRollHours > 0f)
                {
                    Core.State.AuroraWakingBlackoutRollHours = 0f;
                    Core.Instance?.MarkDirty();
                }

                return;
            }

            float rollIntervalHours = 10f / 60f;

            if (!CanRunWakingBlackout(out string blockReason))
            {
                s_WakingBlackoutBlockLogHours += gameHoursPassed;
                if (blockReason != s_LastWakingBlackoutBlockReason || s_WakingBlackoutBlockLogHours >= 2f)
                {
                    LogWakingBlackoutDebug($"blocked -> {blockReason} | Exposure:{Core.State.AuroraInfluenceExposure:0.#} | RollTimer:{Core.State.AuroraWakingBlackoutRollHours:0.##}/{rollIntervalHours:0.##}h");
                    s_LastWakingBlackoutBlockReason = blockReason;
                    s_WakingBlackoutBlockLogHours = 0f;
                }
                return;
            }

            s_LastWakingBlackoutBlockReason = string.Empty;
            s_WakingBlackoutBlockLogHours = 0f;
            Core.State.AuroraWakingBlackoutRollHours += gameHoursPassed;
            if (Core.State.AuroraWakingBlackoutRollHours < rollIntervalHours) return;

            Core.State.AuroraWakingBlackoutRollHours = 0f;
            Core.Instance?.MarkDirty();

            float maxChance = Mathf.Clamp(Settings.options.AuroraWakingBlackoutChance, 0f, 100f);
            float chance = Mathf.Lerp(maxChance * 0.5f, maxChance, Mathf.InverseLerp(WAKING_BLACKOUT_THRESHOLD, EXPOSURE_MAX, Core.State.AuroraInfluenceExposure));
            float roll = Random.Range(0f, 100f);
            LogWakingBlackoutDebug($"roll -> Roll:{roll:0.##} | Chance:{chance:0.##}% | Exposure:{Core.State.AuroraInfluenceExposure:0.#}");

            if (roll > chance)
            {
                LogWakingBlackoutDebug("roll failed");
                return;
            }

            TriggerWakingBlackout("Natural");
        }

        private static bool CanRunWakingBlackout(out string reason)
        {
            if (s_BlackoutMovementLocked || s_SleepwalkingPendingTeleport || s_BlackoutRoutine != null)
            {
                reason = "blackout already active";
                return false;
            }

            Rest rest = GameManager.GetRestComponent();
            if (rest != null && rest.IsSleeping())
            {
                reason = "player sleeping";
                return false;
            }

            PassTime passTime = GameManager.GetPassTime();
            if (passTime != null && passTime.IsPassingTime())
            {
                reason = "passing time";
                return false;
            }

            PlayerManager player = GameManager.GetPlayerManagerComponent();
            if (player == null)
            {
                reason = "no player manager";
                return false;
            }
            if (player.PlayerIsSprinting())
            {
                reason = "player sprinting";
                return false;
            }
            if (player.PlayerIsClimbing())
            {
                reason = "player climbing";
                return false;
            }

            PlayerStruggle struggle = GameManager.GetPlayerStruggleComponent();
            if (struggle != null && struggle.InStruggle())
            {
                reason = "player in struggle";
                return false;
            }

            if (IsPlayerBeingTrackedByPredator(out string predatorReason))
            {
                reason = predatorReason;
                return false;
            }

            if (GameManager.GetConditionComponent()?.m_CurrentHP <= 25f)
            {
                reason = "condition too low";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        private static bool StartSleepwalking(string triggerSource, bool ignoreSourceScene, out string result)
        {
            if (s_BlackoutRoutine != null)
            {
                result = "blackout routine already running";
                return false;
            }

            if (s_SleepwalkingPendingTeleport)
            {
                result = "sleepwalking teleport already pending";
                return false;
            }

            if (!TryResolveSleepwalkingTarget(ignoreSourceScene, out SleepwalkingTarget target, out string targetReason))
            {
                result = targetReason;
                return false;
            }

            PlayerManager player = GameManager.GetPlayerManagerComponent();
            Transform playerTransform = GameManager.GetPlayerTransform();
            if (player == null || playerTransform == null)
            {
                result = "missing player manager or transform";
                return false;
            }

            float cameraPitch = GetCurrentCameraPitch(playerTransform);
            float cameraYaw = GetCurrentCameraYaw(playerTransform);

            SetBlackoutMovementLocked(true, "sleepwalking start");
            SetBlackoutAlpha(0f);
            RefreshBlackoutOverlayTopmost();

            LogSleepwalkingDebug($"starting -> Source:{triggerSource} | CurrentScene:{GetCurrentSceneName()} | LastOutdoor:{GetLastOutdoorSceneName()} | TargetScene:{target.TargetScene} | LogicalRegion:{RegionalAfflictionManager.GetRegionLogName(target.LogicalRegion)} | WakeSource:{target.WakePointSource} | NeedsSafeOffset:{target.NeedsSafeOffsetAfterLoad} | WakePoint:{target.WakePosition} | CameraPitch:{cameraPitch:0.##} | CameraYaw:{cameraYaw:0.##}");
            s_BlackoutRoutine = MelonCoroutines.Start(SleepwalkingRoutine(player, target, cameraPitch, cameraYaw, triggerSource));

            result = $"triggered -> {target.TargetScene} {target.WakePosition} ({target.WakePointSource})";
            return true;
        }

        private static bool TryResolveSleepwalkingTarget(bool ignoreSourceScene, out SleepwalkingTarget target, out string reason)
        {
            target = default;
            reason = string.Empty;

            string currentScene = GetCurrentSceneName();
            string lastOutdoorScene = GetLastOutdoorSceneName();
            string logicalRegion = GetCurrentLogicalRegionForAurora(currentScene);
            string targetScene = GetSleepwalkingTargetScene(currentScene, logicalRegion);

            if (string.IsNullOrEmpty(targetScene))
            {
                reason = $"sleepwalking disabled here -> no manual wake points configured for current/logical region (current:{currentScene}, lastOutdoor:{lastOutdoorScene}, logicalRegion:{RegionalAfflictionManager.GetRegionLogName(logicalRegion)})";
                return false;
            }

            if (!ignoreSourceScene && !RegionalAfflictionManager.IsGameplayScene(targetScene))
            {
                reason = $"target scene is not gameplay (current:{currentScene}, lastOutdoor:{lastOutdoorScene}, target:{targetScene})";
                return false;
            }

            if (!AuroraSleepwalkingWakePoints.TrySelectWakePoint(targetScene, out Vector3 configuredWakePoint, out int configuredPointCount))
            {
                reason = $"sleepwalking disabled here -> manual wake table has no usable point for target scene {targetScene} (current:{currentScene}, lastOutdoor:{lastOutdoorScene}, logicalRegion:{RegionalAfflictionManager.GetRegionLogName(logicalRegion)})";
                return false;
            }

            target = new SleepwalkingTarget(targetScene, logicalRegion, configuredWakePoint, false, $"manual configured table ({configuredPointCount} points)");
            return true;
        }

        private static string GetSleepwalkingTargetScene(string currentScene, string logicalRegion)
        {
            if (!string.IsNullOrEmpty(currentScene) && AuroraSleepwalkingWakePoints.HasSceneEntry(currentScene)) return currentScene;

            string currentOutdoorRegion = RegionalAfflictionManager.ResolveOutdoorLogicalRegionForScene(currentScene);
            if (!string.IsNullOrEmpty(currentOutdoorRegion) && AuroraSleepwalkingWakePoints.TryGetDefaultOutdoorScene(currentOutdoorRegion, out string currentOutdoorDefaultScene)) return currentOutdoorDefaultScene;

            if (AuroraSleepwalkingWakePoints.TryGetDefaultOutdoorScene(logicalRegion, out string logicalDefaultScene)) return logicalDefaultScene;

            return string.Empty;
        }

        private static IEnumerator SleepwalkingRoutine(PlayerManager player, SleepwalkingTarget target, float cameraPitch, float cameraYaw, string triggerSource)
        {
            string currentScene = GetCurrentSceneName();
            bool needsSceneLoad = !string.Equals(currentScene, target.TargetScene, StringComparison.OrdinalIgnoreCase);

            if (needsSceneLoad)
            {
                SetBlackoutAlpha(1f);
                RefreshBlackoutOverlayTopmost();
                yield return WaitUnscaledSeconds(0.75f);
            }
            else
            {
                RefreshBlackoutOverlayTopmost();
                yield return FadeBlackout(0f, 1f, 0.75f);
                yield return WaitUnscaledSeconds(0.5f);
            }

            Vector3 finalWakePosition = target.WakePosition;
            string finalWakeSource = target.WakePointSource;

            if (target.NeedsSafeOffsetAfterLoad && TryFindSafeNearbyPosition(target.WakePosition, out Vector3 safeOffsetWakePosition, out _))
            {
                finalWakePosition = safeOffsetWakePosition;
                finalWakeSource += " -> safe offset accepted";
            }
            else if (target.NeedsSafeOffsetAfterLoad)
            {
                finalWakeSource += " -> safe offset failed, using fallback origin";
            }

            if (string.Equals(currentScene, target.TargetScene, StringComparison.OrdinalIgnoreCase))
            {
                LogSleepwalkingDebug($"same-scene arrival -> snapping player | Scene:{currentScene} | WakePoint:{finalWakePosition} | WakeSource:{finalWakeSource} | CameraPitch:{cameraPitch:0.##} | CameraYaw:{cameraYaw:0.##}");
                yield return CompleteSleepwalkingTeleport(player, finalWakePosition, cameraPitch, cameraYaw, triggerSource, target.TargetScene, target.LogicalRegion, finalWakeSource, 0.75f, 1.25f);
                yield break;
            }

            s_SleepwalkingPendingTeleport = true;
            s_PendingSleepwalkingWakePosition = finalWakePosition;
            s_PendingSleepwalkingCameraPitch = cameraPitch;
            s_PendingSleepwalkingCameraYaw = cameraYaw;
            s_PendingSleepwalkingSource = triggerSource;
            s_PendingSleepwalkingTargetScene = target.TargetScene;
            s_PendingSleepwalkingWakePointSource = finalWakeSource;
            s_PendingSleepwalkingLogicalRegion = target.LogicalRegion;

            LogSleepwalkingDebug($"starting scene load -> From:{currentScene} To:{target.TargetScene} Save:{GetCurrentSaveName()} WakePoint:{finalWakePosition} WakeSource:{finalWakeSource} CameraPitch:{cameraPitch:0.##} CameraYaw:{cameraYaw:0.##}");
            RefreshBlackoutOverlayTopmost();

            bool sceneLoadSetupFailed = false;

            try
            {
                CameraFade.FadeOut(
                    time: GameManager.m_SceneTransitionFadeOutTime,
                    onFadeFinished: (Action)(() => BeginSleepwalkingSceneLoad(finalWakePosition, target.TargetScene, currentScene))
                );
            }
            catch (Exception e)
            {
                ClearPendingSleepwalkingTeleport();
                LogSleepwalkingDebug($"scene fade/load setup failed -> {e.Message}");
                sceneLoadSetupFailed = true;
            }

            if (sceneLoadSetupFailed)
            {
                yield return RecoverFromBlackoutRoutine("sleepwalking scene load setup failed");
                yield break;
            }

            s_BlackoutRoutine = null;
        }

        private static void BeginSleepwalkingSceneLoad(Vector3 wakePosition, string targetScene, string currentScene)
        {
            try
            {
                PrepareSleepwalkingSceneTransitionData(wakePosition, targetScene, currentScene);
                GameManager.LoadScene(targetScene, GetCurrentSaveName());
            }
            catch (Exception e)
            {
                ClearPendingSleepwalkingTeleport();
                LogSleepwalkingDebug($"scene load failed -> {e.Message}");
                s_BlackoutRoutine = MelonCoroutines.Start(RecoverFromBlackoutRoutine("sleepwalking scene load failed"));
            }
        }

        private static IEnumerator RecoverFromBlackoutRoutine(string reason)
        {
            LogBlackoutDebug($"recovering -> {reason}");
            yield return FadeBlackout(1f, 0f, 2f);
            ForceBlackoutClear(reason);
            SetBlackoutMovementLocked(false, reason);
            s_BlackoutRoutine = null;
        }

        private static void ClearPendingSleepwalkingTeleport()
        {
            s_SleepwalkingPendingTeleport = false;
            s_PendingSleepwalkingWakePosition = Vector3.zero;
            s_PendingSleepwalkingCameraPitch = 0f;
            s_PendingSleepwalkingCameraYaw = 0f;
            s_PendingSleepwalkingSource = string.Empty;
            s_PendingSleepwalkingTargetScene = string.Empty;
            s_PendingSleepwalkingWakePointSource = string.Empty;
            s_PendingSleepwalkingLogicalRegion = string.Empty;
        }

        private static IEnumerator CompleteSleepwalkingTeleport(PlayerManager player, Vector3 wakePosition, float cameraPitch, float cameraYaw, string triggerSource, string targetScene, string logicalRegion, string wakePointSource, float holdSeconds, float fadeInSeconds)
        {
            PlayerManager currentPlayer = GameManager.GetPlayerManagerComponent();
            if (currentPlayer != null) player = currentPlayer;

            RefreshBlackoutOverlayTopmost();

            try
            {
                if (player == null && GameManager.GetPlayerObject() == null)
                {
                    LogSleepwalkingDebug("teleport skipped -> missing player object");
                }
                else
                {
                    bool snapped = SnapPlayerTo(wakePosition, cameraPitch, cameraYaw);
                    if (!snapped) LogSleepwalkingDebug("player snap failed -> continuing wakeup cleanup");

                    float lostHours = Random.Range(0.5f, 3f);
                    SkipTimeDry(lostHours, "Sleepwalking");

                    HUDMessage.AddMessage(Localization.Get("GAMEPLAY_AuroraInfluenceSleepwalking"), AURORA_INFLUENCE_HUD_DISPLAY_SECONDS, false);
                    Core.Log($"Aurora sleepwalking -> Source:{triggerSource} | Scene:{targetScene} | LogicalRegion:{RegionalAfflictionManager.GetRegionLogName(logicalRegion)} | WakePoint:{wakePosition} | WakeSource:{wakePointSource} | CameraPitch:{cameraPitch:0.##} | CameraYaw:{cameraYaw:0.##} | TimeLost:{lostHours:0.##}h | Exposure:{Core.State.AuroraInfluenceExposure:0.#}", false);
                    Core.Instance?.MarkDirty();
                }
            }
            catch (Exception e)
            {
                LogSleepwalkingDebug($"teleport apply failed -> fading back in anyway | {e.Message}");
            }

            yield return WaitUnscaledSeconds(holdSeconds);
            RefreshBlackoutOverlayTopmost();
            yield return FadeBlackout(1f, 0f, fadeInSeconds);

            ForceBlackoutClear("sleepwalking complete");
            SetBlackoutMovementLocked(false, "sleepwalking complete");
            s_BlackoutRoutine = null;
        }

        private static bool SnapPlayerTo(Vector3 position, float cameraPitch, float cameraYaw)
        {
            GameObject playerObject = GameManager.GetPlayerObject();
            if (playerObject == null) return false;

            CharacterController? playerController = null;
            vp_FPSCamera? camera = null;

            try { playerController = playerObject.GetComponent<CharacterController>(); }
            catch { }

            try { camera = GameManager.GetVpFPSCamera(); }
            catch { }

            if (playerController != null) playerController.enabled = false;
            try
            {
                playerObject.transform.position = position;
            }
            finally
            {
                if (playerController != null) playerController.enabled = true;
            }

            if (camera != null)
            {
                camera.m_Pitch = cameraPitch;
                camera.m_TargetPitch = cameraPitch;
                camera.m_CurrentPitch = cameraPitch;

                camera.m_Yaw = cameraYaw;
                camera.m_TargetYaw = cameraYaw;
                camera.m_CurrentYaw = cameraYaw;
            }

            return true;
        }

        private static float GetCurrentCameraPitch(Transform fallbackTransform)
        {
            try
            {
                vp_FPSCamera camera = GameManager.GetVpFPSCamera();
                if (camera != null) return camera.m_Pitch;
            }
            catch { }

            if (fallbackTransform == null) return 0f;

            float pitch = fallbackTransform.eulerAngles.x;
            if (pitch > 180f) pitch -= 360f;
            return pitch;
        }

        private static float GetCurrentCameraYaw(Transform fallbackTransform)
        {
            try
            {
                vp_FPSCamera camera = GameManager.GetVpFPSCamera();
                if (camera != null) return camera.m_Yaw;
            }
            catch { }

            return fallbackTransform != null ? fallbackTransform.eulerAngles.y : 0f;
        }

        private static string GetCurrentSceneName()
        {
            try
            {
                if (!string.IsNullOrEmpty(GameManager.m_ActiveScene)) return GameManager.m_ActiveScene;
            }
            catch { }

            try { return UnityEngine.SceneManagement.SceneManager.GetActiveScene().name ?? string.Empty; }
            catch { return string.Empty; }
        }

        private static string GetLastOutdoorSceneName()
        {
            try
            {
                var data = GameManager.m_SceneTransitionData;
                if (data != null && !string.IsNullOrEmpty(data.m_LastOutdoorScene)) return data.m_LastOutdoorScene;
            }
            catch { }

            return GetCurrentSceneName();
        }

        private static string GetCurrentSaveName()
        {
            try
            {
                MethodInfo? method = typeof(SaveGameSystem).GetMethod("GetCurrentSaveName", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                if (method != null && method.Invoke(null, null) is string saveName && !string.IsNullOrEmpty(saveName)) return saveName;
            }
            catch (Exception e)
            {
                LogSleepwalkingDebug($"current save name lookup failed -> {e.Message}");
            }

            return "autosave";
        }

        private static void PrepareSleepwalkingSceneTransitionData(Vector3 wakePosition, string targetScene, string currentScene)
        {
            try
            {
                SceneTransitionData? original = null;
                try { original = GameManager.m_SceneTransitionData; }
                catch { }

                GameManager.m_SceneTransitionData = new SceneTransitionData
                {
                    m_SceneSaveFilenameCurrent = currentScene,
                    m_SceneSaveFilenameNextLoad = targetScene,
                    m_ForceNextSceneLoadTriggerScene = original?.m_ForceNextSceneLoadTriggerScene,
                    m_SceneLocationLocIDOverride = original?.m_SceneLocationLocIDOverride,
                    m_GameRandomSeed = original != null ? original.m_GameRandomSeed : 0,
                    m_Location = original?.m_Location,
                    m_LastOutdoorScene = targetScene,
                    m_PosBeforeInteriorLoad = wakePosition,
                    m_TeleportPlayerSaveGamePosition = true
                };
            }
            catch (Exception e)
            {
                LogSleepwalkingDebug($"scene transition data setup failed -> {e.Message}");
            }
        }

        private static bool IsPlayerBeingTrackedByPredator(out string reason)
        {
            reason = string.Empty;

            try
            {
                if (BaseAiManager.m_BaseAis == null) return false;

                foreach (BaseAi animal in BaseAiManager.m_BaseAis)
                {
                    if (animal == null) continue;

                    switch (animal.m_AiSubType)
                    {
                        case AiSubType.Wolf:
                        case AiSubType.Bear:
                        case AiSubType.Moose:
                        case AiSubType.Cougar:
                            break;

                        default:
                            continue;
                    }

                    AiMode mode = animal.GetAiMode();
                    switch (mode)
                    {
                        case AiMode.Stalking:
                        case AiMode.Attack:
                        case AiMode.PassingAttack:
                        case AiMode.HoldGround:
                            reason = $"predator tracking player ({animal.m_AiSubType}, {mode})";
                            return true;
                    }
                }
            }
            catch (Exception e)
            {
                LogWakingBlackoutDebug($"predator tracking check failed -> {e.Message}");
            }

            return false;
        }

        private static void TriggerWakingBlackout(string triggerSource)
        {
            if (s_BlackoutRoutine != null)
            {
                LogWakingBlackoutDebug($"trigger blocked -> blackout routine already running | Source:{triggerSource}");
                return;
            }

            Transform playerTransform = GameManager.GetPlayerTransform();
            PlayerManager player = GameManager.GetPlayerManagerComponent();
            if (playerTransform == null || player == null)
            {
                LogWakingBlackoutDebug($"trigger aborted -> missing player transform or player manager | Source:{triggerSource}");
                return;
            }

            Vector3 origin = playerTransform.position;
            Quaternion rotation = playerTransform.rotation;
            LogWakingBlackoutDebug($"trigger -> Source:{triggerSource} | Origin:{origin} | SafeSearch:delayed-until-blackout | TimeLost:pending-distance | Exposure:{Core.State.AuroraInfluenceExposure:0.#}");

            SetBlackoutMovementLocked(true, "waking blackout start");
            s_BlackoutRoutine = MelonCoroutines.Start(WakingBlackoutRoutine(player, origin, rotation, triggerSource));
        }

        private static IEnumerator WakingBlackoutRoutine(PlayerManager player, Vector3 origin, Quaternion rotation, string triggerSource)
        {
            yield return FadeBlackout(0f, 1f, 2f);
            yield return WaitUnscaledSeconds(0.75f);

            Vector3 safePos = origin;
            bool moved = false;
            float pathDistanceMeters = 0f;

            try
            {
                if (IsOutdoorScene())
                {
                    LogWakingBlackoutDebug("safe position search started -> screen already black");
                    moved = TryFindSafeNearbyPosition(origin, out safePos, out pathDistanceMeters, true);
                    if (!moved)
                    {
                        safePos = origin;
                        pathDistanceMeters = 0f;
                    }
                }

                float lostHours = CalculateWakingBlackoutLostHours(pathDistanceMeters, moved);
                LogWakingBlackoutDebug($"safe position result -> Source:{triggerSource} | Origin:{origin} | SafeFound:{moved} | Target:{safePos} | PathDistance:{pathDistanceMeters:0.##}m | TimeLost:{lostHours:0.##}h | Exposure:{Core.State.AuroraInfluenceExposure:0.#}");

                SkipTimeDry(lostHours, "WakingBlackout");

                Fatigue fatigue = GameManager.GetFatigueComponent();
                if (fatigue != null)
                {
                    float fatigueLoss = CalculateWakingBlackoutFatigueLoss(lostHours);
                    float before = fatigue.m_CurrentFatigue;
                    fatigue.AddFatigue(fatigueLoss);
                    LogWakingBlackoutDebug($"fatigue loss applied -> {before:0.##}+{fatigueLoss:0.##}={fatigue.m_CurrentFatigue:0.##} | PathDistance:{pathDistanceMeters:0.##}m | TimeLost:{lostHours:0.##}h | Rate:{WAKING_BLACKOUT_FATIGUE_LOSS_PER_HOUR:0.#}/h");
                }
                else
                {
                    LogWakingBlackoutDebug("fatigue loss skipped -> missing fatigue component");
                }

                if (moved)
                {
                    player.TeleportPlayer(safePos, rotation);
                    LogWakingBlackoutDebug($"teleported -> {origin} => {safePos} | PathDistance:{pathDistanceMeters:0.##}m");
                }
                else
                {
                    LogWakingBlackoutDebug("no safe nearby position found -> time-only blackout");
                }

                HUDMessage.AddMessage(Localization.Get(moved ? "GAMEPLAY_AuroraInfluenceWakingBlackout" : "GAMEPLAY_AuroraInfluenceWakingBlackoutTimeOnly"), AURORA_INFLUENCE_HUD_DISPLAY_SECONDS, false);
                Core.Log($"Aurora waking blackout -> Source:{triggerSource} | PathDistance:{pathDistanceMeters:0.##}m | TimeLost:{lostHours:0.##}h | Moved:{moved} | Exposure:{Core.State.AuroraInfluenceExposure:0.#}", false);
                Core.Instance?.MarkDirty();
            }
            catch (Exception e)
            {
                LogWakingBlackoutDebug($"effect apply failed -> fading back in anyway | {e.Message}");
            }

            yield return WaitUnscaledSeconds(0.75f);
            yield return FadeBlackout(1f, 0f, 2f);
            ForceBlackoutClear("waking blackout complete");
            SetBlackoutMovementLocked(false, "waking blackout complete");
            s_BlackoutRoutine = null;
        }

        private static float CalculateWakingBlackoutLostHours(float pathDistanceMeters, bool moved)
        {
            if (!moved || pathDistanceMeters <= 0f)
            {
                return Random.Range(WAKING_BLACKOUT_TIME_ONLY_MIN_HOURS, WAKING_BLACKOUT_TIME_ONLY_MAX_HOURS);
            }

            float randomized = (WAKING_BLACKOUT_DISTANCE_TIME_BASE_HOURS + pathDistanceMeters * WAKING_BLACKOUT_HOURS_PER_PATH_METER)
                * Random.Range(WAKING_BLACKOUT_DISTANCE_TIME_RANDOM_MIN, WAKING_BLACKOUT_DISTANCE_TIME_RANDOM_MAX);

            return Mathf.Clamp(randomized, WAKING_BLACKOUT_MIN_TIME_HOURS, WAKING_BLACKOUT_MAX_TIME_HOURS);
        }

        private static float CalculateWakingBlackoutFatigueLoss(float lostHours)
        {
            return Mathf.Max(0f, lostHours) * WAKING_BLACKOUT_FATIGUE_LOSS_PER_HOUR;
        }

        private static void SkipTimeDry(float hours, string reason)
        {
            TimeOfDay tod = GameManager.GetTimeOfDayComponent();
            if (tod == null || hours <= 0f) return;

            float currentHours = tod.GetHoursPlayedNotPaused();
            float targetHours = currentHours + hours;
            float currentNormalized = tod.GetNormalizedTime();
            float targetNormalized = Mathf.Repeat(currentNormalized + hours / 24f, 1f);

            tod.SetHoursPlayedNotPaused(targetHours);
            tod.SetNormalizedTime(targetNormalized, true);
            tod.ForceUpdateTodObjects();

            Core.Log($"[Aurora][TimeSkip] {reason} -> Hours:{currentHours:0.##}+{hours:0.##}={targetHours:0.##} | TOD:{currentNormalized * 24f:0.##}h->{targetNormalized * 24f:0.##}h");
        }

        private static bool TryFindSafeNearbyPosition(Vector3 origin, out Vector3 safePosition, out float pathDistanceMeters, bool rejectNarrowElevatedOrigin = false)
        {
            safePosition = origin;
            pathDistanceMeters = 0f;

            if (!TrySnapSearchOrigin(origin, out Vector3 groundedOrigin, out float originSnapDelta))
            {
                groundedOrigin = origin;
                LogWakingBlackoutDebug($"safe position search aborted -> origin ground snap unsafe | Origin:{origin} | MaxUp:1.5m MaxDown:3.5m");
                if (rejectNarrowElevatedOrigin) return false;
            }

            if (rejectNarrowElevatedOrigin && IsNarrowElevatedOrigin(groundedOrigin, out int stableProbes, out int dropProbes, out float maxProbeDelta))
            {
                LogWakingBlackoutDebug($"safe position search aborted -> narrow/elevated origin detected | Origin:{origin} | Grounded:{groundedOrigin} | SnapDelta:{originSnapDelta:0.##}m | StableProbes:{stableProbes}/8 | DropProbes:{dropProbes}/8 | MaxProbeDelta:{maxProbeDelta:0.##}m");
                return false;
            }

            if (TryFindLogicalBlackoutPosition(groundedOrigin, SAFE_SEARCH_PRIMARY_MIN_DISTANCE, SAFE_SEARCH_PRIMARY_MAX_DISTANCE, 80, "primary", out safePosition, out pathDistanceMeters)) return true;
            if (TryFindLogicalBlackoutPosition(groundedOrigin, SAFE_SEARCH_FALLBACK_MIN_DISTANCE, SAFE_SEARCH_FALLBACK_MAX_DISTANCE, 64, "fallback", out safePosition, out pathDistanceMeters)) return true;
            if (TryFindLogicalBlackoutPosition(groundedOrigin, SAFE_SEARCH_LOCAL_MIN_DISTANCE, SAFE_SEARCH_LOCAL_MAX_DISTANCE, 84, "local", out safePosition, out pathDistanceMeters)) return true;

            safePosition = origin;
            pathDistanceMeters = 0f;
            LogWakingBlackoutDebug($"safe position search failed completely -> Primary:{SAFE_SEARCH_PRIMARY_MIN_DISTANCE:0.#}-{SAFE_SEARCH_PRIMARY_MAX_DISTANCE:0.#}m/80 attempts | Fallback:{SAFE_SEARCH_FALLBACK_MIN_DISTANCE:0.#}-{SAFE_SEARCH_FALLBACK_MAX_DISTANCE:0.#}m/64 attempts | Local:{SAFE_SEARCH_LOCAL_MIN_DISTANCE:0.#}-{SAFE_SEARCH_LOCAL_MAX_DISTANCE:0.#}m/84 attempts");
            return false;
        }

        private static bool TryFindLogicalBlackoutPosition(Vector3 origin, float minDistance, float maxDistance, int attempts, string passName, out Vector3 safePosition, out float pathDistanceMeters)
        {
            safePosition = origin;
            pathDistanceMeters = 0f;

            int groundFails = 0;
            int heightFails = 0;
            int slopeFails = 0;
            int clearanceFails = 0;
            int edgeFails = 0;
            int astarFails = 0;
            int astarAttempts = 0;
            int astarSkipped = 0;

            for (int i = 0; i < attempts; i++)
            {
                Vector2 dir = Random.insideUnitCircle;
                if (dir.sqrMagnitude < 0.001f) dir = Vector2.right;
                dir.Normalize();

                float distance = Random.Range(minDistance, maxDistance);
                Vector3 raw = origin + new Vector3(dir.x, 0f, dir.y) * distance;

                if (!TrySnapToGround(raw, out Vector3 grounded, out Vector3 normal)) { groundFails++; continue; }
                if (!IsGroundedPointSafe(origin, grounded, normal, true, out string pointFailReason))
                {
                    CountPointFailure(pointFailReason, ref heightFails, ref slopeFails, ref clearanceFails, ref edgeFails);
                    continue;
                }

                float astarCellSize = passName switch
                {
                    "primary" => 2f,
                    "local" => 1f,
                    _ => 1f
                };

                int astarMaxAttempts = passName switch
                {
                    "primary" => 22,
                    "local" => 32,
                    _ => 28
                };
                if (astarAttempts >= astarMaxAttempts)
                {
                    astarSkipped++;
                    continue;
                }

                astarAttempts++;
                bool astarPath = HasLocalAStarPath(origin, grounded, normal, astarCellSize, out int astarVisited, out float astarDistance, out List<Vector3> astarPoints);
                if (!astarPath)
                {
                    astarFails++;
                    continue;
                }

                bool navPath = HasNavMeshPath(origin, grounded);
                safePosition = grounded + Vector3.up * 0.05f;
                pathDistanceMeters = Mathf.Max(0f, astarDistance);
                ShowDebugAStarPathIfEnabled(astarPoints, passName, astarCellSize, navPath);
                LogWakingBlackoutDebug($"safe position accepted -> Pass:{passName} Attempt:{i + 1}/{attempts} | Mode:astar | Distance:{Vector3.Distance(origin, grounded):0.##}m | HeightDelta:{grounded.y - origin.y:0.##}m | Slope:{Vector3.Angle(normal, Vector3.up):0.#}° | AStarCell:{astarCellSize:0.##}m | AStarVisited:{astarVisited} | AStarDistance:{pathDistanceMeters:0.##}m | NavPath:{navPath}");
                return true;
            }

            LogWakingBlackoutDebug($"safe position pass failed -> Pass:{passName} Range:{minDistance:0.#}-{maxDistance:0.#}m Attempts:{attempts} | Ground:{groundFails} Height:{heightFails} Slope:{slopeFails} Clearance:{clearanceFails} Edge:{edgeFails} AStar:{astarFails}/{astarAttempts} Skipped:{astarSkipped}");
            return false;
        }

        private static void CountPointFailure(string reason, ref int heightFails, ref int slopeFails, ref int clearanceFails, ref int edgeFails)
        {
            switch (reason)
            {
                case "height": heightFails++; break;
                case "slope": slopeFails++; break;
                case "clearance": clearanceFails++; break;
                default: edgeFails++; break;
            }
        }

        private static bool TrySnapSearchOrigin(Vector3 origin, out Vector3 groundedOrigin, out float verticalDelta)
        {
            verticalDelta = 0f;

            if (!TrySnapToGround(origin, out groundedOrigin, out _)) return false;

            verticalDelta = groundedOrigin.y - origin.y;
            return verticalDelta <= 1.5f && verticalDelta >= -3.5f;
        }

        private static bool TrySnapToGround(Vector3 raw, out Vector3 grounded, out Vector3 normal)
        {
            grounded = raw;
            normal = Vector3.up;

            Vector3 start = raw + Vector3.up * 18f;
            RaycastHit[] hits = Physics.RaycastAll(start, Vector3.down, 42f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (hits == null || hits.Length == 0) return false;

            bool found = false;
            RaycastHit bestHit = default;
            float bestScore = float.MaxValue;

            foreach (RaycastHit hit in hits)
            {
                float verticalDelta = hit.point.y - raw.y;
                if (Mathf.Abs(verticalDelta) > 18f + 8f) continue;

                float score = Mathf.Abs(verticalDelta);
                if (score >= bestScore) continue;

                bestScore = score;
                bestHit = hit;
                found = true;
            }

            if (!found) return false;

            grounded = bestHit.point;
            normal = bestHit.normal;
            return true;
        }

        private static bool IsNarrowElevatedOrigin(Vector3 position, out int stableProbes, out int dropProbes, out float maxProbeDelta)
        {
            stableProbes = 0;
            dropProbes = 0;
            maxProbeDelta = 0f;

            Vector3[] offsets =
            [
                new(1f, 0f, 0f),
                new(-1f, 0f, 0f),
                new(0f, 0f, 1f),
                new(0f, 0f, -1f),
                new(0.7071f, 0f, 0.7071f),
                new(-0.7071f, 0f, 0.7071f),
                new(0.7071f, 0f, -0.7071f),
                new(-0.7071f, 0f, -0.7071f),
            ];

            foreach (Vector3 offset in offsets)
            {
                Vector3 raw = position + offset * 2.5f;
                if (!TrySnapToGround(raw, out Vector3 probe, out Vector3 probeNormal))
                {
                    dropProbes++;
                    continue;
                }

                float delta = probe.y - position.y;
                float absDelta = Mathf.Abs(delta);
                if (absDelta > maxProbeDelta) maxProbeDelta = absDelta;

                if (absDelta <= 2.25f && Vector3.Angle(probeNormal, Vector3.up) <= 40f + 5f)
                {
                    stableProbes++;
                    continue;
                }

                if (delta <= -4f || absDelta >= 4f) dropProbes++;
            }

            return dropProbes >= 5 && stableProbes <= 3;
        }

        private static bool IsGroundedPointSafe(Vector3 origin, Vector3 grounded, Vector3 normal, bool requireStableEdges, out string reason)
        {
            reason = string.Empty;

            if (Mathf.Abs(grounded.y - origin.y) > 18f)
            {
                reason = "height";
                return false;
            }

            if (Vector3.Angle(normal, Vector3.up) > 40f)
            {
                reason = "slope";
                return false;
            }

            if (!HasPlayerClearance(grounded))
            {
                reason = "clearance";
                return false;
            }

            if (requireStableEdges && !HasStableGroundAround(grounded))
            {
                reason = "edge";
                return false;
            }

            return true;
        }

        private static bool HasStableGroundAround(Vector3 position)
        {
            Vector3[] offsets =
            [
                new(0.75f, 0f, 0f),
                new(-0.75f, 0f, 0f),
                new(0f, 0f, 0.75f),
                new(0f, 0f, -0.75f)
            ];

            foreach (Vector3 offset in offsets)
            {
                if (!TrySnapToGround(position + offset, out Vector3 probe, out Vector3 probeNormal)) return false;
                if (Mathf.Abs(probe.y - position.y) > 1.25f) return false;
                if (Vector3.Angle(probeNormal, Vector3.up) > 40f + 5f) return false;
            }

            return true;
        }

        private static bool HasPlayerClearance(Vector3 position)
        {
            Vector3 bottom = position + Vector3.up * (0.35f + 0.1f);
            Vector3 top = position + Vector3.up * 1.65f;
            return !Physics.CheckCapsule(bottom, top, 0.35f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        }

        private static bool HasWalkableSegment(Vector3 from, Vector3 to)
        {
            Vector3 flatDelta = new(to.x - from.x, 0f, to.z - from.z);
            if (flatDelta.sqrMagnitude < 0.001f) return true;

            Vector3 direction = flatDelta.normalized;
            Vector3 start = from + direction * 0.3f + Vector3.up * 0.9f;
            Vector3 end = to - direction * 0.3f + Vector3.up * 0.9f;
            return !Physics.Linecast(start, end, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        }

        private static bool HasLocalAStarPath(Vector3 origin, Vector3 target, Vector3 targetNormal, float cellSize, out int visited, out float pathDistance, out List<Vector3> pathPoints)
        {
            visited = 0;
            pathDistance = 0f;
            pathPoints = [];

            float minX = Mathf.Min(origin.x, target.x) - 26f;
            float maxX = Mathf.Max(origin.x, target.x) + 26f;
            float minZ = Mathf.Min(origin.z, target.z) - 26f;
            float maxZ = Mathf.Max(origin.z, target.z) + 26f;

            AStarCoord startCoord = WorldToAStarCoord(origin, origin, cellSize);
            AStarCoord targetCoord = WorldToAStarCoord(target, origin, cellSize);

            Dictionary<AStarCoord, AStarNode> allNodes = [];
            AStarOpenHeap open = new();
            HashSet<AStarCoord> closed = [];

            AStarNode start = new(startCoord, origin, 0f, HorizontalDistance(origin, target) * 1.12f);
            allNodes[startCoord] = start;
            open.Add(start);

            while (open.Count > 0 && visited < 6500)
            {
                AStarNode current = open.RemoveFirst();

                if (!closed.Add(current.Coord)) continue;
                visited++;

                if (current.Coord.Equals(targetCoord))
                {
                    List<Vector3> rawPath = BuildAStarPath(current);
                    pathDistance = CalculatePathDistance(rawPath);
                    pathPoints = rawPath;
                    return true;
                }

                for (int x = -1; x <= 1; x++)
                {
                    for (int z = -1; z <= 1; z++)
                    {
                        if (x == 0 && z == 0) continue;

                        int neighborX = current.Coord.X + x;
                        int neighborZ = current.Coord.Z + z;
                        Vector3 neighborRaw = AStarCoordToWorld(neighborX, neighborZ, current.Position.y, origin, cellSize);
                        if (neighborRaw.x < minX || neighborRaw.x > maxX || neighborRaw.z < minZ || neighborRaw.z > maxZ) continue;

                        List<AStarGroundCandidate> neighborCandidates = GetAStarGroundCandidates(neighborRaw, current.Position, origin, target, targetNormal, targetCoord, cellSize);
                        for (int candidateIndex = 0; candidateIndex < neighborCandidates.Count; candidateIndex++)
                        {
                            Vector3 neighborPosition = neighborCandidates[candidateIndex].Position;
                            Vector3 neighborNormal = neighborCandidates[candidateIndex].Normal;
                            AStarCoord neighborCoord = WorldToAStarCoord(neighborPosition, origin, cellSize);
                            if (closed.Contains(neighborCoord)) continue;

                            if (Mathf.Abs(neighborPosition.y - current.Position.y) > 2.5f) continue;
                            if (!HasWalkableSegment(current.Position, neighborPosition)) continue;

                            float stepDistance = Vector3.Distance(current.Position, neighborPosition);
                            if (stepDistance <= 0.001f) continue;

                            float movementPenalty = GetAStarMovementPenalty(current.Position, neighborPosition, neighborNormal, origin);
                            float candidateG = current.G + stepDistance + movementPenalty;
                            if (allNodes.TryGetValue(neighborCoord, out AStarNode existing))
                            {
                                if (candidateG >= existing.G) continue;
                                existing.G = candidateG;
                                existing.Parent = current;
                                if (existing.IsOpen) open.UpdateItem(existing);
                                else open.Add(existing);
                            }
                            else
                            {
                                AStarNode neighbor = new(neighborCoord, neighborPosition, candidateG, HorizontalDistance(neighborPosition, target) * 1.12f, current);
                                allNodes[neighborCoord] = neighbor;
                                open.Add(neighbor);
                            }
                        }
                    }
                }
            }

            return false;
        }

        private static List<AStarGroundCandidate> GetAStarGroundCandidates(Vector3 raw, Vector3 currentPosition, Vector3 origin, Vector3 target, Vector3 targetNormal, AStarCoord targetCoord, float cellSize)
        {
            List<AStarGroundCandidate> candidates = [];

            AStarCoord rawCoordAtCurrentLevel = WorldToAStarCoord(new Vector3(raw.x, currentPosition.y, raw.z), origin, cellSize);
            if (rawCoordAtCurrentLevel.X == targetCoord.X && rawCoordAtCurrentLevel.Z == targetCoord.Z)
            {
                if (Mathf.Abs(target.y - currentPosition.y) <= 2.5f + 0.2f
                    && IsGroundedPointSafe(origin, target, targetNormal, false, out _))
                {
                    candidates.Add(new AStarGroundCandidate(target, targetNormal));
                }
            }

            Vector3 start = new(raw.x, currentPosition.y + 18f, raw.z);
            RaycastHit[] hits = Physics.RaycastAll(start, Vector3.down, 18f + 42f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (hits == null || hits.Length == 0) return candidates;

            Array.Sort(hits, (a, b) => Mathf.Abs(a.point.y - currentPosition.y).CompareTo(Mathf.Abs(b.point.y - currentPosition.y)));

            for (int i = 0; i < hits.Length && candidates.Count < 3; i++)
            {
                RaycastHit hit = hits[i];
                Vector3 position = hit.point;
                Vector3 normal = hit.normal;

                if (Mathf.Abs(position.y - currentPosition.y) > 2.5f + 0.2f) continue;
                if (!IsGroundedPointSafe(origin, position, normal, false, out _)) continue;

                bool duplicate = false;
                for (int j = 0; j < candidates.Count; j++)
                {
                    if (Vector3.Distance(candidates[j].Position, position) < 0.15f)
                    {
                        duplicate = true;
                        break;
                    }
                }

                if (!duplicate) candidates.Add(new AStarGroundCandidate(position, normal));
            }

            return candidates;
        }

        private static float GetAStarMovementPenalty(Vector3 from, Vector3 to, Vector3 toNormal, Vector3 origin)
        {
            float penalty = 0f;

            float slope = Vector3.Angle(toNormal, Vector3.up);
            if (slope > 8f)
            {
                penalty += (slope - 8f) * 0.035f;
            }

            float stepHeight = Mathf.Abs(to.y - from.y);
            if (stepHeight > 0.15f)
            {
                penalty += stepHeight * 0.75f;
            }

            float totalHeightDrift = Mathf.Abs(to.y - origin.y);
            if (totalHeightDrift > 6f)
            {
                penalty += (totalHeightDrift - 6f) * 0.2f;
            }

            return penalty;
        }

        private static List<Vector3> BuildAStarPath(AStarNode targetNode)
        {
            List<Vector3> points = [];
            AStarNode? current = targetNode;
            int guard = 0;

            while (current != null && guard < 6500 + 4)
            {
                points.Add(current.Position);
                current = current.Parent;
                guard++;
            }

            points.Reverse();
            return points;
        }

        private static float CalculatePathDistance(List<Vector3> points)
        {
            if (points == null || points.Count < 2) return 0f;

            float distance = 0f;
            for (int i = 1; i < points.Count; i++)
            {
                distance += Vector3.Distance(points[i - 1], points[i]);
            }

            return distance;
        }

        private static void ShowDebugAStarPathIfEnabled(List<Vector3> points, string passName, float cellSize, bool navPath)
        {
            if (!Settings.options.AuroraAStarDebugPathEnabled) return;
            if (points == null || points.Count < 2) return;

            float displaySeconds = Settings.options.AuroraAStarDebugPathDisplaySeconds > 0f
                ? Mathf.Clamp(Settings.options.AuroraAStarDebugPathDisplaySeconds, 2f, 120f)
                : DEBUG_ASTAR_PATH_DEFAULT_DURATION_SECONDS;

            ClearDebugAStarPath();

            try
            {
                s_DebugAStarPathObject = new GameObject($"MajorM_AE_AStarDebugPath_{passName}");
                LineRenderer line = s_DebugAStarPathObject.AddComponent<LineRenderer>();
                line.useWorldSpace = true;
                line.positionCount = points.Count;
                line.widthMultiplier = Mathf.Clamp(cellSize * 0.08f, 0.05f, 0.14f);
                line.numCornerVertices = 2;
                line.numCapVertices = 2;
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                line.receiveShadows = false;

                Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("GUI/Text Shader");
                if (shader != null) line.material = new Material(shader);

                Color pathColor = navPath ? new Color(0.1f, 0.85f, 1f, 0.95f) : new Color(1f, 0.65f, 0.05f, 0.95f);
                line.startColor = pathColor;
                line.endColor = pathColor;

                for (int i = 0; i < points.Count; i++)
                {
                    line.SetPosition(i, points[i] + Vector3.up * 0.35f);
                }

                AddDebugPathMarker(s_DebugAStarPathObject.transform, points[0], new Color(0.1f, 1f, 0.25f, 0.95f), "Origin");
                AddDebugPathMarker(s_DebugAStarPathObject.transform, points[^1], new Color(1f, 0.15f, 0.15f, 0.95f), "Target");

                s_DebugAStarPathExpireRealtime = Time.realtimeSinceStartup + displaySeconds;
                LogWakingBlackoutDebug($"debug path shown -> RawPoints:{points.Count} | Pass:{passName} | Cell:{cellSize:0.##}m | Duration:{displaySeconds:0.#}s | NavPath:{navPath}");
            }
            catch (Exception ex)
            {
                ClearDebugAStarPath();
                LogWakingBlackoutDebug($"debug path render failed -> {ex.GetType().Name}: {ex.Message}");
            }
        }

        private static void AddDebugPathMarker(Transform parent, Vector3 position, Color color, string name)
        {
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = $"MajorM_AE_AStarDebugPath_{name}";
            marker.transform.SetParent(parent, true);
            marker.transform.position = position + Vector3.up * (0.35f + 0.15f);
            marker.transform.localScale = Vector3.one * 0.35f;

            Collider collider = marker.GetComponent<Collider>();
            if (collider != null) UnityEngine.Object.Destroy(collider);

            Renderer renderer = marker.GetComponent<Renderer>();
            if (renderer == null) return;

            Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("GUI/Text Shader");
            if (shader == null) return;

            renderer.material = new Material(shader) { color = color };
        }

        private static void UpdateDebugAStarPathLifetime()
        {
            if (s_DebugAStarPathObject == null) return;
            if (s_DebugAStarPathExpireRealtime <= 0f) return;
            if (Time.realtimeSinceStartup < s_DebugAStarPathExpireRealtime) return;

            ClearDebugAStarPath();
        }

        internal static void ClearDebugAStarPath()
        {
            if (s_DebugAStarPathObject != null)
            {
                try { UnityEngine.Object.Destroy(s_DebugAStarPathObject); }
                catch { }
            }

            s_DebugAStarPathObject = null;
            s_DebugAStarPathExpireRealtime = -1f;
        }

        private static AStarCoord WorldToAStarCoord(Vector3 position, Vector3 origin, float cellSize)
        {
            return new AStarCoord(
                Mathf.RoundToInt((position.x - origin.x) / cellSize),
                Mathf.RoundToInt((position.z - origin.z) / cellSize),
                Mathf.RoundToInt((position.y - origin.y) / 1f));
        }

        private static Vector3 AStarCoordToWorld(int x, int z, float y, Vector3 origin, float cellSize)
        {
            return new Vector3(origin.x + x * cellSize, y, origin.z + z * cellSize);
        }

        private static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        private static IEnumerator WaitUnscaledSeconds(float seconds)
        {
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += Mathf.Max(Time.unscaledDeltaTime, 0.001f);
                yield return null;
            }
        }

        private static IEnumerator FadeBlackout(float from, float to, float seconds)
        {
            Image? image = GetBlackoutImage();
            if (image == null) yield break;

            float elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = seconds <= 0f ? 1f : Mathf.Clamp01(elapsed / seconds);
                SetBlackoutAlpha(Mathf.Lerp(from, to, t));
                yield return null;
            }

            SetBlackoutAlpha(to);
        }

        private static Image? GetBlackoutImage()
        {
            if (s_BlackoutImage != null) return s_BlackoutImage;

            try
            {
                s_BlackoutCanvas = new GameObject("MajorMiseries_AuroraBlackoutFade");
                UnityEngine.Object.DontDestroyOnLoad(s_BlackoutCanvas);

                Canvas canvas = s_BlackoutCanvas.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.overrideSorting = true;
                canvas.sortingOrder = int.MaxValue;

                CanvasScaler scaler = s_BlackoutCanvas.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);

                s_BlackoutCanvas.AddComponent<GraphicRaycaster>();

                GameObject imageObject = new("Blackout");
                imageObject.transform.SetParent(s_BlackoutCanvas.transform, false);

                s_BlackoutImage = imageObject.AddComponent<Image>();
                s_BlackoutImage.color = new Color(0f, 0f, 0f, 0f);
                s_BlackoutImage.raycastTarget = false;

                RectTransform rect = s_BlackoutImage.rectTransform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
            }
            catch (Exception e)
            {
                Core.Warn($"[Aurora][Blackout] fade overlay creation failed -> {e.Message}", false);
                s_BlackoutImage = null;
            }

            return s_BlackoutImage;
        }

        private static void RefreshBlackoutOverlayTopmost()
        {
            Image? image = GetBlackoutImage();
            if (image == null) return;

            try
            {
                SetBlackoutOverlayActive(true);

                if (s_BlackoutCanvas != null)
                {
                    Canvas canvas = s_BlackoutCanvas.GetComponent<Canvas>();
                    if (canvas != null)
                    {
                        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                        canvas.overrideSorting = true;
                        canvas.sortingOrder = int.MaxValue;
                    }

                    s_BlackoutCanvas.transform.SetAsLastSibling();
                }

                image.transform.SetAsLastSibling();
            }
            catch { }

            SetBlackoutAlpha(1f);
        }

        private static void SetBlackoutAlpha(float alpha)
        {
            float clampedAlpha = Mathf.Clamp01(alpha);
            s_BlackoutAlpha = clampedAlpha;

            Image? image = GetBlackoutImage();
            if (image == null) return;

            SetBlackoutOverlayActive(clampedAlpha > 0.001f);

            Color color = image.color;
            color.a = clampedAlpha;
            image.color = color;
        }

        private static void ForceBlackoutClear(string reason)
        {
            s_BlackoutAlpha = 0f;

            Image? image = s_BlackoutImage;
            if (image != null)
            {
                Color color = image.color;
                color.a = 0f;
                image.color = color;
            }

            SetBlackoutOverlayActive(false);
            LogBlackoutDebug($"cleared -> {reason}");
        }

        private static void SetBlackoutOverlayActive(bool active)
        {
            try
            {
                if (s_BlackoutCanvas != null && s_BlackoutCanvas.activeSelf != active) s_BlackoutCanvas.SetActive(active);
                if (s_BlackoutImage != null && s_BlackoutImage.gameObject.activeSelf != active) s_BlackoutImage.gameObject.SetActive(active);
            }
            catch { }
        }

        private static bool HasNavMeshPath(Vector3 origin, Vector3 target)
        {
            try
            {
                if (!NavMesh.SamplePosition(origin, out NavMeshHit navOrigin, 2f, NavMesh.AllAreas)) return false;
                if (!NavMesh.SamplePosition(target, out NavMeshHit navTarget, 2f, NavMesh.AllAreas)) return false;

                NavMeshPath path = new();
                if (!NavMesh.CalculatePath(navOrigin.position, navTarget.position, NavMesh.AllAreas, path)) return false;
                return path.status == NavMeshPathStatus.PathComplete;
            }
            catch (Exception e)
            {
                LogWakingBlackoutDebug($"navmesh path check failed -> {e.Message}");
                return false;
            }
        }
    }
}