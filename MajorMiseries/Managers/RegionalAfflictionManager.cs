using AfflictionComponent.Components;
using MajorMiseries.Afflictions;
using MajorMiseries.Persistence;
using MajorMiseries.Afflictions.Buffs;

namespace MajorMiseries.Managers
{
    internal static class RegionalAfflictionManager
    {
        private const float RECOVERY_MULTIPLIER = 4f;

        internal const float HOME_COMFORT_MOVEMENT_FATIGUE_MULTIPLIER = 0.95f;
        internal const float HOME_SICKNESS_MOVEMENT_FATIGUE_MULTIPLIER = 1.15f;
        internal const float REGIONAL_DISTRESS_MOVEMENT_FATIGUE_MULTIPLIER = 1.10f;

        internal const float HOME_COMFORT_SLEEP_RECOVERY_MULTIPLIER = 1.05f;
        internal const float HOME_SICKNESS_SLEEP_RECOVERY_MULTIPLIER = 0.90f;
        internal const float REGIONAL_DISTRESS_SLEEP_RECOVERY_MULTIPLIER = 0.95f;

        private static bool s_LoggedRegionalConflict = false;
        private static bool s_SettingsSyncPending = false;

        private static string s_LastLoggedHomeState = string.Empty;
        private static string s_LastLoggedRegionalDistressState = string.Empty;

        private static readonly string[] s_HomeRegionIds =
        [
            "",
            "LakeRegion",
            "CoastalRegion",
            "RuralRegion",
            "MountainTownRegion",
            "MarshRegion",
            "TracksRegion",
            "WhalingStationRegion",
            "CrashMountainRegion",
            "AshCanyonRegion",
            "RiverValleyRegion",
            "CanneryRegion",
            "BlackrockRegion",
            "AirfieldRegion",
            "MiningRegion",
            "MountainPassRegion",
            "HubRegion",

            // TLDev regions
            "ModForsakenShore",
            "ModMountainPass",
            "ModPrecariousCauseway",
            "ModRockyThoroughfare",
            "ModShatteredMarsh"
        ];

        private static readonly string[] s_RegionalDistressRegionIds =
        [
            "",
            "LakeRegion",
            "CoastalRegion",
            "RuralRegion",
            "MountainTownRegion",
            "MarshRegion",
            "TracksRegion",
            "WhalingStationRegion",
            "CrashMountainRegion",
            "AshCanyonRegion",
            "RiverValleyRegion",
            "CanneryRegion",
            "BlackrockRegion",
            "AirfieldRegion",
            "MiningRegion",
            "MountainPassRegion",

            // Keeper's Pass South
            "CanyonRoadTransitionZone",

            // Ravine
            "RavineTransitionZone",

            // Keeper's Pass North
            "BlackrockTransitionZone",

            // Winding River
            "DamRiverTransitionZone",

            // Other transition regions
            "HighwayTransitionZone",
            "HubRegion",
            "HubCaveTransitionZone",

            // TLDev regions
            "ModForsakenShore",
            "ModMountainPass",
            "ModPrecariousCauseway",
            "ModRockyThoroughfare",
            "ModShatteredMarsh"
        ];

        private static readonly Dictionary<string, string> s_OutdoorSceneToLogicalRegion = new(StringComparer.OrdinalIgnoreCase)
        {
            { "BlackrockPrisonSurvivalZone", "BlackrockRegion" },

            // Interior scenes that should resolve to their outdoor/logical region
            { "Dam", "LakeRegion" },

            // Transition scenes whose internal scene name does not end with "TransitionZone"
            { "DamRiverTransitionZoneB", "DamRiverTransitionZone" },
            { "MountainTownCaveB", "MountainTownCaveTransitionZone" },
            { "BlackrockCaveA", "BlackrockTransitionZone" },
            { "HubCave", "HubCaveTransitionZone" },

            // Keeper's Pass cave between South and North
            { "CanyonRoadCave", "CanyonRoadCave" },

            // TLDev regions
            { "ModForsakenShore", "ModForsakenShore" },
            { "ModMountainPass", "ModMountainPass" },
            { "ModPrecariousCauseway", "ModPrecariousCauseway" },
            { "ModRockyThoroughfare", "ModRockyThoroughfare" },
            { "ModShatteredMarsh", "ModShatteredMarsh" },

            // TLDev "indoor" scenes
            { "ModPrecariousCavern", "ModPrecariousCauseway" }
        };

