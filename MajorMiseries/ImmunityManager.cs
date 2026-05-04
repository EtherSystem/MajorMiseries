using AfflictionComponent.Components;
using MajorMiseries.Afflictions.Buffs;
using static MajorMiseries.Afflictions.Fever;

namespace MajorMiseries
{
    internal static class ImmunityManager
    {
        private const string ImmunityWidgetName = "Immunity Shield";

        private const float ShieldMin = 0f;
        private const float ShieldMax = 100f;

        private const float LowStatThresholdPercent = 25f;
        private const float RegenAllowedStatThresholdPercent = 25f;
        private const float RegenBonusStatThresholdPercent = 75f;
        private const float ConditionRegenThresholdPercent = 50f;
        private const float ReducedConditionRegenThresholdPercent = 20f;
        private const float ConditionRegenBonusThresholdPercent = 75f;

        private const float MaxDrainPerStatPerHour = 1f;
        private const float ZeroStatExtraDrainMultiplier = 0.5f;
        private const float MaxTotalDrainPerHour = 8f;

        private const float InfectionRiskDrainPerHour = 0.25f;
        private const float InfectionDrainPerHour = 0.75f;
        private const float SepsisRiskDrainPerHour = 1f;
        private const float SepsisDrainPerHour = 2f;
        private const float IntestinalParasitesRiskDrainPerHour = 0.20f;
        private const float IntestinalParasitesDrainPerHour = 0.50f;
        private const float FoodPoisoningDrainPerHour = 0.50f;
        private const float DysenteryDrainPerHour = 0.75f;
        private const float CorpseSicknessRiskDrainPerHour = 0.40f;
        private const float CorpseSicknessDrainPerHour = 0.80f;

        private const float AwakeRegenPerHour = 0.10f;
        private const float SleepRegenPerHour = 1.00f;
        private const float StatHighBonusMultiplier = 0.025f;
        private const float ConditionHighBonusMultiplier = 0.05f;
        private const float HomeComfortRegenMultiplier = 1.05f;

        private const float FeverMinShield = 50f;
        private const float FeverOnsetRequiredHours = 1f;
        private const float FeverLingerHours = 3f;

        private const float EffectiveFeverFatiguePerHour = 3f;
        private const float LingeringFeverFatiguePerHour = 1f;
        private const float EffectiveFeverThirstPerHour = 3f;
        private const float LingeringFeverThirstPerHour = 1f;

        private const float FeverRiskMultiplier = 0.90f;
        private const float MinRiskProgressMultiplier = 0.45f;
        private const float MaxRiskProgressMultiplier = 4f;

        private const float FeverInfectionRiskRecoveryMinShield = 90f;
        private const float FeverInfectionRiskRecoveryRollHours = 1f;
        private const float FeverInfectionRiskRecoveryChance = 5f;

        private static int s_LastLoggedShieldBucket = -1;
        private static string s_LastLoggedMode = string.Empty;

        private static FeverMode s_FeverMode = FeverMode.None;
        private static float s_FeverInfectionRiskRecoveryHours;

        internal static bool IsFeverActive => s_FeverMode == FeverMode.Effective || s_FeverMode == FeverMode.Lingering;

        internal static void ResetRuntime()
        {
            s_LastLoggedShieldBucket = -1;
            s_LastLoggedMode = string.Empty;

            s_FeverMode = FeverMode.None;
            s_FeverInfectionRiskRecoveryHours = 0f;
        }

        internal static void ClampState()
        {
            Core.State.ImmunityShield = Mathf.Clamp(Core.State.ImmunityShield, ShieldMin, ShieldMax);
            Core.State.FeverOnsetHours = Mathf.Clamp(Core.State.FeverOnsetHours, 0f, FeverOnsetRequiredHours);
            Core.State.FeverLingerHoursRemaining = Mathf.Clamp(Core.State.FeverLingerHoursRemaining, 0f, FeverLingerHours);
        }

        internal static void OnStateLoaded()
        {
            ClampState();
            ResetRuntime();
        }

