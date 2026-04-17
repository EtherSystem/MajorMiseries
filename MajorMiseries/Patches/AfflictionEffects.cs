using System.Collections;
using Il2CppTLD.IntBackedUnit;
using static MajorMiseries.Afflictions.BlackLung;
using static MajorMiseries.Afflictions.COPoisoning;
using static MajorMiseries.Afflictions.Sepsis;
using static MajorMiseries.Afflictions.SepsisRisk;

namespace MajorMiseries.Patches
{
    internal class AfflictionEffects
    {
        // --------------------------------------------------------------------
        // Respiratory effects tuning
        // --------------------------------------------------------------------

        private const string BLACK_LUNG_COUGH_EVENT = "Play_SuffocationCough";

        private const float CO_SPRINT_USAGE_MULT = 1.85f;
        private const float CO_SPRINT_RECOVERY_MULT = 0.55f;
        private const float CO_SPRINT_RECOVERY_DELAY_MULT = 1.5f;

        private const float BLACK_LUNG_SPRINT_USAGE_MULT = 1.35f;
        private const float BLACK_LUNG_SPRINT_RECOVERY_MULT = 0.75f;
        private const float BLACK_LUNG_SPRINT_RECOVERY_DELAY_MULT = 1.2f;

        private const float BLACK_LUNG_MIN_SLEEP_HOURS_BEFORE_COUGH = 1f;
        private const float BLACK_LUNG_MAX_SLEEP_HOURS_BEFORE_COUGH = 3f;

        private static float _respiratoryEffectTickTimer = 0f;

        private static bool _blackLungWasSleeping = false;
        private static bool _blackLungWakeQueued = false;
        private static bool _blackLungCoughStopPending = false;
        private static float _blackLungSleepHoursSinceLastCough = 0f;
        private static float _nextBlackLungSleepCoughAtHours = 0f;

        private static PlayerMovement? _cachedPlayerMovement = null;
        private static float _baseSprintStaminaRecoverPerHour = 0f;
        private static float _baseSprintStaminaUsagePerSecond = 0f;
        private static float _baseSecondsNotSprintingBeforeRecovery = 0f;

        private static bool IsGameplayScene()
        {
            string scene = GameManager.m_ActiveScene;
            if (string.IsNullOrEmpty(scene))
                return false;

            string lower = scene.ToLowerInvariant();
            return !lower.Contains("menu") && !lower.Contains("boot") && lower != "empty";
        }

        private static void CacheRespiratoryMovementBaseline(PlayerMovement movement)
        {
            if (movement == null)
                return;

            if (_cachedPlayerMovement == movement)
                return;

            _cachedPlayerMovement = movement;
            _baseSprintStaminaRecoverPerHour = movement.m_SprintStaminaRecoverPerHour;
            _baseSprintStaminaUsagePerSecond = movement.m_SprintStaminaUsagePerSecond;
            _baseSecondsNotSprintingBeforeRecovery = movement.m_SecondsNotSprintingBeforeRecovery;
        }

        private static void ApplyRespiratoryStaminaTuning(PlayerMovement movement)
        {
            if (movement == null)
                return;

            CacheRespiratoryMovementBaseline(movement);

            bool hasBlackLung = BlackLungAffliction.IsActive;
            bool hasCOPoisoning = COPoisoningAffliction.IsActive;

            float usageMult = 1f;
            float recoveryMult = 1f;
            float delayMult = 1f;

            if (hasBlackLung)
            {
                usageMult = Mathf.Max(usageMult, BLACK_LUNG_SPRINT_USAGE_MULT);
                recoveryMult = Mathf.Min(recoveryMult, BLACK_LUNG_SPRINT_RECOVERY_MULT);
                delayMult = Mathf.Max(delayMult, BLACK_LUNG_SPRINT_RECOVERY_DELAY_MULT);
            }

            if (hasCOPoisoning)
            {
                usageMult = Mathf.Max(usageMult, CO_SPRINT_USAGE_MULT);
                recoveryMult = Mathf.Min(recoveryMult, CO_SPRINT_RECOVERY_MULT);
                delayMult = Mathf.Max(delayMult, CO_SPRINT_RECOVERY_DELAY_MULT);
            }

            movement.m_SprintStaminaUsagePerSecond = _baseSprintStaminaUsagePerSecond * usageMult;
            movement.m_SprintStaminaRecoverPerHour = _baseSprintStaminaRecoverPerHour * recoveryMult;
            movement.m_SecondsNotSprintingBeforeRecovery = _baseSecondsNotSprintingBeforeRecovery * delayMult;
        }