        internal static string GetHomeRegionId(int choiceIndex)
        {
            if (choiceIndex < 0 || choiceIndex >= s_HomeRegionIds.Length) return string.Empty;

            return s_HomeRegionIds[choiceIndex];
        }

        internal static string GetRegionalDistressRegionId(int choiceIndex)
        {
            if (choiceIndex < 0 || choiceIndex >= s_RegionalDistressRegionIds.Length) return string.Empty;

            return s_RegionalDistressRegionIds[choiceIndex];
        }

        internal static int GetHomeRegionChoiceIndex(string? regionId)
        {
            if (string.IsNullOrEmpty(regionId)) return 0;

            for (int i = 0; i < s_HomeRegionIds.Length; i++)
            {
                if (string.Equals(s_HomeRegionIds[i], regionId, StringComparison.OrdinalIgnoreCase)) return i;
            }

            return 0;
        }

        internal static int GetRegionalDistressRegionChoiceIndex(string? regionId)
        {
            if (string.IsNullOrEmpty(regionId)) return 0;

            for (int i = 0; i < s_RegionalDistressRegionIds.Length; i++)
            {
                if (string.Equals(s_RegionalDistressRegionIds[i], regionId, StringComparison.OrdinalIgnoreCase)) return i;
            }

            return 0;
        }

        internal static void SyncSettingsDisplayFromState()
        {
            EnsureState();

            Settings.options.HomeRegion = GetHomeRegionChoiceIndex(Core.State.ConfiguredHomeRegion);
            Settings.options.RegionalDistressRegion = GetRegionalDistressRegionChoiceIndex(Core.State.ConfiguredRegionalDistressRegion);

            Settings.UpdateRegionalAfflictionVisibility();
            Settings.options.RefreshGUI();
        }

        internal static string GetRegionLogName(string? regionId)
        {
            return string.IsNullOrEmpty(regionId) ? "Unknown" : regionId;
        }

        internal static void ResetRuntime()
        {
            s_LoggedRegionalConflict = false;
            s_SettingsSyncPending = false;
            s_LastLoggedHomeState = string.Empty;
            s_LastLoggedRegionalDistressState = string.Empty;
        }

        internal static void RestoreFromState()
        {
            ResetRuntime();
            EnsureState();
            ClampState();
            SyncFromSettings(logSettingsChanges: false);
        }

        internal static void RequestSettingsSync()
        {
            s_SettingsSyncPending = true;
        }

