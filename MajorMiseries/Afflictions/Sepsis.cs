using AfflictionComponent.Components;
using AfflictionComponent.Enums;
using AfflictionComponent.Interfaces;
using MajorMiseries.Resources.Localization;

namespace MajorMiseries.Afflictions
{
    internal class Sepsis
    {
        public class SepsisAffliction : CustomAffliction, IDuration, IRemedies, IInstance, ILocalizableAffliction
        {
            private const string NAME_KEY = "GAMEPLAY_SepsisName";
            private const string CAUSE_KEY = "GAMEPLAY_SepsisCause";
            private const string DESC_KEY = "GAMEPLAY_SepsisDescription";

            private const string ICON = "MajorMiseries.Resources.Icons.Afflictions.Classic.Sepsis.png";
            private const string ALT_ICON = "MajorMiseries.Resources.Icons.Afflictions.Alt.Sepsis_ALT.png";

            public const float TOTAL_DURATION_HOURS = 480f;
            public const int ANTIBIOTIC_BOTTLES_PER_INTAKE = 2;

            public const float UNTREATED_CONDITION_LOSS_PER_HOUR = 10f;
            public const float TREATED_CONDITION_LOSS_PER_HOUR = 0.2f;

            private float m_SuppressedUntilTime = -1f;
            private bool m_HasShownDoseExpiredMessage = false;
            private float m_LastWholeMinute = -1f;
            private const float DRAIN_LOG_INTERVAL_MINUTES = 60f;

            private float m_DrainLogHpLoss = 0f;
            private float m_DrainLogMinutes = 0f;

            public static bool IsActive { get; private set; } = false;

            public InstanceType Type { get; set; } = InstanceType.Single;

            public float Duration { get; set; } = TOTAL_DURATION_HOURS;
            public float EndTime { get; set; }

            public Tuple<string, int, int>[] RemedyItems { get; set; } = [];

            public Tuple<string, int, int>[] AltRemedyItems { get; set; } = [];

            public bool InstantHeal { get; set; } = false;

            public SepsisAffliction(AfflictionBodyArea bodyArea) : base(NAME_KEY, CAUSE_KEY, DESC_KEY, null, UnityEngine.Random.Range(0f, 100f) < Settings.options.AltAfflictionIconChance ? ALT_ICON : ICON, bodyArea, true)
            {
                float now = GameManager.GetTimeOfDayComponent().GetHoursPlayedNotPaused();
                EndTime = now + Duration;

                ResetRemedyItemsForCurrentPreset();
            }

            public void OnFoundExistingInstance(CustomAffliction existingAffliction)
            {
                return;
            }

            protected override bool ApplyRemedyCondition()
            {
                return !IsTreatmentActive();
            }

            public void CureSymptoms()
            {
                EnsureRemedyItemsMatchPreset();
                ConsumeExtraAntibioticBottleForDisplay();

                if (NeedsRemedy())
                {
                    int remainingTablets = GetRemainingAntibioticBottles();
                    int requiredTablets = GetRequiredAntibioticBottleDisplayCount();
                    int takenTablets = requiredTablets - remainingTablets;
                    int takenIntakes = takenTablets / ANTIBIOTIC_BOTTLES_PER_INTAKE;
                    int requiredIntakes = AfflictionLogic.GetSepsisRequiredDoseIntakesPerWindow();

                    HUDMessage.AddMessage($"Sepsis treatment progress: {takenTablets}/{requiredTablets} antibiotics.");
                    Core.Log($"Sepsis treatment progress: {takenTablets}/{requiredTablets} tablets ({takenIntakes}/{requiredIntakes} dose intakes). Instance:{GetHashCode()}");
                    return;
                }

                FlushDrainLog();

                float now = GameManager.GetTimeOfDayComponent().GetHoursPlayedNotPaused();
                float treatmentDurationHours = AfflictionLogic.GetSepsisTreatmentWindowIntervalHours();
                m_SuppressedUntilTime = now + treatmentDurationHours;
                m_HasShownDoseExpiredMessage = false;

                HUDMessage.AddMessage($"Sepsis treatment applied for {treatmentDurationHours / 24f:0.#} days.");
                Core.Log($"Sepsis treatment applied until hour {m_SuppressedUntilTime:0.##}. Required intakes:{AfflictionLogic.GetSepsisRequiredDoseIntakesPerWindow()}, interval:{treatmentDurationHours:0.##}h.");
            }

            public void OnCure()
            {
                FlushDrainLog();

                IsActive = false;
                ResetRemedyItemsForCurrentPreset();
                AfflictionSaveHelper.QueueSurvivalSave();
            }