        internal static void Update(float gameHoursPassed)
        {
            if (!IsEnabled())
            {
                bool changed = Core.State.FeverOnsetHours > 0f || Core.State.FeverLingerHoursRemaining > 0f || s_FeverMode != FeverMode.None;

                Core.State.FeverOnsetHours = 0f;
                Core.State.FeverLingerHoursRemaining = 0f;
                s_FeverMode = FeverMode.None;
                s_FeverInfectionRiskRecoveryHours = 0f;

                SyncFeverAffliction(false);

                if (changed) Core.Instance?.MarkDirty();
                return;
            }

            if (gameHoursPassed <= 0f || gameHoursPassed > 12f) return;
            if (!TryGetBodyStats(out BodyStats stats)) return;

            float oldShield = Core.State.ImmunityShield;

            float bodyDrainPerHour = GetBodyStatDrainPerHour(stats, out int activeDrainStats, out int zeroStats, out float zeroDrainMultiplier);
            float biologicalDrainPerHour = GetBiologicalThreatDrainPerHour(out bool biologicalThreat, out string biologicalSources);

            if (biologicalDrainPerHour > 0f) biologicalDrainPerHour *= zeroDrainMultiplier;

            float drainPerHour = Mathf.Clamp(bodyDrainPerHour + biologicalDrainPerHour, 0f, MaxTotalDrainPerHour);

            string mode;

            if (drainPerHour > 0f)
            {
                Core.State.ImmunityShield -= drainPerHour * gameHoursPassed;
                mode = BuildDrainMode(activeDrainStats, zeroStats, bodyDrainPerHour, biologicalDrainPerHour, biologicalSources);
            }
            else
            {
                float regenPerHour = GetRegenPerHour(stats, biologicalThreat);

                if (regenPerHour > 0f)
                {
                    Core.State.ImmunityShield += regenPerHour * gameHoursPassed;
                    mode = "regenerating";
                }
                else
                {
                    mode = biologicalThreat ? "engaged" : "stable";
                }
            }

            Core.State.ImmunityShield = Mathf.Clamp(Core.State.ImmunityShield, ShieldMin, ShieldMax);

            UpdateFeverState(gameHoursPassed, biologicalThreat);
            SyncFeverAffliction(ShouldFeverAfflictionBeVisible());

            UpdateFeverInfectionRiskRecovery(gameHoursPassed);

            if (!Mathf.Approximately(oldShield, Core.State.ImmunityShield)) Core.Instance?.MarkDirty();

            LogStateIfChanged(mode, stats, drainPerHour, bodyDrainPerHour, biologicalDrainPerHour);
        }

        internal static bool IsEffectiveFeverActive()
        {
            return s_FeverMode == FeverMode.Effective;
        }

        internal static bool ShouldFeverAfflictionBeVisible()
        {
            return s_FeverMode == FeverMode.Effective || s_FeverMode == FeverMode.Lingering;
        }

        internal static float GetFeverFatiguePerHour()
        {
            if (s_FeverMode == FeverMode.Effective) return EffectiveFeverFatiguePerHour;
            if (s_FeverMode == FeverMode.Lingering) return LingeringFeverFatiguePerHour;

            return 0f;
        }

        internal static float GetFeverThirstPerHour()
        {
            if (s_FeverMode == FeverMode.Effective) return EffectiveFeverThirstPerHour;
            if (s_FeverMode == FeverMode.Lingering) return LingeringFeverThirstPerHour;

            return 0f;
        }

        internal static float GetRiskProgressMultiplier()
        {
            if (!IsEnabled()) return 1f;

            float shield = Mathf.Clamp(Core.State.ImmunityShield, ShieldMin, ShieldMax);
            float multiplier;

            if (shield >= 50f)
            {
                multiplier = Mathf.Lerp(1f, 0.5f, (shield - 50f) / 50f);
            }
            else
            {
                multiplier = Mathf.Lerp(4f, 1f, shield / 50f);
            }

            if (IsEffectiveFeverActive()) multiplier *= FeverRiskMultiplier;

            return Mathf.Clamp(multiplier, MinRiskProgressMultiplier, MaxRiskProgressMultiplier);
        }

        internal static string GetFirstAidText()
        {
            if (!IsEnabled()) return "0%";

            return $"{Core.State.ImmunityShield:0}%";
        }

        private static bool IsEnabled()
        {
            return Settings.options != null && Settings.options.EnableImmunityShield;
        }