        internal static void SyncFromSettings(bool logSettingsChanges = false, bool allowHomeRegionChange = false)
        {
            EnsureState();
            ClampState();

            s_SettingsSyncPending = false;

            string selectedHomeRegion = GetHomeRegionId(Settings.options.HomeRegion);
            string distressRegion = Settings.options.EnableRegionalAfflictions ? GetRegionalDistressRegionId(Settings.options.RegionalDistressRegion) : string.Empty;

            if (!Settings.options.EnableRegionalAfflictions)
            {
                bool hadTimers = Core.State.HomeSicknessHoursAway > 0f || Core.State.RegionalDistressHoursInRegion > 0f;

                Core.State.HomeSicknessHoursAway = 0f;
                Core.State.RegionalDistressHoursInRegion = 0f;

                bool cured = CureHomeComfort() | CureHomeSickness() | CureRegionalDistress();

                if (hadTimers || cured)
                {
                    Core.Instance?.MarkDirty();

                    if (logSettingsChanges) Core.Log("RegionalAfflictions -> disabled, timers and effects cleared. HomeRegion preserved.");
                }

                return;
            }

            if (allowHomeRegionChange)
            {
                TryApplyHomeRegionSelection(selectedHomeRegion, logSettingsChanges);
            }

            string effectiveHomeRegion = Core.State.ConfiguredHomeRegion ?? string.Empty;

            if (!string.Equals(Core.State.ConfiguredRegionalDistressRegion, distressRegion, StringComparison.OrdinalIgnoreCase))
            {
                Core.State.ConfiguredRegionalDistressRegion = distressRegion;
                Core.State.RegionalDistressHoursInRegion = 0f;

                CureRegionalDistress();

                Core.Instance?.MarkDirty();

                if (logSettingsChanges) Core.Log($"RegionalDistress region selected -> {GetRegionLogName(distressRegion)}. RegionalDistress timer reset.");
            }

            if (!string.IsNullOrEmpty(effectiveHomeRegion) && string.Equals(effectiveHomeRegion, distressRegion, StringComparison.OrdinalIgnoreCase))
            {
                if (!s_LoggedRegionalConflict)
                {
                    s_LoggedRegionalConflict = true;
                    Core.Log($"RegionalDistress ignored -> same region as HomeRegion ({GetRegionLogName(effectiveHomeRegion)}).");
                }

                bool timerHadValue = Core.State.RegionalDistressHoursInRegion > 0f;
                Core.State.RegionalDistressHoursInRegion = 0f;

                bool cured = CureRegionalDistress();

                if (timerHadValue || cured) Core.Instance?.MarkDirty();
            }
            else
            {
                s_LoggedRegionalConflict = false;
            }
        }

        private static void TryApplyHomeRegionSelection(string selectedHomeRegion, bool logSettingsChanges)
        {
            string currentHomeRegion = Core.State.ConfiguredHomeRegion ?? string.Empty;

            if (string.Equals(currentHomeRegion, selectedHomeRegion, StringComparison.OrdinalIgnoreCase)) return;

            if (Core.State.HomeRegionRelocationCooldownHoursRemaining > 0f)
            {
                string remaining = FormatHomeRegionRelocationCooldown(Core.State.HomeRegionRelocationCooldownHoursRemaining);

                ShowHomeRegionHudMessage("GAMEPLAY_HomeRegionMoveBlocked", remaining);

                if (logSettingsChanges)
                {
                    Core.Log($"HomeRegion relocation blocked -> Current:{GetRegionLogName(currentHomeRegion)} | Requested:{GetRegionLogName(selectedHomeRegion)} | Cooldown:{Core.State.HomeRegionRelocationCooldownHoursRemaining:0.#}h remaining.");
                }

                return;
            }

            Core.State.ConfiguredHomeRegion = selectedHomeRegion;
            Core.State.HomeSicknessHoursAway = 0f;
            Core.State.HomeRegionRelocationCooldownHoursRemaining = 30f * 24f;
            Core.State.HomeRegionRelocationReadyMessageShown = false;

            CureHomeComfort();
            CureHomeSickness();

            Core.Instance?.MarkDirty();

            if (string.IsNullOrEmpty(selectedHomeRegion))
            {
                ShowHomeRegionHudMessage("GAMEPLAY_HomeRegionCleared");

                if (logSettingsChanges) Core.Log($"HomeRegion cleared -> Previous:{GetRegionLogName(currentHomeRegion)} | Next move in 30 days.");
            }
            else
            {
                ShowHomeRegionHudMessage("GAMEPLAY_HomeRegionMoved", GetRegionLogName(selectedHomeRegion));

                if (logSettingsChanges) Core.Log($"HomeRegion selected -> Previous:{GetRegionLogName(currentHomeRegion)} | New:{GetRegionLogName(selectedHomeRegion)} | Next move in 30 days. HomeSickness timer reset.");
            }
        }