        private static void ApplyRespiratoryCameraEffects()
        {
            if (!IsGameplayScene())
                return;

            bool hasBlackLung = BlackLungAffliction.IsActive;
            bool hasCOPoisoning = COPoisoningAffliction.IsActive;

            if (!hasBlackLung && !hasCOPoisoning)
                return;

            CameraStatusEffects? cameraStatus = GameManager.GetCameraStatusEffects();
            if (cameraStatus == null)
                return;

            float waterTarget = 0f;
            float sprainTarget = 0f;
            float headacheTarget = 0f;
            float headacheSinSpeed = 0f;
            float headacheVignetteIntensity = 0f;
            Color vignetteColor = Color.white;

            if (hasBlackLung)
            {
                waterTarget = Mathf.Max(waterTarget, 0.2f);
                sprainTarget = Mathf.Max(sprainTarget, 0.1f);
                headacheTarget = Mathf.Max(headacheTarget, 0.1f);
                headacheSinSpeed = Mathf.Max(headacheSinSpeed, 2.5f);
                headacheVignetteIntensity = Mathf.Max(headacheVignetteIntensity, 0.20f);
                vignetteColor = Color.white;
            }

            if (hasCOPoisoning)
            {
                waterTarget = Mathf.Max(waterTarget, 0.35f);
                sprainTarget = Mathf.Max(sprainTarget, 0.3f);
                headacheTarget = Mathf.Max(headacheTarget, 0.4f);
                headacheSinSpeed = Mathf.Max(headacheSinSpeed, 5f);
                headacheVignetteIntensity = Mathf.Max(headacheVignetteIntensity, 0.5f);
                vignetteColor = Color.black;
            }

            cameraStatus.m_WaterTarget = Mathf.Max(cameraStatus.m_WaterTarget, waterTarget);
            cameraStatus.m_SprainTarget = Mathf.Max(cameraStatus.m_SprainTarget, sprainTarget);
            cameraStatus.m_HeadacheTarget = Mathf.Max(cameraStatus.m_HeadacheTarget, headacheTarget);
            cameraStatus.m_HeadacheSinSpeed = Mathf.Max(cameraStatus.m_HeadacheSinSpeed, headacheSinSpeed);
            cameraStatus.m_HeadacheVignetteIntensity = Mathf.Max(cameraStatus.m_HeadacheVignetteIntensity, headacheVignetteIntensity);
            cameraStatus.m_SprainVignetteColor = vignetteColor;
        }

        private static void UpdateRespiratoryTimedEffects(Condition condition, float gameHoursPassed)
        {
            if (!IsGameplayScene() || gameHoursPassed <= 0f || condition == null)
                return;

            bool hasBlackLung = BlackLungAffliction.IsActive;

            UpdateBlackLungSleepCough(hasBlackLung, gameHoursPassed);
        }

