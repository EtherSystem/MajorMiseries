using System.Text;
using AfflictionComponent.Components;
using Il2CppTLD.IntBackedUnit;

namespace MajorMiseries.Patches
{
    internal class AfflictionEffects
    {
        [HarmonyPatch(typeof(Condition), nameof(Condition.GetAdjustedMaxHPModifier))]
        internal static class StageMaxHPModifierPatch
        {
            private static void Postfix(ref float __result)
            {
                __result -= RequiemStagesEffects.GetMaxHpPenalty(AfflictionLogic.GetCurrentStage(), AfflictionLogic.GetScarredFleshCount());
            }
        }

        [HarmonyPatch(typeof(BloodLoss), nameof(BloodLoss.BloodLossStart))]
        [HarmonyPriority(Priority.Last)] // <-- Priority.Last to avoid conflit with ImprovedAfflictions
        internal static class BloodLossToSevereLacerationPatch
        {
            private static void Postfix(ref string cause)
            {
                AfflictionLogic.TryConvertPredatorBloodLossToSevereLaceration(cause);
            }
        }

        [HarmonyPatch(typeof(SevereLacerations), nameof(SevereLacerations.Update))]
        internal static class SevereLacerationHealingPatch
        {
            private static void Postfix(SevereLacerations __instance)
            {
                AfflictionLogic.TryHandleSevereLacerationHealing(__instance);
            }
        }

        [HarmonyPatch(typeof(PlayerStruggle), nameof(PlayerStruggle.ApplyBearDamageAfterStruggleEnds))]
        internal static class BearStruggleBrokenLimbPatch
        {
            private static void Postfix()
            {
                AfflictionLogic.TryApplyBearStruggleFracture();
            }
        }

        [HarmonyPatch(typeof(PlayerStruggle), nameof(PlayerStruggle.ApplyMooseDamageAfterStruggleEnds))]
        internal static class MooseStruggleBrokenLimbPatch
        {
            private static void Postfix()
            {
                AfflictionLogic.TryApplyMooseStruggleFracture();
            }
        }

        [HarmonyPatch(typeof(vp_FPSController), nameof(vp_FPSController.GetSlopeMultiplier))]
        internal static class MovementSpeedPatch
        {
            private static void Postfix(ref float __result)
            {
                __result *= AfflictionLogic.GetMovementSpeedMultiplier();
            }
        }

        [HarmonyPatch(typeof(Panel_Crafting), nameof(Panel_Crafting.GetModifiedCraftingDuration))]
        internal static class CraftingDurationPatch
        {
            private static void Postfix(ref int __result)
            {
                __result = (int)(__result * AfflictionLogic.GetCraftingTimeMultiplier());
            }
        }

        [HarmonyPatch(typeof(Fatigue), nameof(Fatigue.CalculateFatigueIncrease))]
        internal static class MovementFatiguePatch
        {
            private static void Postfix(ref float __result)
            {
                __result *= AfflictionLogic.GetMovementFatigueMultiplier();
            }
        }

        [HarmonyPatch(typeof(GunItem), nameof(GunItem.Update))]
        internal static class GunAimStaminaPatch
        {
            private static readonly float BASE_INCREASE = 0.1f;
            private static readonly float BASE_DECREASE = 0.15f;

            private static void Postfix(GunItem __instance)
            {
                if (__instance == null) return;

                __instance.m_SwayIncreasePerSecond = BASE_INCREASE * AfflictionLogic.GetAimSwayIncreaseMultiplier();
                __instance.m_SwayDecreasePerSecond = BASE_DECREASE * AfflictionLogic.GetAimSwayDecreaseMultiplier();
            }
        }

        [HarmonyPatch(typeof(PlayerManager), nameof(PlayerManager.PlayerCantSprintBecauseOfInjury))]
        internal static class BlockSprintBecauseOfBrokenLegPatch
        {
            private static void Postfix(ref bool __result)
            {
                if (AfflictionLogic.ShouldBlockSprint())
                {
                    __result = true;
                }
            }
        }

        [HarmonyPatch(typeof(FallDamage), nameof(FallDamage.ApplyFallDamage))]
        private static class ApplyFallDamagePatch
        {
            private static void Prefix()
            {
                AfflictionLogic.BeginFallDamageEvaluation();
            }

            private static void Postfix()
            {
                AfflictionLogic.EndFallDamageEvaluation();
            }
        }

        [HarmonyPatch(typeof(PlayerClimbRope), nameof(PlayerClimbRope.BeginClimbing))]
        internal static class BlockRopeClimbBecauseOfBrokenLimbPatch
        {
            private static bool Prefix()
            {
                if (!AfflictionLogic.ShouldBlockClimbing())
                    return true;

                GameAudioManager.PlayGUIError();
                HUDMessage.AddMessage("You cannot climb ropes with a broken limb.");
                return false;
            }
        }

        private static void ApplyBrokenLegCarryMultiplier(ref ItemWeight result)
        {
            float multiplier = AfflictionLogic.GetBrokenLegCarryCapacityMultiplier();
            if (Mathf.Approximately(multiplier, 1f))
                return;

            result *= multiplier;
        }

        [HarmonyPatch(typeof(Encumber), nameof(Encumber.GetMaxCarryCapacityKG))]
        internal static class BrokenLegMaxCarryCapacityPatch
        {
            private static void Postfix(ref ItemWeight __result)
            {
                ApplyBrokenLegCarryMultiplier(ref __result);
            }
        }

        [HarmonyPatch(typeof(Encumber), nameof(Encumber.GetMaxCarryCapacityWhenExhaustedKG))]
        internal static class BrokenLegMaxCarryCapacityWhenExhaustedPatch
        {
            private static void Postfix(ref ItemWeight __result)
            {
                ApplyBrokenLegCarryMultiplier(ref __result);
            }
        }

        [HarmonyPatch(typeof(Encumber), nameof(Encumber.GetNoSprintCarryCapacityKG))]
        internal static class BrokenLegNoSprintCarryCapacityPatch
        {
            private static void Postfix(ref ItemWeight __result)
            {
                ApplyBrokenLegCarryMultiplier(ref __result);
            }
        }

        [HarmonyPatch(typeof(Encumber), nameof(Encumber.GetNoWalkCarryCapacityKG))]
        internal static class BrokenLegNoWalkCarryCapacityPatch
        {
            private static void Postfix(ref ItemWeight __result)
            {
                ApplyBrokenLegCarryMultiplier(ref __result);
            }
        }

        [HarmonyPatch(typeof(Encumber), nameof(Encumber.GetEncumberLowThresholdKG))]
        internal static class BrokenLegEncumberLowThresholdPatch
        {
            private static void Postfix(ref ItemWeight __result)
            {
                ApplyBrokenLegCarryMultiplier(ref __result);
            }
        }

        [HarmonyPatch(typeof(Encumber), nameof(Encumber.GetEncumberMedThresholdKG))]
        internal static class BrokenLegEncumberMedThresholdPatch
        {
            private static void Postfix(ref ItemWeight __result)
            {
                ApplyBrokenLegCarryMultiplier(ref __result);
            }
        }

        [HarmonyPatch(typeof(Encumber), nameof(Encumber.GetEncumberHighThresholdKG))]
        internal static class BrokenLegEncumberHighThresholdPatch
        {
            private static void Postfix(ref ItemWeight __result)
            {
                ApplyBrokenLegCarryMultiplier(ref __result);
            }
        }
    }
}