        private static void UpdateHomeRegionRelocationCooldown(float gameHoursPassed)
        {
            float oldCooldown = Core.State.HomeRegionRelocationCooldownHoursRemaining;

            if (oldCooldown <= 0f) return;

            Core.State.HomeRegionRelocationCooldownHoursRemaining = Mathf.Max(0f, oldCooldown - gameHoursPassed);
            Core.State.HomeRegionRelocationReadyMessageShown = false;

            Core.Instance?.MarkDirty();

            if (Core.State.HomeRegionRelocationCooldownHoursRemaining > 0f) return;

            ShowHomeRegionRelocationAvailableMessage();
        }

        private static void ShowHomeRegionRelocationAvailableMessage()
        {
            if (Core.State.HomeRegionRelocationReadyMessageShown) return;

            Core.State.HomeRegionRelocationReadyMessageShown = true;

            ShowHomeRegionHudMessage("GAMEPLAY_HomeRegionMoveAvailable");
            Core.Log("HomeRegion relocation available.");

            Core.Instance?.MarkDirty();
        }

        private static void ShowHomeRegionHudMessage(string localizationKey, params object[] args)
        {
            if (!IsGameplayScene(GameManager.m_ActiveScene)) return;

            string message = Localization.Get(localizationKey);

            if (args is { Length: > 0 })
            {
                message = string.Format(message, args);
            }

            HUDMessage.AddMessage(message, 4, false);
        }

        private static string FormatHomeRegionRelocationCooldown(float hoursRemaining)
        {
            int daysRemaining = Mathf.CeilToInt(hoursRemaining / 24f);

            return daysRemaining <= 1 ? "1 day" : $"{daysRemaining} days";
        }

        internal static void DevResetHomeRegionRelocationCooldown()
        {
            EnsureState();
            ClampState();

            Core.State.HomeRegionRelocationCooldownHoursRemaining = 0f;
            Core.State.HomeRegionRelocationReadyMessageShown = false;

            ShowHomeRegionRelocationAvailableMessage();

            Core.Instance?.MarkDirty();

            Core.Log("HomeRegion relocation cooldown override by dev command... You bad kitty...", false);
        }

        private static void EnsureSettingsSynced()
        {
            string distressRegion = Settings.options.EnableRegionalAfflictions ? GetRegionalDistressRegionId(Settings.options.RegionalDistressRegion) : string.Empty;

            if (!string.Equals(Core.State.ConfiguredRegionalDistressRegion, distressRegion, StringComparison.OrdinalIgnoreCase))
            {
                SyncFromSettings(logSettingsChanges: false);
            }
        }

        internal static void UpdateSceneContext(string sceneName)
        {
            if (!IsGameplayScene(sceneName)) return;

            EnsureState();

            string oldCurrentRegion = Core.State.CurrentLogicalRegion ?? string.Empty;
            string oldLastKnownRegion = Core.State.LastKnownLogicalRegion ?? string.Empty;

            string outdoorRegion = GetOutdoorLogicalRegion(sceneName);

            if (!string.IsNullOrEmpty(outdoorRegion))
            {
                Core.State.LastKnownLogicalRegion = outdoorRegion;
                Core.State.CurrentLogicalRegion = outdoorRegion;
            }
            else if (!string.IsNullOrEmpty(Core.State.LastKnownLogicalRegion))
            {
                Core.State.CurrentLogicalRegion = Core.State.LastKnownLogicalRegion;
            }
            else
            {
                Core.State.CurrentLogicalRegion = string.Empty;
            }

            bool regionChanged = !string.Equals(oldCurrentRegion, Core.State.CurrentLogicalRegion, StringComparison.OrdinalIgnoreCase) || !string.Equals(oldLastKnownRegion, Core.State.LastKnownLogicalRegion, StringComparison.OrdinalIgnoreCase);

            if (!regionChanged) return;

            Core.Instance?.MarkDirty();

            Core.Log($"Regional context -> scene '{sceneName}' resolved as '{GetRegionLogName(Core.State.CurrentLogicalRegion)}' " + $"(last known: '{GetRegionLogName(Core.State.LastKnownLogicalRegion)}').");
        }