        private static void UpdateBlackLungSleepCough(bool hasBlackLung, float gameHoursPassed)
        {
            Rest? rest = GameManager.GetRestComponent();

            if (!hasBlackLung || rest == null)
            {
                _blackLungWasSleeping = false;
                _blackLungWakeQueued = false;
                _blackLungSleepHoursSinceLastCough = 0f;
                _nextBlackLungSleepCoughAtHours = 0f;
                _blackLungCoughStopPending = false;
                return;
            }

            bool isSleeping = rest.IsSleeping();

            if (!isSleeping)
            {
                _blackLungWasSleeping = false;
                _blackLungWakeQueued = false;
                _blackLungSleepHoursSinceLastCough = 0f;
                _nextBlackLungSleepCoughAtHours = 0f;
                _blackLungCoughStopPending = false;
                return;
            }

            if (!_blackLungWasSleeping)
            {
                _blackLungWasSleeping = true;
                _blackLungWakeQueued = false;
                _blackLungSleepHoursSinceLastCough = 0f;
                _nextBlackLungSleepCoughAtHours = UnityEngine.Random.Range(
                    BLACK_LUNG_MIN_SLEEP_HOURS_BEFORE_COUGH,
                    BLACK_LUNG_MAX_SLEEP_HOURS_BEFORE_COUGH
                );
            }

            if (_blackLungWakeQueued)
                return;

            _blackLungSleepHoursSinceLastCough += gameHoursPassed;

            if (_blackLungSleepHoursSinceLastCough < _nextBlackLungSleepCoughAtHours)
                return;

            _blackLungWakeQueued = true;
            _blackLungSleepHoursSinceLastCough = 0f;
            _nextBlackLungSleepCoughAtHours = UnityEngine.Random.Range(
                BLACK_LUNG_MIN_SLEEP_HOURS_BEFORE_COUGH,
                BLACK_LUNG_MAX_SLEEP_HOURS_BEFORE_COUGH
            );

            TriggerBlackLungSleepCough(rest);
        }

        private static void TriggerBlackLungSleepCough(Rest rest)
        {
            if (rest == null)
                return;

            rest.m_InterruptionAfterSecondsSleeping = 1;

            TryPlayBlackLungCough();

            HUDMessage.AddMessage(Localization.Get("GAMEPLAY_BlackLungSleepWakeup"), 4, false);
            AfflictionSaveHelper.QueueSurvivalSave();

            Core.Log("BlackLung -> sleep interrupted by coughing.");
        }

        private static void TryPlayBlackLungCough()
        {
            try
            {
                PlayerCough? cough = GameManager.GetPlayerCough();
                if (cough == null)
                    return;

                if (!cough.IsActive())
                {
                    cough.MaybeStart(BLACK_LUNG_COUGH_EVENT);

                    if (!_blackLungCoughStopPending)
                    {
                        MelonCoroutines.Start(StopBlackLungCoughAfterDelay(2.5f));
                    }
                }
            }
            catch (Exception e)
            {
                Core.Log($"BlackLung cough event failed: {e.Message}");
            }
        }

        private static IEnumerator StopBlackLungCoughAfterDelay(float delaySeconds)
        {
            _blackLungCoughStopPending = true;

            yield return new WaitForSeconds(delaySeconds);

            try
            {
                PlayerCough? cough = GameManager.GetPlayerCough();
                if (cough != null && cough.IsActive())
                {
                    cough.Stop();
                }
            }
            catch (Exception e)
            {
                Core.Log($"BlackLung cough stop failed: {e.Message}");
            }
            finally
            {
                _blackLungCoughStopPending = false;
            }
        }

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

