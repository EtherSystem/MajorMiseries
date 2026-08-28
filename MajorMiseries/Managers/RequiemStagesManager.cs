using AfflictionComponent.Components;
using MajorMiseries.Persistence;
using static MajorMiseries.Afflictions.RequiemStagesAfflictions.Dirge;
using static MajorMiseries.Afflictions.RequiemStagesAfflictions.Knell;
using static MajorMiseries.Afflictions.RequiemStagesAfflictions.Omen;
using static MajorMiseries.Afflictions.RequiemStagesAfflictions.Requiem;

namespace MajorMiseries
{
    internal static partial class AfflictionLogic
    {
        // ============================================================================
        //                              Requiem stages
        // ============================================================================

        private static readonly HashSet<RequiemStage> s_DevSuppressedStages = [];

        internal static void ResetRequiemDevSuppressions()
        {
            s_DevSuppressedStages.Clear();
        }

        internal static void DevUnsuppressStage(RequiemStage stage)
        {
            s_DevSuppressedStages.Remove(stage);
        }

        internal static void DevSuppressAndCureStage(RequiemStage stage)
        {
            s_DevSuppressedStages.Add(stage);
            ApplyCurrentStageFromGame();
            AfflictionSaveHelper.QueueSurvivalSave();
            Core.Log($"DEV: Requiem stage cured and suppressed for the current runtime -> {stage}.");
        }

        internal static RequiemStage GetCurrentStage()
        {
            RefreshEffectsIfNeeded();

            if (_cache.Requiem) return RequiemStage.Requiem;
            if (_cache.Knell) return RequiemStage.Knell;
            if (_cache.Dirge) return RequiemStage.Dirge;
            if (_cache.Omen) return RequiemStage.Omen;

            return RequiemStage.None;
        }

        private static RequiemStage GetStageFromTotalHours(int totalHours)
        {
            float daysAlive = totalHours / 24f;

            GetConfiguredStageThresholds(out float omenThreshold, out float dirgeThreshold, out float knellThreshold, out float requiemThreshold);

            if (daysAlive >= requiemThreshold && !s_DevSuppressedStages.Contains(RequiemStage.Requiem)) return RequiemStage.Requiem;
            if (daysAlive >= knellThreshold && !s_DevSuppressedStages.Contains(RequiemStage.Knell)) return RequiemStage.Knell;
            if (daysAlive >= dirgeThreshold && !s_DevSuppressedStages.Contains(RequiemStage.Dirge)) return RequiemStage.Dirge;
            if (daysAlive >= omenThreshold && !s_DevSuppressedStages.Contains(RequiemStage.Omen)) return RequiemStage.Omen;

            return RequiemStage.None;
        }

        private static void GetConfiguredStageThresholds(out float omenThreshold, out float dirgeThreshold, out float knellThreshold, out float requiemThreshold)
        {
            omenThreshold = Settings.options.OmenThreshold;
            dirgeThreshold = Mathf.Max(omenThreshold, Settings.options.DirgeThreshold);
            knellThreshold = Mathf.Max(dirgeThreshold, Settings.options.KnellThreshold);
            requiemThreshold = Mathf.Max(knellThreshold, Settings.options.RequiemThreshold);
        }

        private static RequiemStage GetAppliedStage()
        {
            if (HasAffliction<RequiemAffliction>()) return RequiemStage.Requiem;
            if (HasAffliction<KnellAffliction>()) return RequiemStage.Knell;
            if (HasAffliction<DirgeAffliction>()) return RequiemStage.Dirge;
            if (HasAffliction<OmenAffliction>()) return RequiemStage.Omen;

            return RequiemStage.None;
        }

        private static void ApplyStage(RequiemStage stage)
        {
            if (!Settings.options.EnableRequiemStages) return;

            switch (stage)
            {
                case RequiemStage.Omen:
                    new OmenAffliction(AfflictionBodyArea.Head).Start();
                    AfflictionSaveHelper.QueueSurvivalSave();
                    Core.Log("omen applied");
                    break;

                case RequiemStage.Dirge:
                    new DirgeAffliction(AfflictionBodyArea.Head).Start();
                    AfflictionSaveHelper.QueueSurvivalSave();
                    Core.Log("dirge applied");
                    break;

                case RequiemStage.Knell:
                    new KnellAffliction(AfflictionBodyArea.Head).Start();
                    AfflictionSaveHelper.QueueSurvivalSave();
                    Core.Log("knell applied");
                    break;

                case RequiemStage.Requiem:
                    new RequiemAffliction(AfflictionBodyArea.Head).Start();
                    AfflictionSaveHelper.QueueSurvivalSave();
                    Core.Log("requiem applied");
                    break;
            }
        }

        private static void ClampConditionToCurrentStageMax(RequiemStage stage)
        {
            if (stage == RequiemStage.None) return;

            Condition? condition = GameManager.GetConditionComponent();
            if (condition == null) return;

            float before = condition.m_CurrentHP;

            float adjustedMaxHp = Mathf.Max(1f, VANILLA_MAX_CONDITION_HP + condition.GetAdjustedMaxHPModifier());

            if (before <= adjustedMaxHp) return;

            condition.m_CurrentHP = adjustedMaxHp;

            Core.Log($"stage condition clamp -> {stage}: {before:0.##} -> {condition.m_CurrentHP:0.##} / {adjustedMaxHp:0.##}");
        }

        private static void CureAllStageAfflictions()
        {
            var mgr = AfflictionManager.GetAfflictionManagerInstance();
            if (mgr?.m_Afflictions == null) return;

            for (int i = mgr.m_Afflictions.Count - 1; i >= 0; i--)
            {
                var affliction = mgr.m_Afflictions[i];
                if (affliction == null) continue;

                if (affliction is OmenAffliction || affliction is DirgeAffliction || affliction is KnellAffliction || affliction is RequiemAffliction)
                {
                    affliction.Cure();
                }
            }
        }

        private static int GetPopupStageNumber(RequiemStage stage)
        {
            return stage switch
            {
                RequiemStage.Omen => 1,
                RequiemStage.Dirge => 2,
                RequiemStage.Knell => 3,
                RequiemStage.Requiem => 4,
                _ => 0
            };
        }

        private static string? GetPopupStageLocId(RequiemStage stage)
        {
            return stage switch
            {
                RequiemStage.Omen => "GAMEPLAY_Stage1Name",
                RequiemStage.Dirge => "GAMEPLAY_Stage2Name",
                RequiemStage.Knell => "GAMEPLAY_Stage3Name",
                RequiemStage.Requiem => "GAMEPLAY_Stage4Name",
                _ => null
            };
        }
    }
}