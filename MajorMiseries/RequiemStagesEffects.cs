using MajorMiseries.Persistence;

namespace MajorMiseries
{
    internal static class RequiemStagesEffects
    {
        internal const float GaugeLockPercent = 0.25f;
        internal const float GaugeAvailablePercent = 1f - GaugeLockPercent;

        internal readonly struct StageProfile
        {
            public readonly float MaxHpPenalty;
            public readonly int BasePredatorThreat;
            public readonly bool ConvertPredatorBloodLossToSevere;
            public readonly bool PredatorHostilityEnabled;
            public readonly bool LockHunger;
            public readonly bool LockThirst;
            public readonly bool LockFatigue;
            public readonly bool LockFreezing;

            public StageProfile(float maxHpPenalty, int basePredatorThreat, bool convertPredatorBloodLossToSevere, bool predatorHostilityEnabled, bool lockHunger, bool lockThirst, bool lockFatigue, bool lockFreezing)
            {
                MaxHpPenalty = maxHpPenalty;
                BasePredatorThreat = basePredatorThreat;
                ConvertPredatorBloodLossToSevere = convertPredatorBloodLossToSevere;
                PredatorHostilityEnabled = predatorHostilityEnabled;
                LockHunger = lockHunger;
                LockThirst = lockThirst;
                LockFatigue = lockFatigue;
                LockFreezing = lockFreezing;
            }
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
    }
}