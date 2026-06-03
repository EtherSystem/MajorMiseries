using Il2CppInterop.Runtime;
using Il2CppTLD.Gameplay;
using Il2CppTLD.Player;
using MajorMiseries.Persistence;

namespace MajorMiseries
{
    internal static class RequiemStagesEffects
    {
        internal const float GaugeLockPercent = 0.25f;
        internal const float GaugeAvailablePercent = 1f - GaugeLockPercent;

        private const int PreferredVitaminCIndex = 0;
        private const float MinVitaminCTrackedDelta = 0.000001f;

        private static bool s_HasLastVitaminCDrainState;
        private static bool s_LastVitaminCDrainActive;
        private static int s_LastVitaminCIndex = -1;
        private static int s_LastVitaminCLossPerDay = -1;
        private static float s_LastVitaminCDrainMultiplier = 1f;
        private static RequiemStage s_LastVitaminCDrainStage = RequiemStage.None;

        internal static void ResetRuntime()
        {
            s_HasLastVitaminCDrainState = false;
            s_LastVitaminCDrainActive = false;
            s_LastVitaminCIndex = -1;
            s_LastVitaminCLossPerDay = -1;
            s_LastVitaminCDrainMultiplier = 1f;
            s_LastVitaminCDrainStage = RequiemStage.None;
        }

        internal static float GetVitaminCDrainPresetMultiplier()
        {
            return Settings.options.VitaminCDrainPreset switch
            {
                0 => 1.5f,
                1 => 2f,
                2 => 3f,
                3 => 4f,
                4 => 5f,
                _ => 2f
            };
        }

        internal static bool IsVitaminCDrainAccelerationActive(RequiemStage stage)
        {
            return Settings.options.VitaminCDrainMode switch
            {
                1 => true,
                2 => false,
                _ => Settings.options.EnableRequiemStages && stage >= RequiemStage.Omen
            };
        }

        internal static float GetVitaminCDrainMultiplier(RequiemStage stage)
        {
            return IsVitaminCDrainAccelerationActive(stage) ? GetVitaminCDrainPresetMultiplier() : 1f;
        }

        internal static bool TryGetCurrentVitaminCAmount(out float amount)
        {
            amount = 500f;

            Nutrition nutrition = Nutrition.Instance;
            if (nutrition == null) return false;
            if (!Nutrition_Update_VitaminCDrainPatch.TryGetVitaminCIndex(nutrition, out int vitaminCIndex)) return false;

            amount = Mathf.Max(0f, nutrition.m_Amounts[vitaminCIndex]);
            return true;
        }

        private static void LogVitaminCDrainStateIfChanged(RequiemStage stage, bool active, float multiplier, int vitaminCIndex, int lossPerDay, float amount)
        {
            if (s_HasLastVitaminCDrainState &&
                active == s_LastVitaminCDrainActive &&
                vitaminCIndex == s_LastVitaminCIndex &&
                lossPerDay == s_LastVitaminCLossPerDay &&
                Mathf.Approximately(multiplier, s_LastVitaminCDrainMultiplier) &&
                stage == s_LastVitaminCDrainStage) return;

            s_HasLastVitaminCDrainState = true;
            s_LastVitaminCDrainActive = active;
            s_LastVitaminCIndex = vitaminCIndex;
            s_LastVitaminCLossPerDay = lossPerDay;
            s_LastVitaminCDrainMultiplier = multiplier;
            s_LastVitaminCDrainStage = stage;

            float modifiedLossPerDay = active ? lossPerDay * multiplier : lossPerDay;

            Core.Log($"Vitamin C drain acceleration active:{active} | stage:{stage} | multiplier:x{multiplier:0.#} | index:{vitaminCIndex} | vanillaLossPerDay:{lossPerDay:0.###} | modifiedLossPerDay:{modifiedLossPerDay:0.###} | m_Amounts:{amount:0.######}");
        }

        internal readonly struct StageProfile(float maxHpPenalty, int basePredatorThreat, bool convertPredatorBloodLossToSevere, bool predatorHostilityEnabled, bool lockHunger, bool lockThirst, bool lockFatigue, bool lockFreezing)
        {
            public readonly float MaxHpPenalty = maxHpPenalty;
            public readonly int BasePredatorThreat = basePredatorThreat;
            public readonly bool ConvertPredatorBloodLossToSevere = convertPredatorBloodLossToSevere;
            public readonly bool PredatorHostilityEnabled = predatorHostilityEnabled;
            public readonly bool LockHunger = lockHunger;
            public readonly bool LockThirst = lockThirst;
            public readonly bool LockFatigue = lockFatigue;
            public readonly bool LockFreezing = lockFreezing;
        }

