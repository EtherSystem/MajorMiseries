using AfflictionComponent.Components;
using AfflictionComponent.Enums;
using AfflictionComponent.Interfaces;
using MajorMiseries.Managers;
using MajorMiseries.Resources.Localization;

namespace MajorMiseries.Afflictions
{
    internal class DeepNecrosis
    {
        public class DeepNecrosisAffliction : CustomAffliction, IRemedies, IInstance, ILocalizableAffliction
        {
            private const string NAME_KEY = "GAMEPLAY_DeepNecrosisName";
            private const string CAUSE_KEY = "GAMEPLAY_DeepNecrosisCause";
            private const string DESC_KEY = "GAMEPLAY_DeepNecrosisDescription";

            private const string HEAD_ICON = "MajorMiseries.Resources.Icons.Afflictions.Classic.DeepNecrosisHead.png";
            private const string ARM_LEFT_ICON = "MajorMiseries.Resources.Icons.Afflictions.Classic.DeepNecrosisArm_Left.png";
            private const string ARM_RIGHT_ICON = "MajorMiseries.Resources.Icons.Afflictions.Classic.DeepNecrosisArm_Right.png";
            private const string LEG_LEFT_ICON = "MajorMiseries.Resources.Icons.Afflictions.Classic.DeepNecrosisLeg_Left.png";
            private const string LEG_RIGHT_ICON = "MajorMiseries.Resources.Icons.Afflictions.Classic.DeepNecrosisLeg_Right.png";

            private const string HEAD_ALT_ICON = "MajorMiseries.Resources.Icons.Afflictions.Alt.DeepNecrosisHead_ALT.png";
            private const string ARM_LEFT_ALT_ICON = "MajorMiseries.Resources.Icons.Afflictions.Alt.DeepNecrosisArm_Left_ALT.png";
            private const string ARM_RIGHT_ALT_ICON = "MajorMiseries.Resources.Icons.Afflictions.Alt.DeepNecrosisArm_Right_ALT.png";
            private const string LEG_LEFT_ALT_ICON = "MajorMiseries.Resources.Icons.Afflictions.Alt.DeepNecrosisLeg_Left_ALT.png";
            private const string LEG_RIGHT_ALT_ICON = "MajorMiseries.Resources.Icons.Afflictions.Alt.DeepNecrosisLeg_Right_ALT.png";

            public const float FATIGUE_INCREASE_MULTIPLIER = 1.5f;
            public const float INITIAL_CONDITION_LOSS_PER_HOUR = 0.1f;
            public const float LIMB_CONDITION_LOSS_INCREASE_PER_HOUR = 0.1f;
            public const float HEAD_CONDITION_LOSS_INCREASE_PER_HOUR = 0.2f;

            public InstanceType Type { get; set; } = InstanceType.SingleLocation;

            public Tuple<string, int, int>[] RemedyItems { get; set; } = [];
            public Tuple<string, int, int>[] AltRemedyItems { get; set; } = [];
            public bool InstantHeal { get; set; } = false;

            public AfflictionBodyArea SourceExtremity { get; set; }
            public float ElapsedHours { get; set; } = 0f;
            public float LastUpdateTime { get; set; }

            public DeepNecrosisAffliction(AfflictionBodyArea sourceExtremity) : base(NAME_KEY, CAUSE_KEY, DESC_KEY, null, GetIcon(NecrosisManager.GetDeepNecrosisLocation(sourceExtremity)), NecrosisManager.GetDeepNecrosisLocation(sourceExtremity), true)
            {
                SourceExtremity = sourceExtremity;

                TimeOfDay? timeOfDay = GameManager.GetTimeOfDayComponent();
                LastUpdateTime = timeOfDay != null ? timeOfDay.GetHoursPlayedNotPaused() : 0f;
            }

