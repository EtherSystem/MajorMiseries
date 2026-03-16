using MajorMiseries.Persistence;

namespace MajorMiseries
{
    internal static class RequiemStagesEffects
    {
        internal readonly struct StageProfile
        {
            public readonly float MaxHpPenalty;
            public readonly int BasePredatorThreat;
            public readonly bool ConvertPredatorBloodLossToSevere;
            public readonly bool PredatorHostilityEnabled;

            public StageProfile(
                float maxHpPenalty,
                int basePredatorThreat,
                bool convertPredatorBloodLossToSevere,
                bool predatorHostilityEnabled)
            {
                MaxHpPenalty = maxHpPenalty;
                BasePredatorThreat = basePredatorThreat;
                ConvertPredatorBloodLossToSevere = convertPredatorBloodLossToSevere;
                PredatorHostilityEnabled = predatorHostilityEnabled;
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
                    predatorHostilityEnabled: true),

                RequiemStage.Dirge => new StageProfile(
                    maxHpPenalty: 20f,
                    basePredatorThreat: 2,
                    convertPredatorBloodLossToSevere: false,
                    predatorHostilityEnabled: true),

                RequiemStage.Knell => new StageProfile(
                    maxHpPenalty: 30f,
                    basePredatorThreat: 3,
                    convertPredatorBloodLossToSevere: false,
                    predatorHostilityEnabled: true),

                RequiemStage.Requiem => new StageProfile(
                    maxHpPenalty: 50f,
                    basePredatorThreat: 4,
                    convertPredatorBloodLossToSevere: true,
                    predatorHostilityEnabled: true),

                _ => new StageProfile(
                    maxHpPenalty: 0f,
                    basePredatorThreat: 0,
                    convertPredatorBloodLossToSevere: false,
                    predatorHostilityEnabled: false)
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
            return GetProfile(stage).ConvertPredatorBloodLossToSevere;
        }

        internal static bool IsPredatorHostilityEnabled(RequiemStage stage)
        {
            return GetProfile(stage).PredatorHostilityEnabled;
        }
    }
}