        internal static void Update(float gameHoursPassed)
        {
            if (gameHoursPassed <= 0f) return;

            EnsureState();
            ClampState();

            UpdateHomeRegionRelocationCooldown(gameHoursPassed);

            if (s_SettingsSyncPending) SyncFromSettings(logSettingsChanges: true);

            if (!Settings.options.EnableRegionalAfflictions)
            {
                SyncFromSettings(logSettingsChanges: false);
                return;
            }

            EnsureSettingsSynced();

            string currentRegion = Core.State.CurrentLogicalRegion ?? string.Empty;

            if (string.IsNullOrEmpty(currentRegion))
            {
                UpdateHomeSicknessTimer(active: false, gameHoursPassed);
                UpdateRegionalDistressTimer(active: false, gameHoursPassed);
                CureHomeComfort();
                SyncActiveAfflictionsWithTimers();
                return;
            }

            string homeRegion = Core.State.ConfiguredHomeRegion ?? string.Empty;
            string distressRegion = Core.State.ConfiguredRegionalDistressRegion ?? string.Empty;

            bool hasHomeRegion = !string.IsNullOrEmpty(homeRegion);
            bool hasDistressRegion = !string.IsNullOrEmpty(distressRegion);

            bool inHomeRegion = hasHomeRegion && string.Equals(currentRegion, homeRegion, StringComparison.OrdinalIgnoreCase);

            bool distressConflict = hasHomeRegion && hasDistressRegion && string.Equals(homeRegion, distressRegion, StringComparison.OrdinalIgnoreCase);

            bool inDistressRegion = hasDistressRegion && !distressConflict && !inHomeRegion && string.Equals(currentRegion, distressRegion, StringComparison.OrdinalIgnoreCase);

            if (inHomeRegion) ApplyHomeComfort();
            else CureHomeComfort();

            UpdateHomeSicknessTimer(hasHomeRegion && !inHomeRegion, gameHoursPassed);
            UpdateRegionalDistressTimer(inDistressRegion, gameHoursPassed);

            SyncActiveAfflictionsWithTimers();
        }

        internal static bool ShouldHomeComfortBeActive()
        {
            if (!Settings.options.EnableRegionalAfflictions) return false;

            EnsureState();

            string homeRegion = Core.State.ConfiguredHomeRegion ?? string.Empty;
            string currentRegion = Core.State.CurrentLogicalRegion ?? string.Empty;

            return !string.IsNullOrEmpty(homeRegion) && !string.IsNullOrEmpty(currentRegion) && string.Equals(currentRegion, homeRegion, StringComparison.OrdinalIgnoreCase);
        }

        internal static bool ShouldHomeSicknessBeActive()
        {
            if (!Settings.options.EnableRegionalAfflictions) return false;

            EnsureState();

            string homeRegion = Core.State.ConfiguredHomeRegion ?? string.Empty;
            string currentRegion = Core.State.CurrentLogicalRegion ?? string.Empty;

            return !string.IsNullOrEmpty(homeRegion) && !string.IsNullOrEmpty(currentRegion) && !string.Equals(currentRegion, homeRegion, StringComparison.OrdinalIgnoreCase) && Core.State.HomeSicknessHoursAway >= GetHomeSicknessDelayHours();
        }

        internal static bool ShouldRegionalDistressBeActive()
        {
            if (!Settings.options.EnableRegionalAfflictions) return false;

            EnsureState();

            string homeRegion = Core.State.ConfiguredHomeRegion ?? string.Empty;
            string distressRegion = Core.State.ConfiguredRegionalDistressRegion ?? string.Empty;
            string currentRegion = Core.State.CurrentLogicalRegion ?? string.Empty;

            if (string.IsNullOrEmpty(distressRegion) || string.IsNullOrEmpty(currentRegion)) return false;

            if (!string.IsNullOrEmpty(homeRegion) && string.Equals(homeRegion, distressRegion, StringComparison.OrdinalIgnoreCase)) return false;

            return string.Equals(currentRegion, distressRegion, StringComparison.OrdinalIgnoreCase) && Core.State.RegionalDistressHoursInRegion >= GetRegionalDistressDelayHours();
        }

