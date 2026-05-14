using MajorMiseries.Managers;
using MajorMiseries.Persistence;

namespace MajorMiseries
{
    internal static partial class AfflictionLogic
    {
        // ==========================================================================
        //    Afflictions Effects (see AfflictionEffects.cs for Harmony patches)
        // ==========================================================================

        internal static float GetMaxHpPenalty()
        {
            RefreshEffectsIfNeeded();

            float penalty = 0f;

            if (_cache.Requiem) penalty += 50f;
            else if (_cache.Knell) penalty += 30f;
            else if (_cache.Dirge) penalty += 20f;
            else if (_cache.Omen) penalty += 10f;

            penalty += 2f * GetScarredFleshCount();

            return penalty;
        }

        internal static bool HasStageEffect(RequiemStage minimumStage)
        {
            return GetCurrentStage() >= minimumStage;
        }

        internal static float GetSleepFatigueRecoveryMultiplier()
        {
            RefreshEffectsIfNeeded();

            float multiplier = HasStageEffect(RequiemStage.Omen) ? 0.5f : 1f;

            if (_cache.HomeComfort) multiplier *= RegionalAfflictionManager.HOME_COMFORT_SLEEP_RECOVERY_MULTIPLIER;

            if (_cache.HomeSickness) multiplier *= RegionalAfflictionManager.HOME_SICKNESS_SLEEP_RECOVERY_MULTIPLIER;

            if (_cache.RegionalDistress) multiplier *= RegionalAfflictionManager.REGIONAL_DISTRESS_SLEEP_RECOVERY_MULTIPLIER;

            return multiplier;
        }

        internal static int GetAdjustedMaxSleepHours(int vanillaMaxHours)
        {
            if (!HasStageEffect(RequiemStage.Omen)) return vanillaMaxHours;

            return Mathf.Max(1, vanillaMaxHours - 4);
        }

        internal static float GetBodyTemperatureModifierCelsius()
        {
            return HasStageEffect(RequiemStage.Dirge) ? -5f : 0f;
        }

        internal static bool ShouldDisableNaturalConditionRecovery()
        {
            return HasStageEffect(RequiemStage.Requiem);
        }

        internal static float ApplyIncomingDamageMultiplier(float healthDelta)
        {
            if (_applyingSevere && healthDelta < 0f)
            {
                Core.Log($"blocked condition damage during SevereLacerations conversion -> hpDelta:{healthDelta:0.###}");
                return 0f;
            }

            if (!Settings.options.EnableRequiemStages) return healthDelta;
            if (!HasStageEffect(RequiemStage.Requiem) || healthDelta >= 0f) return healthDelta;

            return healthDelta * 2f;
        }

        internal static float ApplyChunkedConditionDrain(Condition condition, float rawHpLoss, DamageSource damageSource = DamageSource.Unspecified)
        {
            if (condition == null || rawHpLoss <= 0f) return 0f;

            if (condition.m_CurrentHP <= 0f) return 0f;

            float damageMultiplier = Mathf.Abs(ApplyIncomingDamageMultiplier(-1f));
            if (damageMultiplier <= 0f) damageMultiplier = 1f;

            const float SAFE_EFFECTIVE_CHUNK = 0.02f;

            float safeRawChunk = SAFE_EFFECTIVE_CHUNK / damageMultiplier;
            if (safeRawChunk <= 0f) safeRawChunk = 0.01f;

            float remainingRawLoss = rawHpLoss;
            float appliedEffectiveLoss = 0f;

            int chunks = 0;
            const int MAX_CHUNKS_PER_TICK = 128;

            while (remainingRawLoss > 0.0001f && condition.m_CurrentHP > 0f)
            {
                float rawChunk = Mathf.Min(remainingRawLoss, safeRawChunk);

                float hpBefore = condition.m_CurrentHP;

                condition.AddHealth(-rawChunk, damageSource);

                float hpAfter = condition.m_CurrentHP;
                float actualLoss = Mathf.Max(0f, hpBefore - hpAfter);

                appliedEffectiveLoss += actualLoss;
                remainingRawLoss -= rawChunk;

                chunks++;

                if (chunks >= MAX_CHUNKS_PER_TICK)
                {
                    Core.Log($"[ChunkedConditionDrain] Aborted after {MAX_CHUNKS_PER_TICK} chunks | " + $"rawHpLoss:{rawHpLoss:0.#####} | " + $"remainingRaw:{remainingRawLoss:0.#####} | " + $"safeRawChunk:{safeRawChunk:0.#####} | " + $"damageMultiplier:{damageMultiplier:0.###}");

                    break;
                }
            }

            return appliedEffectiveLoss;
        }