        private static bool TryGetBodyStats(out BodyStats stats)
        {
            stats = default;

            Fatigue fatigue = GameManager.GetFatigueComponent();
            Hunger hunger = GameManager.GetHungerComponent();
            Thirst thirst = GameManager.GetThirstComponent();
            Freezing freezing = GameManager.GetFreezingComponent();
            PlayerManager player = GameManager.GetPlayerManagerComponent();
            Condition condition = GameManager.GetConditionComponent();

            if (fatigue == null || hunger == null || thirst == null || freezing == null || player == null || condition == null) return false;

            float fatigue01 = Mathf.Clamp01(fatigue.GetNormalizedFatigue());
            float hungerMax = Mathf.Max(1f, hunger.GetAdjustedMaxReserveCalories());
            float thirstMax = Mathf.Max(1f, thirst.m_MaxThirst);
            float freezingMax = Mathf.Max(1f, freezing.m_MaxFreezing);

            stats.EnergyPercent = Mathf.Clamp01(1f - fatigue01) * 100f;
            stats.CaloriesPercent = Mathf.Clamp01(hunger.m_CurrentReserveCalories / hungerMax) * 100f;
            stats.HydrationPercent = Mathf.Clamp01(1f - thirst.m_CurrentThirst / thirstMax) * 100f;
            stats.WarmthPercent = Mathf.Clamp01(1f - freezing.m_CurrentFreezing / freezingMax) * 100f;
            stats.ConditionPercent = GetConditionPercentForRegen(condition);
            stats.Sleeping = player.PlayerIsSleeping();

            return true;
        }

        private static float GetConditionPercentForRegen(Condition condition)
        {
            if (condition == null) return 0f;

            return Mathf.Clamp01(condition.GetNormalizedCondition()) * 100f;
        }

        private static float GetBodyStatDrainPerHour(BodyStats stats, out int activeDrainStats, out int zeroStats, out float zeroDrainMultiplier)
        {
            float[] values =
            {
                stats.EnergyPercent,
                stats.CaloriesPercent,
                stats.HydrationPercent,
                stats.WarmthPercent
            };

            zeroStats = 0;

            for (int i = 0; i < values.Length; i++)
            {
                if (values[i] <= 0.01f) zeroStats++;
            }

            zeroDrainMultiplier = GetZeroStatDrainMultiplier(zeroStats);

            activeDrainStats = 0;
            float rawTotal = 0f;

            for (int i = 0; i < values.Length; i++)
            {
                float drain = GetSingleStatDrainPerHour(values[i]);
                if (drain <= 0f) continue;

                activeDrainStats++;
                rawTotal += drain;
            }

            return rawTotal * zeroDrainMultiplier;
        }

        private static float GetZeroStatDrainMultiplier(int zeroStats)
        {
            if (zeroStats <= 0) return 1f;

            return 1f + zeroStats * ZeroStatExtraDrainMultiplier;
        }

        private static float GetSingleStatDrainPerHour(float statPercent)
        {
            if (statPercent >= LowStatThresholdPercent) return 0f;

            float pressure01 = Mathf.Clamp01((LowStatThresholdPercent - statPercent) / LowStatThresholdPercent);
            return MaxDrainPerStatPerHour * pressure01 * pressure01;
        }

        private static float GetRegenPerHour(BodyStats stats, bool biologicalThreat)
        {
            if (biologicalThreat) return 0f;

            float conditionRegenThreshold = Settings.options.MaxConditionPenaltiesBlockImmunityRegen ? ConditionRegenThresholdPercent : ReducedConditionRegenThresholdPercent;

            if (stats.ConditionPercent <= conditionRegenThreshold) return 0f;

            if (stats.EnergyPercent <= RegenAllowedStatThresholdPercent) return 0f;
            if (stats.CaloriesPercent <= RegenAllowedStatThresholdPercent) return 0f;
            if (stats.HydrationPercent <= RegenAllowedStatThresholdPercent) return 0f;
            if (stats.WarmthPercent <= RegenAllowedStatThresholdPercent) return 0f;

            float regen = stats.Sleeping ? SleepRegenPerHour : AwakeRegenPerHour;
            float multiplier = 1f;

            if (stats.EnergyPercent > RegenBonusStatThresholdPercent) multiplier += StatHighBonusMultiplier;
            if (stats.CaloriesPercent > RegenBonusStatThresholdPercent) multiplier += StatHighBonusMultiplier;
            if (stats.HydrationPercent > RegenBonusStatThresholdPercent) multiplier += StatHighBonusMultiplier;
            if (stats.WarmthPercent > RegenBonusStatThresholdPercent) multiplier += StatHighBonusMultiplier;

            if (stats.ConditionPercent > ConditionRegenBonusThresholdPercent) multiplier += ConditionHighBonusMultiplier;

            if (HomeComfort.HomeComfortBuff.IsActive) multiplier *= HomeComfortRegenMultiplier;

            return regen * multiplier;
        }