        [HarmonyPatch(typeof(PlayerMovement), nameof(PlayerMovement.Update))]
        internal static class RespiratoryStaminaEffectsPatch
        {
            private static void Postfix(PlayerMovement __instance)
            {
                if (__instance == null || !IsGameplayScene())
                    return;

                ApplyRespiratoryStaminaTuning(__instance);
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
                HUDMessage.AddMessage(Localization.Get("GAMEPLAY_BrokenLimbNoRopeClimb"), 4, false);
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

        [HarmonyPatch(typeof(Infection), nameof(Infection.InfectionStart))]
        internal static class Infection_InfectionStart
        {
            private static void Postfix(Infection __instance, int location)
            {
                if (__instance == null)
                    return;

                MelonCoroutines.Start(ApplySepsisRiskNextFrame((AfflictionBodyArea)location));
            }

            private static IEnumerator ApplySepsisRiskNextFrame(AfflictionBodyArea requestedBodyArea)
            {
                yield return null;

                Infection infection = GameManager.GetInfectionComponent();
                if (infection == null)
                {
                    Core.Log("InfectionStart -> infection component missing next frame, SepsisRisk not applied.");
                    yield break;
                }

                int count = infection.GetAfflictionsCount();
                if (count <= 0)
                {
                    Core.Log("InfectionStart -> no vanilla infection found next frame, SepsisRisk not applied.");
                    yield break;
                }

                AfflictionBodyArea resolvedBodyArea = requestedBodyArea;
                bool foundExact = false;

                for (int i = 0; i < count; i++)
                {
                    AfflictionBodyArea vanillaArea = infection.GetLocation(i);
                    if (vanillaArea == requestedBodyArea)
                    {
                        resolvedBodyArea = vanillaArea;
                        foundExact = true;
                        break;
                    }
                }

                if (!foundExact)
                {
                    resolvedBodyArea = infection.GetLocation(count - 1);
                    Core.Log($"InfectionStart -> requested area {requestedBodyArea}, resolved vanilla area {resolvedBodyArea}.");
                }

                new SepsisRiskAffliction(resolvedBodyArea).Start();
                Core.Log($"Vanilla infection started on {resolvedBodyArea}, applying SepsisRisk.");
            }
        }

        [HarmonyPatch(typeof(vp_FPSController), nameof(vp_FPSController.GetSlopeMultiplier))]
        internal static class Vp_FPSController_GetSlopeMultiplier_Patch
        {
            [HarmonyPostfix]
            private static void Postfix(ref float __result)
            {
                bool isSprinting = GameManager.GetPlayerManagerComponent()?.PlayerIsSprinting() ?? false;

                if (isSprinting) __result = AfflictionLogic.ApplySprintSpeedPenaltyToFinalMultiplier(__result);
            }
        }

        [HarmonyPatch(typeof(Freezing), nameof(Freezing.CalculateBodyTemperature))]
        internal static class BodyTemperaturePatch
        {
            private static void Postfix(ref float __result)
            {
                __result += AfflictionLogic.GetBodyTemperatureModifierCelsius();
            }
        }

        [HarmonyPatch(typeof(Condition), nameof(Condition.Update))]
        internal static class HealthyConditionRecoveryPatch
        {
            private static void Prefix(Condition __instance, out float __state)
            {
                __state = 0f;

                if (__instance == null || !AfflictionLogic.ShouldDisableNaturalConditionRecovery())
                    return;

                __state = __instance.m_HPIncreasePerDayWhileHealthy;
                __instance.m_HPIncreasePerDayWhileHealthy = 0f;
            }

            private static void Postfix(Condition __instance, float __state)
            {
                if (__instance == null || !AfflictionLogic.ShouldDisableNaturalConditionRecovery())
                    return;

                __instance.m_HPIncreasePerDayWhileHealthy = __state;
            }
        }

        [HarmonyPatch(typeof(Condition), nameof(Condition.Update))]
        internal static class RespiratoryRuntimeEffectsPatch
        {
            private static void Postfix(Condition __instance)
            {
                if (__instance == null || !IsGameplayScene())
                    return;

                ApplyRespiratoryCameraEffects();

                _respiratoryEffectTickTimer += Time.deltaTime;
                if (_respiratoryEffectTickTimer < 1f)
                    return;

                float realSecondsElapsed = _respiratoryEffectTickTimer;
                _respiratoryEffectTickTimer = 0f;

                TimeOfDay? tod = GameManager.GetTimeOfDayComponent();
                if (tod == null)
                    return;

                float gameHoursPassed = tod.GetTODHours(realSecondsElapsed);
                if (gameHoursPassed <= 0f)
                    return;

                UpdateRespiratoryTimedEffects(__instance, gameHoursPassed);
            }
        }

        [HarmonyPatch(typeof(Condition), nameof(Condition.AddHealth), typeof(float), typeof(DamageSource), typeof(bool))]
        internal static class IncomingDamagePatch
        {
            private static void Prefix(ref float hp)
            {
                hp = AfflictionLogic.ApplyIncomingDamageMultiplier(hp);
            }
        }

        [HarmonyPatch(typeof(Rest), nameof(Rest.UpdateFatigue), typeof(float))]
        internal static class SleepFatigueRecoveryPatch
        {
            private static void Prefix(Rest __instance, out float __state)
            {
                __state = 0f;

                if (__instance == null)
                    return;

                __state = __instance.m_ReduceFatiguePerHourRest;
                __instance.m_ReduceFatiguePerHourRest *= AfflictionLogic.GetSleepFatigueRecoveryMultiplier();
            }

            private static void Postfix(Rest __instance, float __state)
            {
                if (__instance == null)
                    return;

                __instance.m_ReduceFatiguePerHourRest = __state;
            }
        }

        [HarmonyPatch(typeof(Rest), nameof(Rest.AllowedToSleepAmount))]
        internal static class AllowedToSleepAmountPatch
        {
            private static void Postfix(Rest __instance, int amount, ref bool __result)
            {
                if (!__result || __instance == null)
                    return;

                int adjustedMaxHours = AfflictionLogic.GetAdjustedMaxSleepHours(__instance.m_MaxHoursSleepPerDay);
                if (amount > adjustedMaxHours)
                    __result = false;
            }
        }

        [HarmonyPatch(typeof(Panel_Rest), nameof(Panel_Rest.GetAdjustedMaxSleep))]
        [HarmonyPriority(Priority.Last)]
        internal static class PanelRest_GetAdjustedMaxSleepPatch
        {
            private static void Postfix(ref int __result)
            {
                __result = AfflictionLogic.GetAdjustedMaxSleepHours(__result);
            }
        }

        [HarmonyPatch(typeof(Panel_Rest), nameof(Panel_Rest.Enable), typeof(bool), typeof(bool))]
        [HarmonyPriority(Priority.Last)]
        internal static class PanelRest_EnablePatch
        {
            private static void Postfix(Panel_Rest __instance, bool enable, bool passTimeOnly)
            {
                if (!enable || passTimeOnly)
                    return;

                SyncRestPanelSleepLimit(__instance);
            }
        }

        private static void SyncRestPanelSleepLimit(Panel_Rest panel)
        {
            if (panel == null)
                return;

            if (panel.IsPassingTimeOnly())
                return;

            int finalAdjustedMax = Mathf.Max(panel.m_MinSleepHours, panel.GetAdjustedMaxSleep());

            bool changed = false;

            if (panel.m_CurrentMaxSleepHours != finalAdjustedMax)
            {
                panel.m_CurrentMaxSleepHours = finalAdjustedMax;
                changed = true;
            }

            int clampedSleepHours = Mathf.Clamp(panel.m_SleepHours, panel.m_MinSleepHours, finalAdjustedMax);
            if (panel.m_SleepHours != clampedSleepHours)
            {
                panel.m_SleepHours = clampedSleepHours;
                changed = true;
            }

            if (!changed)
                return;

            panel.UpdateRestDurationLabel();
            panel.UpdateWakeTimeLabel();
            panel.UpdateEstimatedCaloriesBurnedLabel();
        }

        // -------------------------------------natural HP regen disabled for sepsis---------------------------------------
        [HarmonyPatch(typeof(Condition), nameof(Condition.MaybeIncreaseConditionFromWillpower))]
        internal static class Condition_MaybeIncreaseConditionFromWillpower
        {
            private static bool Prefix()
            {
                return !SepsisAffliction.IsActive && !AfflictionLogic.ShouldDisableNaturalConditionRecovery();
            }
        }
    }
}