        internal static float ApplySprintSpeedPenaltyToFinalMultiplier(float multiplier)
        {
            if (!HasStageEffect(RequiemStage.Knell)) return multiplier;

            if (HasWeakJoints()) return multiplier;

            return multiplier * 0.75f;
        }

        internal static bool ShouldBlockSprint()
        {
            RefreshEffectsIfNeeded();

            return _cache.BrokenLegCount > 0 || _cache.SevereAnkleSprainCount > 0;
        }

        internal static bool ShouldBlockClimbing()
        {
            RefreshEffectsIfNeeded();

            return _cache.BrokenLegCount > 0 || _cache.BrokenArmCount > 0 || _cache.SevereAnkleSprainCount > 0 || _cache.SevereWristSprainCount > 0;
        }

        internal static float GetMovementSpeedMultiplier()
        {
            RefreshEffectsIfNeeded();

            float multiplier = 1f;

            if (_cache.BrokenLegLeft && _cache.BrokenLegRight) multiplier *= 0.45f;
            else if (_cache.BrokenLegCount > 0) multiplier *= 0.65f;
            else if (_cache.SevereAnkleSprainCount > 0) multiplier *= 0.75f;

            if (HasStageEffect(RequiemStage.Knell)) multiplier *= 0.9f;

            return multiplier;
        }

        internal static float GetMovementFatigueMultiplier()
        {
            RefreshEffectsIfNeeded();

            float multiplier = 1f;

            if (_cache.BrokenLegLeft && _cache.BrokenLegRight) multiplier *= 2.0f;
            else if (_cache.BrokenLegCount > 0) multiplier *= 1.5f;
            else if (_cache.SevereAnkleSprainCount > 0) multiplier *= 1.35f;

            if (_cache.HomeComfort) multiplier *= RegionalAfflictionManager.HOME_COMFORT_MOVEMENT_FATIGUE_MULTIPLIER;

            if (_cache.HomeSickness) multiplier *= RegionalAfflictionManager.HOME_SICKNESS_MOVEMENT_FATIGUE_MULTIPLIER;

            if (_cache.RegionalDistress) multiplier *= RegionalAfflictionManager.REGIONAL_DISTRESS_MOVEMENT_FATIGUE_MULTIPLIER;

            return multiplier;
        }

        internal static float GetCraftingTimeMultiplier()
        {
            RefreshEffectsIfNeeded();

            float multiplier = 1f;

            if (_cache.BrokenArmLeft && _cache.BrokenArmRight) multiplier = Mathf.Max(multiplier, 2.0f);
            else if (_cache.BrokenArmCount > 0) multiplier = Mathf.Max(multiplier, 1.5f);

            if (_cache.SevereWristSprainCount > 0) multiplier = Mathf.Max(multiplier, 1.35f);

            return multiplier;
        }

        internal static float GetAimSwayIncreaseMultiplier()
        {
            RefreshEffectsIfNeeded();

            float multiplier = 1f;

            if (_cache.BrokenArmLeft && _cache.BrokenArmRight) multiplier = Mathf.Max(multiplier, 3f);
            else if (_cache.BrokenArmCount > 0) multiplier = Mathf.Max(multiplier, 2f);

            if (_cache.SevereWristSprainCount > 0) multiplier = Mathf.Max(multiplier, 2.5f);

            return multiplier;
        }

        internal static float GetAimSwayDecreaseMultiplier()
        {
            RefreshEffectsIfNeeded();

            float multiplier = 1f;

            if (_cache.BrokenArmLeft && _cache.BrokenArmRight) multiplier = Mathf.Min(multiplier, 0.45f);
            else if (_cache.BrokenArmCount > 0) multiplier = Mathf.Min(multiplier, 0.65f);

            if (_cache.SevereWristSprainCount > 0) multiplier = Mathf.Min(multiplier, 0.5f);

            return multiplier;
        }
    }
}