using MajorMiseries.Persistence;
using static MajorMiseries.Afflictions.CorpseSicknessRisk;
using static MajorMiseries.Afflictions.CorpseSickness;

namespace MajorMiseries
{
    internal static partial class AfflictionLogic
    {
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
        private static readonly List<Container> s_HumanCorpseContainers = [];
        private static bool s_HumanCorpseSceneCacheBuilt = false;
        private static string s_HumanCorpseSceneCacheName = string.Empty;

        // Animal carcasses = scene-seeded cache + deferred reseed on death events
        private static readonly List<BodyHarvest> s_AnimalCarcasses = [];
        private static readonly HashSet<int> s_AnimalCarcassIds = [];
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
                GameObject? go = bodyHarvest?.gameObject;
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
                AfflictionSaveHelper.QueueSurvivalSave();
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
    }
}