        private static float GetBiologicalThreatDrainPerHour(out bool biologicalThreat, out string biologicalSources)
        {
            biologicalThreat = false;
            biologicalSources = string.Empty;

            float total = 0f;
            List<string> sources = new();

            void Add(bool active, float drain, string source)
            {
                if (!active) return;

                total += drain;
                sources.Add(source);
            }

            try
            {
                Condition condition = GameManager.GetConditionComponent();

                if (condition != null)
                {
                    Add(condition.HasSpecificAffliction(AfflictionType.InfectionRisk), InfectionRiskDrainPerHour, "InfectionRisk");
                    Add(condition.HasSpecificAffliction(AfflictionType.Infection), InfectionDrainPerHour, "Infection");
                    Add(condition.HasSpecificAffliction(AfflictionType.IntestinalParasitesRisk), IntestinalParasitesRiskDrainPerHour, "ParasitesRisk");
                    Add(condition.HasSpecificAffliction(AfflictionType.IntestinalParasites), IntestinalParasitesDrainPerHour, "Parasites");
                    Add(condition.HasSpecificAffliction(AfflictionType.FoodPoisioning), FoodPoisoningDrainPerHour, "FoodPoisoning");
                    Add(condition.HasSpecificAffliction(AfflictionType.Dysentery), DysenteryDrainPerHour, "Dysentery");
                }

                Add(HasCustomAfflictionByTypeName("SepsisRiskAffliction"), SepsisRiskDrainPerHour, "SepsisRisk");
                Add(HasCustomAfflictionByTypeName("SepsisAffliction"), SepsisDrainPerHour, "Sepsis");
                Add(HasCustomAfflictionByTypeName("CorpseSicknessRiskAffliction"), CorpseSicknessRiskDrainPerHour, "CorpseSicknessRisk");
                Add(HasCustomAfflictionByTypeName("CorpseSicknessAffliction"), CorpseSicknessDrainPerHour, "CorpseSickness");
            }
            catch
            {
                biologicalThreat = false;
                biologicalSources = "error";
                return 0f;
            }

            biologicalThreat = sources.Count > 0;
            biologicalSources = biologicalThreat ? string.Join(",", sources) : string.Empty;

            return total;
        }

        private static string BuildDrainMode(int activeDrainStats, int zeroStats, float bodyDrainPerHour, float biologicalDrainPerHour, string biologicalSources)
        {
            if (bodyDrainPerHour > 0f && biologicalDrainPerHour > 0f)
                return $"draining (body:{bodyDrainPerHour:0.###}/h, bio:{biologicalDrainPerHour:0.###}/h {biologicalSources}, {activeDrainStats} low, {zeroStats} zero)";

            if (bodyDrainPerHour > 0f)
                return $"draining (body:{bodyDrainPerHour:0.###}/h, {activeDrainStats} low, {zeroStats} zero)";

            if (biologicalDrainPerHour > 0f)
                return $"draining (bio:{biologicalDrainPerHour:0.###}/h {biologicalSources})";

            return "draining";
        }