        internal static StageProfile GetProfile(RequiemStage stage)
        {
            return stage switch
            {
                RequiemStage.Omen => new StageProfile(
                    maxHpPenalty: 10f,
                    basePredatorThreat: 1,
                    convertPredatorBloodLossToSevere: false,
                    predatorHostilityEnabled: true,
                    lockHunger: true,
                    lockThirst: false,
                    lockFatigue: false,
                    lockFreezing: false),

                RequiemStage.Dirge => new StageProfile(
                    maxHpPenalty: 20f,
                    basePredatorThreat: 2,
                    convertPredatorBloodLossToSevere: false,
                    predatorHostilityEnabled: true,
                    lockHunger: true,
                    lockThirst: true,
                    lockFatigue: false,
                    lockFreezing: false),

                RequiemStage.Knell => new StageProfile(
                    maxHpPenalty: 30f,
                    basePredatorThreat: 3,
                    convertPredatorBloodLossToSevere: false,
                    predatorHostilityEnabled: true,
                    lockHunger: true,
                    lockThirst: true,
                    lockFatigue: true,
                    lockFreezing: false),

                RequiemStage.Requiem => new StageProfile(
                    maxHpPenalty: 50f,
                    basePredatorThreat: 4,
                    convertPredatorBloodLossToSevere: true,
                    predatorHostilityEnabled: true,
                    lockHunger: true,
                    lockThirst: true,
                    lockFatigue: true,
                    lockFreezing: true),

                _ => new StageProfile(
                    maxHpPenalty: 0f,
                    basePredatorThreat: 0,
                    convertPredatorBloodLossToSevere: false,
                    predatorHostilityEnabled: false,
                    lockHunger: false,
                    lockThirst: false,
                    lockFatigue: false,
                    lockFreezing: false)
            };
        }

        internal static float GetMaxHpPenalty(RequiemStage stage, int scarredFleshCount)
        {
            StageProfile profile = GetProfile(stage);
            return profile.MaxHpPenalty + (2f * scarredFleshCount);
        }

        internal static int GetBasePredatorThreat(RequiemStage stage)
        {
            return GetProfile(stage).BasePredatorThreat;
        }

        internal static bool ShouldConvertPredatorBloodLossToSevere(RequiemStage stage)
        {
            return Settings.options.PredatorBloodLossToSevereLacerationsMode switch
            {
                1 => true,
                2 => false,
                _ => GetProfile(stage).ConvertPredatorBloodLossToSevere
            };
        }

        internal static bool ShouldConvertPredatorBloodLossToSevere()
        {
            return ShouldConvertPredatorBloodLossToSevere(AfflictionLogic.GetCurrentStage());
        }

        internal static bool IsPredatorHostilityEnabled(RequiemStage stage)
        {
            return Settings.options.PredatorHostilityMode switch
            {
                1 => true,
                2 => false,
                _ => GetProfile(stage).PredatorHostilityEnabled
            };
        }

        internal static bool IsPredatorHostilityEnabled()
        {
            return IsPredatorHostilityEnabled(AfflictionLogic.GetCurrentStage());
        }

        internal static bool IsHungerLocked(RequiemStage stage)
        {
            return GetProfile(stage).LockHunger;
        }

        internal static bool IsThirstLocked(RequiemStage stage)
        {
            return GetProfile(stage).LockThirst;
        }

        internal static bool IsFatigueLocked(RequiemStage stage)
        {
            return GetProfile(stage).LockFatigue;
        }

        internal static bool IsFreezingLocked(RequiemStage stage)
        {
            return GetProfile(stage).LockFreezing;
        }

        internal static bool IsGaugeLocked(RequiemStage stage, StatusBar.StatusBarType type)
        {
            return type switch
            {
                StatusBar.StatusBarType.Hunger => IsHungerLocked(stage),
                StatusBar.StatusBarType.Thirst => IsThirstLocked(stage),
                StatusBar.StatusBarType.Fatigue => IsFatigueLocked(stage),
                StatusBar.StatusBarType.Cold => IsFreezingLocked(stage),
                _ => false
            };
        }

        internal static bool IsGaugeLocked(StatusBar.StatusBarType type)
        {
            return IsGaugeLocked(AfflictionLogic.GetCurrentStage(), type);
        }

        private static RequiemStage GetLiveStage()
        {
            return AfflictionLogic.GetCurrentStage();
        }