        private static void UpdateHomeSicknessTimer(bool active, float gameHoursPassed)
        {
            float oldValue = Core.State.HomeSicknessHoursAway;

            if (active)
            {
                Core.State.HomeSicknessHoursAway += gameHoursPassed;

                LogStateOnce(ref s_LastLoggedHomeState, "away", $"HomeSickness -> away from home ({Core.State.HomeSicknessHoursAway:0.#}/{GetHomeSicknessDelayHours():0.#}h).");
            }
            else
            {
                Core.State.HomeSicknessHoursAway = Mathf.Max(0f, Core.State.HomeSicknessHoursAway - gameHoursPassed * RECOVERY_MULTIPLIER);

                if (oldValue > 0f && Mathf.Approximately(Core.State.HomeSicknessHoursAway, 0f))
                {
                    LogStateOnce(ref s_LastLoggedHomeState, "safe", "HomeSickness -> fully recovered.");
                }
            }

            if (!Mathf.Approximately(oldValue, Core.State.HomeSicknessHoursAway)) Core.Instance?.MarkDirty();
        }

        private static void UpdateRegionalDistressTimer(bool active, float gameHoursPassed)
        {
            float oldValue = Core.State.RegionalDistressHoursInRegion;

            if (active)
            {
                Core.State.RegionalDistressHoursInRegion += gameHoursPassed;

                LogStateOnce(ref s_LastLoggedRegionalDistressState, "inside", $"RegionalDistress -> inside distress region ({Core.State.RegionalDistressHoursInRegion:0.#}/{GetRegionalDistressDelayHours():0.#}h).");
            }
            else
            {
                Core.State.RegionalDistressHoursInRegion = Mathf.Max(0f, Core.State.RegionalDistressHoursInRegion - gameHoursPassed * RECOVERY_MULTIPLIER);

                if (oldValue > 0f && Mathf.Approximately(Core.State.RegionalDistressHoursInRegion, 0f))
                {
                    LogStateOnce(ref s_LastLoggedRegionalDistressState, "safe", "RegionalDistress -> fully recovered.");
                }
            }

            if (!Mathf.Approximately(oldValue, Core.State.RegionalDistressHoursInRegion)) Core.Instance?.MarkDirty();
        }

        private static void SyncActiveAfflictionsWithTimers()
        {
            if (ShouldHomeSicknessBeActive()) ApplyHomeSickness();
            else CureHomeSickness();

            if (ShouldRegionalDistressBeActive()) ApplyRegionalDistress();
            else CureRegionalDistress();
        }

        private static void ApplyHomeComfort()
        {
            if (AfflictionLogic.HasAffliction<HomeComfort.HomeComfortBuff>()) return;

            new HomeComfort.HomeComfortBuff(AfflictionBodyArea.Head).Start();
            AfflictionSaveHelper.QueueSurvivalSave();

            AfflictionLogic.ForceRefreshEffects();

            Core.Log($"HomeComfort -> applied in {GetRegionLogName(Core.State.CurrentLogicalRegion)}.");
        }

        private static void ApplyHomeSickness()
        {
            if (AfflictionLogic.HasAffliction<HomeSickness.HomeSicknessAffliction>()) return;

            new HomeSickness.HomeSicknessAffliction(AfflictionBodyArea.Head).Start();
            AfflictionSaveHelper.QueueSurvivalSave();

            AfflictionLogic.ForceRefreshEffects();

            Core.Log($"HomeSickness -> applied after {Core.State.HomeSicknessHoursAway:0.#}h away from {GetRegionLogName(Core.State.ConfiguredHomeRegion)}.");
        }