        private static void UpdateFeverState(float gameHoursPassed, bool biologicalThreat)
        {
            FeverMode oldMode = s_FeverMode;
            float oldOnset = Core.State.FeverOnsetHours;
            float oldLinger = Core.State.FeverLingerHoursRemaining;

            bool canHaveEffectiveFever = biologicalThreat && Core.State.ImmunityShield >= FeverMinShield;

            if (canHaveEffectiveFever)
            {
                Core.State.FeverLingerHoursRemaining = 0f;

                if (ShouldFeverAfflictionBeVisible())
                {
                    Core.State.FeverOnsetHours = FeverOnsetRequiredHours;
                    s_FeverMode = FeverMode.Effective;
                }
                else
                {
                    Core.State.FeverOnsetHours = Mathf.Min(Core.State.FeverOnsetHours + gameHoursPassed, FeverOnsetRequiredHours);
                    s_FeverMode = Core.State.FeverOnsetHours >= FeverOnsetRequiredHours ? FeverMode.Effective : FeverMode.Building;
                }
            }
            else
            {
                Core.State.FeverOnsetHours = 0f;

                if (ShouldFeverAfflictionBeVisible() && Core.State.FeverLingerHoursRemaining <= 0f)
                    Core.State.FeverLingerHoursRemaining = FeverLingerHours;

                if (Core.State.FeverLingerHoursRemaining > 0f)
                {
                    Core.State.FeverLingerHoursRemaining = Mathf.Max(0f, Core.State.FeverLingerHoursRemaining - gameHoursPassed);
                    s_FeverMode = Core.State.FeverLingerHoursRemaining > 0f ? FeverMode.Lingering : FeverMode.None;
                }
                else
                {
                    s_FeverMode = FeverMode.None;
                }
            }

            bool stateChanged = oldMode != s_FeverMode;
            bool timerChanged = !Mathf.Approximately(oldOnset, Core.State.FeverOnsetHours) || !Mathf.Approximately(oldLinger, Core.State.FeverLingerHoursRemaining);

            if (stateChanged)
            {
                Core.Log($"Fever state -> {oldMode} => {s_FeverMode} | Shield:{Core.State.ImmunityShield:0.#}% | Threat:{biologicalThreat} | Onset:{Core.State.FeverOnsetHours:0.##}h | Linger:{Core.State.FeverLingerHoursRemaining:0.##}h");
            }

            if (stateChanged || timerChanged) Core.Instance?.MarkDirty();
        }

        private static void SyncFeverAffliction(bool shouldBeVisible)
        {
            FeverAffliction? fever = GetAffliction<FeverAffliction>();

            if (shouldBeVisible)
            {
                if (fever == null)
                {
                    Core.Log($"Fever response visible -> Mode:{s_FeverMode} | Shield:{Core.State.ImmunityShield:0.#}%");
                    new FeverAffliction(AfflictionBodyArea.Head).Start();
                }

                return;
            }

            if (fever != null)
            {
                Core.Log($"Fever response ended -> Shield:{Core.State.ImmunityShield:0.#}%");
                fever.Cure();
            }
        }

        private static void UpdateFeverInfectionRiskRecovery(float gameHoursPassed)
        {
            if (!IsEnabled()) return;

            if (!IsEffectiveFeverActive())
            {
                s_FeverInfectionRiskRecoveryHours = 0f;
                return;
            }

            if (Core.State.ImmunityShield < FeverInfectionRiskRecoveryMinShield)
            {
                s_FeverInfectionRiskRecoveryHours = 0f;
                return;
            }

            InfectionRisk infectionRisk = GameManager.GetInfectionRiskComponent();
            if (infectionRisk == null || !infectionRisk.HasInfectionRisk())
            {
                s_FeverInfectionRiskRecoveryHours = 0f;
                return;
            }

            s_FeverInfectionRiskRecoveryHours += gameHoursPassed;

            while (s_FeverInfectionRiskRecoveryHours >= FeverInfectionRiskRecoveryRollHours)
            {
                s_FeverInfectionRiskRecoveryHours -= FeverInfectionRiskRecoveryRollHours;
                RollFeverInfectionRiskRecovery(infectionRisk);
            }
        }

        private static void RollFeverInfectionRiskRecovery(InfectionRisk infectionRisk)
        {
            if (infectionRisk == null) return;

            for (int i = infectionRisk.m_ElapsedHoursList.Count - 1; i >= 0; i--)
            {
                if (i < 0 || i >= infectionRisk.m_ElapsedHoursList.Count) continue;
                if (!infectionRisk.RequiresAntiseptic(i)) continue;

                float roll = UnityEngine.Random.Range(0f, 100f);

                if (roll > FeverInfectionRiskRecoveryChance)
                {
                    Core.Log($"Fever failed to contain InfectionRisk -> Index:{i} | Shield:{Core.State.ImmunityShield:0.#}% | Chance:{FeverInfectionRiskRecoveryChance:0.#}% | Roll:{roll:0.#}");
                    continue;
                }

                Core.Log($"Fever contained InfectionRisk -> Index:{i} | Shield:{Core.State.ImmunityShield:0.#}% | Chance:{FeverInfectionRiskRecoveryChance:0.#}% | Roll:{roll:0.#}");
                infectionRisk.InfectionRiskEnd(i, false);
            }
        }