        private static bool ShouldClampHunger()
        {
            return IsHungerLocked(GetLiveStage());
        }

        private static bool ShouldClampThirst()
        {
            return IsThirstLocked(GetLiveStage());
        }

        private static bool ShouldClampFatigue()
        {
            return IsFatigueLocked(GetLiveStage());
        }

        private static bool ShouldClampFreezing()
        {
            return IsFreezingLocked(GetLiveStage());
        }

        internal static void ClampHunger(Hunger hunger)
        {
            if (hunger == null || !ShouldClampHunger()) return;

            float cap = hunger.GetAdjustedMaxReserveCalories();
            if (hunger.m_CurrentReserveCalories > cap)
            {
                hunger.m_CurrentReserveCalories = cap;
            }
        }

        internal static void ClampThirst(Thirst thirst)
        {
            if (thirst == null || !ShouldClampThirst()) return;

            float floor = thirst.m_MaxThirst * GaugeLockPercent;
            if (thirst.m_CurrentThirst < floor)
            {
                thirst.m_CurrentThirst = floor;
            }
        }

        internal static void ClampFatigue(Fatigue fatigue)
        {
            if (fatigue == null || !ShouldClampFatigue()) return;

            float floor = fatigue.m_MaxFatigue * GaugeLockPercent;
            if (fatigue.m_CurrentFatigue < floor)
            {
                fatigue.m_CurrentFatigue = floor;
            }
        }

        internal static void ClampFreezing(Freezing freezing)
        {
            if (freezing == null || !ShouldClampFreezing()) return;

            float floor = freezing.m_MaxFreezing * GaugeLockPercent;
            if (freezing.m_CurrentFreezing < floor)
            {
                freezing.m_CurrentFreezing = floor;
            }
        }

        [HarmonyPatch(typeof(Hunger), nameof(Hunger.GetAdjustedMaxReserveCalories))]
        internal static class Hunger_GetAdjustedMaxReserveCalories_Patch
        {
            private static void Postfix(ref float __result)
            {
                if (!ShouldClampHunger()) return;

                __result *= GaugeAvailablePercent;
            }
        }

        [HarmonyPatch(typeof(Hunger), nameof(Hunger.Update))]
        internal static class Hunger_Update_Patch
        {
            private static void Postfix(Hunger __instance) => ClampHunger(__instance);
        }

        [HarmonyPatch(typeof(Hunger), nameof(Hunger.AddReserveCalories))]
        internal static class Hunger_AddReserveCalories_Patch
        {
            private static void Postfix(Hunger __instance) => ClampHunger(__instance);
        }

        [HarmonyPatch(typeof(Hunger), nameof(Hunger.AddReserveCaloriesOverTime))]
        internal static class Hunger_AddReserveCaloriesOverTime_Patch
        {
            private static void Postfix(Hunger __instance) => ClampHunger(__instance);
        }

        [HarmonyPatch(typeof(Thirst), nameof(Thirst.Update))]
        internal static class Thirst_Update_Patch
        {
            private static void Postfix(Thirst __instance) => ClampThirst(__instance);
        }

        [HarmonyPatch(typeof(Thirst), nameof(Thirst.AddThirst))]
        internal static class Thirst_AddThirst_Patch
        {
            private static void Postfix(Thirst __instance) => ClampThirst(__instance);
        }

        [HarmonyPatch(typeof(Thirst), nameof(Thirst.AddThirstOverTime))]
        internal static class Thirst_AddThirstOverTime_Patch
        {
            private static void Postfix(Thirst __instance) => ClampThirst(__instance);
        }

        [HarmonyPatch(typeof(Fatigue), nameof(Fatigue.Update))]
        internal static class Fatigue_Update_Patch
        {
            private static void Postfix(Fatigue __instance) => ClampFatigue(__instance);
        }

        [HarmonyPatch(typeof(Fatigue), nameof(Fatigue.AddFatigue), typeof(float))]
        internal static class Fatigue_AddFatigue_Patch
        {
            private static void Postfix(Fatigue __instance) => ClampFatigue(__instance);
        }

        [HarmonyPatch(typeof(Freezing), nameof(Freezing.Update))]
        internal static class Freezing_Update_Patch
        {
            private static void Postfix(Freezing __instance) => ClampFreezing(__instance);
        }

        [HarmonyPatch(typeof(Freezing), nameof(Freezing.AddFreezing))]
        internal static class Freezing_AddFreezing_Patch
        {
            private static void Postfix(Freezing __instance) => ClampFreezing(__instance);
        }