            public override void OnUpdate()
            {
                Panel_FirstAid firstAid = InterfaceManager.GetPanel<Panel_FirstAid>();
                if (firstAid != null && firstAid.isActiveAndEnabled)
                    return;

                IsActive = true;

                float now = GameManager.GetTimeOfDayComponent().GetHoursPlayedNotPaused();

                if (now >= EndTime)
                {
                    Core.Log("Sepsis duration completed, curing affliction.");
                    Cure();
                    return;
                }

                EnsureRemedyItemsMatchPreset();
                ApplyHealthDrain();

                if (!IsTreatmentActive(now) && m_SuppressedUntilTime > 0f && !m_HasShownDoseExpiredMessage)
                {
                    FlushDrainLog();

                    m_HasShownDoseExpiredMessage = true;
                    m_SuppressedUntilTime = -1f;

                    ResetRemedyItemsForCurrentPreset();

                    HUDMessage.AddMessage("Sepsis treatment expired. Symptoms are back at full strength.");
                    Core.Log("Sepsis treatment window expired, remedy progress reset.");
                }
            }

            private void EnsureRemedyItemsMatchPreset()
            {
                if (IsTreatmentActive()) return;

                int required = GetRequiredAntibioticBottleDisplayCount();
                if (RemedyItems != null && RemedyItems.Length > 0 && RemedyItems[0].Item2 == required) return;

                ResetRemedyItemsForCurrentPreset();
            }

            private void ResetRemedyItemsForCurrentPreset()
            {
                int required = GetRequiredAntibioticBottleDisplayCount();
                RemedyItems =
                [
                    Tuple.Create("GEAR_BottleAntibiotics", required, required),
                ];
            }

            private static int GetRequiredAntibioticBottleDisplayCount()
            {
                return AfflictionLogic.GetSepsisRequiredDoseIntakesPerWindow() * ANTIBIOTIC_BOTTLES_PER_INTAKE;
            }

            private int GetRemainingAntibioticBottles()
            {
                if (RemedyItems == null || RemedyItems.Length <= 0)
                    return 0;

                return Mathf.Max(0, RemedyItems[0].Item3);
            }

            private void ConsumeExtraAntibioticBottleForDisplay()
            {
                if (RemedyItems == null || RemedyItems.Length <= 0)
                    return;

                var current = RemedyItems[0];

                int remaining = Mathf.Max(0, current.Item3 - (ANTIBIOTIC_BOTTLES_PER_INTAKE - 1));

                RemedyItems[0] = Tuple.Create(
                    current.Item1,
                    current.Item2,
                    remaining
                );
            }

            public bool IsTreatmentActive()
            {
                float now = GameManager.GetTimeOfDayComponent().GetHoursPlayedNotPaused();
                return IsTreatmentActive(now);
            }

            private bool IsTreatmentActive(float now)
            {
                return now < m_SuppressedUntilTime;
            }

            public float GetCurrentConditionLossPerHour()
            {
                return IsTreatmentActive() ? TREATED_CONDITION_LOSS_PER_HOUR : UNTREATED_CONDITION_LOSS_PER_HOUR;
            }

            private static float GetCurrentWholeMinute()
            {
                TimeOfDay tod = GameManager.GetTimeOfDayComponent();
                if (tod == null)
                    return 0f;

                return Mathf.Floor(tod.GetHoursPlayedNotPaused() * 60f);
            }

            private void ApplyHealthDrain()
            {
                Condition cond = GameManager.GetConditionComponent();
                if (cond == null)
                    return;

                float currentMinute = GetCurrentWholeMinute();

                if (m_LastWholeMinute < 0f)
                {
                    m_LastWholeMinute = currentMinute;
                    return;
                }

                float minuteDelta = currentMinute - m_LastWholeMinute;

                if (minuteDelta <= 0f)
                    return;

                m_LastWholeMinute = currentMinute;

                float hpPerHour = GetCurrentConditionLossPerHour();
                float hpPerMinute = hpPerHour / 60f;
                float hpLoss = hpPerMinute * minuteDelta;

                float appliedHpLoss = AfflictionLogic.ApplyChunkedConditionDrain(cond, hpLoss);

                if (appliedHpLoss <= 0f)
                    return;

                m_DrainLogHpLoss += appliedHpLoss;
                m_DrainLogMinutes += minuteDelta;

                if (m_DrainLogMinutes >= DRAIN_LOG_INTERVAL_MINUTES) FlushDrainLog();
            }

            private void FlushDrainLog()
            {
                if (m_DrainLogMinutes <= 0f || m_DrainLogHpLoss <= 0f)
                    return;

                Core.Log($"Sepsis drain: {m_DrainLogHpLoss:0.###} HP over {m_DrainLogMinutes:0} min.");

                m_DrainLogHpLoss = 0f;
                m_DrainLogMinutes = 0f;
            }

            public void RefreshLocalization()
            {
                string oldName = m_Name;

                m_Name = Localization.Get(NAME_KEY);
                m_CauseText = Localization.Get(CAUSE_KEY);
                m_Description = Localization.Get(DESC_KEY);
                m_DescriptionNoHeal = null;

                Core.Log($"Sepsis refresh -> '{oldName}' => '{m_Name}'");
            }
        }
    }
}