        private static bool HasCustomAfflictionByTypeName(string afflictionTypeName)
        {
            if (string.IsNullOrWhiteSpace(afflictionTypeName)) return false;

            AfflictionManager mgr = AfflictionManager.GetAfflictionManagerInstance();
            if (mgr?.m_Afflictions == null) return false;

            for (int i = 0; i < mgr.m_Afflictions.Count; i++)
            {
                object affliction = mgr.m_Afflictions[i];
                if (affliction == null) continue;

                Type type = affliction.GetType();

                if (string.Equals(type.Name, afflictionTypeName, StringComparison.OrdinalIgnoreCase)) return true;
                if (!string.IsNullOrEmpty(type.FullName) && string.Equals(type.FullName, afflictionTypeName, StringComparison.OrdinalIgnoreCase)) return true;
            }

            return false;
        }

        private static T? GetAffliction<T>() where T : class
        {
            AfflictionManager mgr = AfflictionManager.GetAfflictionManagerInstance();
            if (mgr?.m_Afflictions == null) return null;

            for (int i = 0; i < mgr.m_Afflictions.Count; i++)
            {
                if (mgr.m_Afflictions[i] is T affliction) return affliction;
            }

            return null;
        }

        private static void LogStateIfChanged(string mode, BodyStats stats, float drainPerHour, float bodyDrainPerHour, float biologicalDrainPerHour)
        {
            int bucket = Mathf.FloorToInt(Core.State.ImmunityShield / 10f) * 10;
            string fever = s_FeverMode != FeverMode.None ? $" | Fever:{s_FeverMode}" : string.Empty;

            if (bucket == s_LastLoggedShieldBucket && mode == s_LastLoggedMode) return;

            s_LastLoggedShieldBucket = bucket;
            s_LastLoggedMode = mode;

            Core.Log($"ImmunityShield -> {Core.State.ImmunityShield:0.#}% | {mode} | Drain:{drainPerHour:0.###}/h | Body:{bodyDrainPerHour:0.###}/h | Bio:{biologicalDrainPerHour:0.###}/h{fever} | Cond:{stats.ConditionPercent:0}% Energy:{stats.EnergyPercent:0}% Cal:{stats.CaloriesPercent:0}% Hyd:{stats.HydrationPercent:0}% Warm:{stats.WarmthPercent:0}%");
        }

        private static GameObject? FindChild(GameObject parent, string childName)
        {
            for (int i = 0; i < parent.transform.childCount; i++)
            {
                GameObject child = parent.transform.GetChild(i).gameObject;
                if (child.name == childName) return child;
            }

            return null;
        }

        private static void PositionImmunitySection(GameObject caloriesSection, GameObject immunitySection)
        {
            Vector3 pos = caloriesSection.transform.position;

            pos.x += 0.02f;
            pos.y += 0.98f;

            immunitySection.transform.position = pos;
        }

        private enum FeverMode
        {
            None,
            Building,
            Effective,
            Lingering
        }

        private struct BodyStats
        {
            public float EnergyPercent;
            public float CaloriesPercent;
            public float HydrationPercent;
            public float WarmthPercent;
            public float ConditionPercent;
            public bool Sleeping;
        }

        private struct InfectionRiskPatchState
        {
            public bool Applied;
            public float OriginalIncreasePerHour;
        }

        [HarmonyPatch(typeof(InfectionRisk), nameof(InfectionRisk.UpdateInfectionRisk))]
        private static class InfectionRisk_UpdateInfectionRisk_Patch
        {
            private static void Prefix(InfectionRisk __instance, int index, ref InfectionRiskPatchState __state)
            {
                __state = default;

                if (!IsEnabled()) return;
                if (__instance == null || index < 0 || index >= __instance.m_ElapsedHoursList.Count) return;
                if (__instance.IsConstant(index)) return;

                TimeOfDay tod = GameManager.GetTimeOfDayComponent();
                if (tod == null) return;

                float vanillaDelta = tod.GetTODHours(Time.deltaTime);
                if (vanillaDelta <= 0f) return;

                float multiplier = GetRiskProgressMultiplier();
                if (Mathf.Approximately(multiplier, 1f)) return;

                __instance.m_ElapsedHoursList[index] += vanillaDelta * (multiplier - 1f);

                __state.Applied = true;
                __state.OriginalIncreasePerHour = __instance.m_InfectionChanceIncreasePerHour;
                __instance.m_InfectionChanceIncreasePerHour = __state.OriginalIncreasePerHour * multiplier;
            }