            private static string GetIcon(AfflictionBodyArea bodyArea)
            {
                bool useAlt = UnityEngine.Random.Range(0f, 100f) < Settings.options.AltAfflictionIconChance;

                return bodyArea switch
                {
                    AfflictionBodyArea.ArmLeft => useAlt ? ARM_LEFT_ALT_ICON : ARM_LEFT_ICON,
                    AfflictionBodyArea.ArmRight => useAlt ? ARM_RIGHT_ALT_ICON : ARM_RIGHT_ICON,
                    AfflictionBodyArea.LegLeft => useAlt ? LEG_LEFT_ALT_ICON : LEG_LEFT_ICON,
                    AfflictionBodyArea.LegRight => useAlt ? LEG_RIGHT_ALT_ICON : LEG_RIGHT_ICON,
                    _ => useAlt ? HEAD_ALT_ICON : HEAD_ICON,
                };
            }

            public void OnFoundExistingInstance(CustomAffliction existingAffliction)
            {
                return;
            }

            protected override bool ApplyRemedyCondition()
            {
                return false;
            }

            public void CureSymptoms()
            {
            }

            public void OnCure()
            {
                AfflictionSaveHelper.QueueSurvivalSave();
            }

            public override void OnUpdate()
            {

                if (!Settings.options.EnableNecrosis)
                {
                    Cure();
                    return;
                }

                TimeOfDay? timeOfDay = GameManager.GetTimeOfDayComponent();
                if (timeOfDay == null) return;

                float now = timeOfDay.GetHoursPlayedNotPaused();
                float elapsedDelta = now - LastUpdateTime;
                if (elapsedDelta <= 0f) return;

                float previousElapsed = Mathf.Max(0f, ElapsedHours);
                float currentElapsed = previousElapsed + elapsedDelta;
                float conditionLoss = GetCumulativeConditionLoss(currentElapsed) - GetCumulativeConditionLoss(previousElapsed);

                ElapsedHours = currentElapsed;
                LastUpdateTime = now;

                Condition? condition = GameManager.GetConditionComponent();
                if (condition == null || conditionLoss <= 0f) return;

                float appliedLoss = AfflictionLogic.ApplyCompleteChunkedConditionDrain(condition, conditionLoss);
                bool crossedWholeHour = Mathf.FloorToInt(currentElapsed) > Mathf.FloorToInt(previousElapsed);

                if (appliedLoss > 0f && (crossedWholeHour || elapsedDelta >= 1f))
                {
                    Core.Log($"Deep Necrosis [{m_Location}] -> {appliedLoss:0.###} condition lost over {elapsedDelta:0.###}h; elapsed={ElapsedHours:0.###}h, current rate={GetCurrentConditionLossPerHour():0.###}/h.");
                }
            }

            public float GetCurrentConditionLossPerHour()
            {
                float increment = GetConditionLossIncreasePerHour();
                int completedHours = Mathf.Max(0, Mathf.FloorToInt(ElapsedHours));
                return INITIAL_CONDITION_LOSS_PER_HOUR + increment * completedHours;
            }

            private float GetCumulativeConditionLoss(float elapsedHours)
            {
                float clampedHours = Mathf.Max(0f, elapsedHours);
                int completedHours = Mathf.FloorToInt(clampedHours);
                float partialHour = clampedHours - completedHours;
                float increment = GetConditionLossIncreasePerHour();

                float completedLoss = completedHours * INITIAL_CONDITION_LOSS_PER_HOUR
                    + increment * completedHours * (completedHours - 1) * 0.5f;

                float currentHourlyRate = INITIAL_CONDITION_LOSS_PER_HOUR + increment * completedHours;
                return completedLoss + partialHour * currentHourlyRate;
            }

            private float GetConditionLossIncreasePerHour()
            {
                return m_Location == AfflictionBodyArea.Head
                    ? HEAD_CONDITION_LOSS_INCREASE_PER_HOUR
                    : LIMB_CONDITION_LOSS_INCREASE_PER_HOUR;
            }

            public void RefreshLocalization()
            {
                string oldName = m_Name;

                m_Name = Localization.Get(NAME_KEY);
                m_CauseText = Localization.Get(CAUSE_KEY);
                m_Description = Localization.Get(DESC_KEY);
                m_DescriptionNoHeal = null;

                Core.Log($"DeepNecrosis refresh -> '{oldName}' => '{m_Name}'");
            }
        }
    }
}
