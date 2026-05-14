using MajorMiseries.Persistence;

namespace MajorMiseries
{
    internal static partial class AfflictionLogic
    {
        // =======================================================================================
        //                              PredatorHostility Logic
        // =======================================================================================

        private const float PREDATOR_HOSTILITY_DECAY_DELAY_HOURS = 168f; // a week
        private const float PREDATOR_HOSTILITY_DECAY_PER_DAY = 2f;
        private const float PREDATOR_HOSTILITY_DECAY_PER_HOUR = PREDATOR_HOSTILITY_DECAY_PER_DAY / 24f;

        internal static bool IsPredatorHostilityEnabled()
        {
            RequiemStage stage = GetCurrentStage();
            return RequiemStagesEffects.IsPredatorHostilityEnabled(stage);
        }

        internal static int GetBasePredatorThreatLevel()
        {
            RequiemStage stage = GetCurrentStage();
            return RequiemStagesEffects.GetBasePredatorThreat(stage);
        }

        private static int GetDynamicPredatorThreatLevelFromHeat(float heat)
        {
            if (heat >= 30f) return 8;
            if (heat >= 25f) return 7;
            if (heat >= 20f) return 6;
            if (heat >= 16f) return 5;
            if (heat >= 12f) return 4;
            if (heat >= 9f) return 3;
            if (heat >= 6f) return 2;
            if (heat >= 3f) return 1;
            return 0;
        }

        internal static int GetDynamicPredatorThreatLevel()
        {
            return GetDynamicPredatorThreatLevelFromHeat(Core.State.PredatorHostility);
        }

        internal static int GetTotalPredatorThreatLevel()
        {
            if (!IsPredatorHostilityEnabled()) return 0;

            return GetBasePredatorThreatLevel() + GetDynamicPredatorThreatLevel();
        }

        internal static void RegisterPredatorKill(float hostilityAdded)
        {
            if (hostilityAdded <= 0f) return;

            if (!IsPredatorHostilityEnabled()) return;

            float before = Core.State.PredatorHostility;

            Core.State.PredatorHostility += hostilityAdded;
            Core.State.HoursSinceLastPredatorKill = 0f;

            Core.Instance?.MarkDirty();

            Core.Log($"predator kill -> hostility +{hostilityAdded:0.##} ({before:0.##} -> {Core.State.PredatorHostility:0.##})");
        }

        internal static float GetPredatorSmellDistanceMultiplier()
        {
            int threat = GetTotalPredatorThreatLevel();
            return threat > 0 ? 1f + (0.2f * threat) : 1f;
        }

        internal static float GetPredatorRushDistanceMultiplier()
        {
            int threat = GetTotalPredatorThreatLevel();
            return threat > 0 ? 1f + (0.2f * threat) : 1f;
        }

        internal static int GetAdditionalPredatorSpawnQuantity()
        {
            int threat = GetTotalPredatorThreatLevel();
            return threat > 0 ? threat / 2 : 0;
        }

        internal static void UpdatePredatorHostilityDecay(float gameHoursPassed)
        {
            if (gameHoursPassed <= 0f) return;

            if (Core.State.PredatorHostility <= 0f)
            {
                Core.State.PredatorHostility = 0f;
                Core.State.HoursSinceLastPredatorKill = 0f;
                return;
            }

            Core.State.HoursSinceLastPredatorKill += gameHoursPassed;
            Core.Instance?.MarkDirty();

            if (Core.State.HoursSinceLastPredatorKill < PREDATOR_HOSTILITY_DECAY_DELAY_HOURS) return;

            float before = Core.State.PredatorHostility;
            int dynamicThreatBefore = GetDynamicPredatorThreatLevelFromHeat(before);

            float decay = PREDATOR_HOSTILITY_DECAY_PER_HOUR * gameHoursPassed;

            Core.State.PredatorHostility = Mathf.Max(0f, Core.State.PredatorHostility - decay);

            if (!Mathf.Approximately(before, Core.State.PredatorHostility))
            {
                Core.Instance?.MarkDirty();

                int dynamicThreatAfter = GetDynamicPredatorThreatLevelFromHeat(Core.State.PredatorHostility);

                if (dynamicThreatBefore != dynamicThreatAfter || Core.State.PredatorHostility <= 0f)
                {
                    Core.Log($"predator hostility decay -> {before:0.##} -> {Core.State.PredatorHostility:0.##} | DynamicThreat:{dynamicThreatBefore}->{dynamicThreatAfter}");
                }
            }

            if (Core.State.PredatorHostility <= 0f)
            {
                Core.State.PredatorHostility = 0f;
                Core.State.HoursSinceLastPredatorKill = 0f;
                Core.Instance?.MarkDirty();
            }
        }
    }
}