            private static void Postfix(InfectionRisk __instance, InfectionRiskPatchState __state)
            {
                if (!__state.Applied || __instance == null) return;

                __instance.m_InfectionChanceIncreasePerHour = __state.OriginalIncreasePerHour;
            }
        }

        [HarmonyPatch(typeof(IntestinalParasites), nameof(IntestinalParasites.AddRiskPercent))]
        private static class IntestinalParasites_AddRiskPercent_Patch
        {
            private static void Prefix(IntestinalParasites __instance, ref float __state)
            {
                __state = __instance != null ? __instance.m_CurrentInfectionChance : 0f;
            }

            private static void Postfix(IntestinalParasites __instance, float __state)
            {
                if (!IsEnabled()) return;
                if (__instance == null || !__instance.HasIntestinalParasitesRisk()) return;

                float current = __instance.m_CurrentInfectionChance;
                float added = current - __state;
                if (added <= 0f) return;

                float adjustedAdded = added * GetRiskProgressMultiplier();
                __instance.m_CurrentInfectionChance = Mathf.Clamp(__state + adjustedAdded, 1f, 100f);
            }
        }

        [HarmonyPatch(typeof(IntestinalParasites), nameof(IntestinalParasites.CheckForInfection))]
        private static class IntestinalParasites_CheckForInfection_Patch
        {
            private static void Prefix(IntestinalParasites __instance)
            {
                if (!IsEnabled()) return;
                if (__instance == null || !__instance.HasIntestinalParasitesRisk()) return;

                TimeOfDay tod = GameManager.GetTimeOfDayComponent();
                if (tod == null) return;

                float vanillaDelta = tod.GetTODHours(Time.deltaTime);
                if (vanillaDelta <= 0f) return;

                float multiplier = GetRiskProgressMultiplier();
                if (Mathf.Approximately(multiplier, 1f)) return;

                __instance.m_RiskElapsedHours += vanillaDelta * (multiplier - 1f);
            }

            private static void Postfix(IntestinalParasites __instance)
            {
                if (__instance == null || !__instance.HasIntestinalParasitesRisk()) return;
                if (__instance.m_RiskElapsedHours < 0f) __instance.m_RiskElapsedHours = 0f;
            }
        }

        [HarmonyPatch(typeof(Panel_FirstAid), nameof(Panel_FirstAid.Initialize))]
        private static class PanelFirstAid_Initialize_Patch
        {
            [HarmonyPriority(Priority.Last)]
            private static void Postfix(Panel_FirstAid __instance)
            {
                GameObject statusBars = __instance.gameObject.transform.GetChild(2).gameObject;
                GameObject caloriesSection = statusBars.transform.GetChild(12).gameObject;

                GameObject? immunitySection = FindChild(statusBars, ImmunityWidgetName);

                if (immunitySection == null)
                {
                    immunitySection = UnityEngine.Object.Instantiate(caloriesSection, caloriesSection.transform.parent);
                    immunitySection.name = ImmunityWidgetName;
                }

                PositionImmunitySection(caloriesSection, immunitySection);
            }
        }

        [HarmonyPatch(typeof(Panel_FirstAid), nameof(Panel_FirstAid.RefreshStatusLabels))]
        private static class PanelFirstAid_RefreshStatusLabels_Patch
        {
            [HarmonyPriority(Priority.Last)]
            private static void Postfix(Panel_FirstAid __instance)
            {
                GameObject statusBars = __instance.gameObject.transform.GetChild(2).gameObject;
                GameObject? immunitySection = FindChild(statusBars, ImmunityWidgetName);

                if (immunitySection == null) return;

                immunitySection.SetActive(IsEnabled());
                if (!IsEnabled()) return;

                GameObject caloriesSection = statusBars.transform.GetChild(12).gameObject;
                PositionImmunitySection(caloriesSection, immunitySection);

                immunitySection.transform.GetChild(1).GetComponent<UILabel>().text = "IMMUNITY SHIELD";
                immunitySection.transform.GetChild(2).GetComponent<UILabel>().text = GetFirstAidText();
            }
        }
    }
}