        //scurvy stuff
        [HarmonyPatch(typeof(Nutrition), nameof(Nutrition.Update))]
        internal static class Nutrition_Update_VitaminCDrainPatch
        {
            private struct NutritionUpdateState
            {
                public bool IsValid;
                public int VitaminCIndex;
                public float VitaminCBefore;
            }

            private static void Prefix(Nutrition __instance, out NutritionUpdateState __state)
            {
                __state = default;

                if (!TryGetVitaminCIndex(__instance, out int vitaminCIndex)) return;

                __state.IsValid = true;
                __state.VitaminCIndex = vitaminCIndex;
                __state.VitaminCBefore = __instance.m_Amounts[vitaminCIndex];
            }

            private static void Postfix(Nutrition __instance, NutritionUpdateState __state)
            {
                if (!__state.IsValid) return;
                if (!TryGetVitaminCIndex(__instance, out int vitaminCIndex)) return;
                if (vitaminCIndex != __state.VitaminCIndex) return;

                RequiemStage stage = AfflictionLogic.GetCurrentStage();
                float multiplier = GetVitaminCDrainMultiplier(stage);
                bool active = multiplier > 1f;

                int lossPerDay = GetVitaminCLossPerDay(__instance, vitaminCIndex);

                float vitaminCAfterVanilla = __instance.m_Amounts[vitaminCIndex];

                LogVitaminCDrainStateIfChanged(stage, active, multiplier, vitaminCIndex, lossPerDay, vitaminCAfterVanilla);

                if (!active) return;

                float vanillaDelta = vitaminCAfterVanilla - __state.VitaminCBefore;
                if (vanillaDelta >= -MinVitaminCTrackedDelta) return;

                float vanillaLoss = -vanillaDelta;
                float extraLoss = vanillaLoss * (multiplier - 1f);
                float modifiedVitaminC = Mathf.Max(0f, vitaminCAfterVanilla - extraLoss);

                if (modifiedVitaminC >= vitaminCAfterVanilla) return;

                __instance.m_Amounts[vitaminCIndex] = modifiedVitaminC;
            }

            internal static bool TryGetVitaminCIndex(Nutrition nutrition, out int vitaminCIndex)
            {
                vitaminCIndex = -1;

                if (nutrition == null) return false;
                if (!nutrition.IsInitialized) return false;
                if (!nutrition.HasNutritionSettings()) return false;
                if (nutrition.m_Nutrients == null || nutrition.m_Amounts == null) return false;

                ScurvyManager scurvy = GameManager.GetScurvyComponent();
                if (scurvy == null) return false;

                NutrientDefinition vitaminC = scurvy.m_NutrientDefinition;
                if (vitaminC == null) return false;

                if (IsVitaminCAtIndex(nutrition, vitaminC, PreferredVitaminCIndex))
                {
                    vitaminCIndex = PreferredVitaminCIndex;
                    return true;
                }

                IntPtr vitaminCPtr = IL2CPP.Il2CppObjectBaseToPtr(vitaminC);
                int count = Mathf.Min(nutrition.m_Nutrients.Count, nutrition.m_Amounts.Count);

                for (int i = 0; i < count; i++)
                {
                    NutrientDefinition nutrient = nutrition.m_Nutrients[i];
                    if (nutrient == null) continue;

                    IntPtr nutrientPtr = IL2CPP.Il2CppObjectBaseToPtr(nutrient);
                    if (nutrientPtr != vitaminCPtr) continue;

                    vitaminCIndex = i;
                    return true;
                }

                return false;
            }

            private static bool IsVitaminCAtIndex(Nutrition nutrition, NutrientDefinition vitaminC, int index)
            {
                if (index < 0) return false;
                if (nutrition.m_Nutrients == null || nutrition.m_Amounts == null) return false;
                if (index >= nutrition.m_Nutrients.Count || index >= nutrition.m_Amounts.Count) return false;

                NutrientDefinition nutrient = nutrition.m_Nutrients[index];
                if (nutrient == null) return false;

                return IL2CPP.Il2CppObjectBaseToPtr(nutrient) == IL2CPP.Il2CppObjectBaseToPtr(vitaminC);
            }

            private static int GetVitaminCLossPerDay(Nutrition nutrition, int vitaminCIndex)
            {
                if (nutrition == null || vitaminCIndex < 0) return 0;
                if (nutrition.m_LossPerDay == null || vitaminCIndex >= nutrition.m_LossPerDay.Count) return 0;

                return nutrition.m_LossPerDay[vitaminCIndex];
            }
        }
    }
}