        private static void ApplyRegionalDistress()
        {
            if (AfflictionLogic.HasAffliction<RegionalDistress.RegionalDistressAffliction>()) return;

            new RegionalDistress.RegionalDistressAffliction(AfflictionBodyArea.Head).Start();
            AfflictionSaveHelper.QueueSurvivalSave();

            AfflictionLogic.ForceRefreshEffects();

            Core.Log($"RegionalDistress -> applied after {Core.State.RegionalDistressHoursInRegion:0.#}h in {GetRegionLogName(Core.State.ConfiguredRegionalDistressRegion)}.");
        }

        private static bool CureHomeComfort()
        {
            bool cured = CureAllAfflictionsOfType<HomeComfort.HomeComfortBuff>();

            if (cured)
            {
                AfflictionLogic.ForceRefreshEffects();
                Core.Log("HomeComfort -> removed.");
            }

            return cured;
        }

        private static bool CureHomeSickness()
        {
            bool cured = CureAllAfflictionsOfType<HomeSickness.HomeSicknessAffliction>();

            if (cured)
            {
                AfflictionLogic.ForceRefreshEffects();
                Core.Log("HomeSickness -> removed.");
            }

            return cured;
        }

        private static bool CureRegionalDistress()
        {
            bool cured = CureAllAfflictionsOfType<RegionalDistress.RegionalDistressAffliction>();

            if (cured)
            {
                AfflictionLogic.ForceRefreshEffects();
                Core.Log("RegionalDistress -> removed.");
            }

            return cured;
        }

        private static bool CureAllAfflictionsOfType<TAffliction>() where TAffliction : class
        {
            AfflictionManager mgr = AfflictionManager.GetAfflictionManagerInstance();

            if (mgr?.m_Afflictions == null) return false;

            bool cured = false;

            for (int i = mgr.m_Afflictions.Count - 1; i >= 0; i--)
            {
                if (mgr.m_Afflictions[i] is TAffliction afflictionObject && afflictionObject is CustomAffliction affliction)
                {
                    if (affliction is HomeSickness.HomeSicknessAffliction { DebugForced: true } ||
                        affliction is RegionalDistress.RegionalDistressAffliction { DebugForced: true })
                    {
                        continue;
                    }

                    affliction.Cure();
                    cured = true;
                }
            }

            return cured;
        }

        private static string GetOutdoorLogicalRegion(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) return string.Empty;

            if (s_OutdoorSceneToLogicalRegion.TryGetValue(sceneName, out string? logicalRegion)) return logicalRegion;

            if (sceneName.EndsWith("Region", StringComparison.OrdinalIgnoreCase)) return sceneName;

            if (sceneName.EndsWith("TransitionZone", StringComparison.OrdinalIgnoreCase)) return sceneName;

            return string.Empty;
        }

        internal static bool IsGameplayScene(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) return false;

            string lower = sceneName.ToLowerInvariant();

            return lower != "mainmenu" && lower != "boot" && lower != "empty" && !lower.Contains("menu");
        }

        private static float GetHomeSicknessDelayHours()
        {
            return Mathf.Max(1f, Settings.options.HomeSicknessDelayHours);
        }

        private static float GetRegionalDistressDelayHours()
        {
            return Mathf.Max(1f, Settings.options.RegionalDistressDelayHours);
        }

        private static void EnsureState()
        {
            Core.State ??= new MMState();

            Core.State.LastKnownLogicalRegion ??= string.Empty;
            Core.State.CurrentLogicalRegion ??= string.Empty;
            Core.State.ConfiguredHomeRegion ??= string.Empty;
            Core.State.ConfiguredRegionalDistressRegion ??= string.Empty;
        }

        private static void ClampState()
        {
            Core.State.HomeSicknessHoursAway = Mathf.Max(0f, Core.State.HomeSicknessHoursAway);
            Core.State.RegionalDistressHoursInRegion = Mathf.Max(0f, Core.State.RegionalDistressHoursInRegion);
        }

        private static void LogStateOnce(ref string state, string newState, string message)
        {
            if (state == newState) return;

            state = newState;
            Core.Log(message);
        }
    }
}