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
            public const float DOSE_DURATION_HOURS = 120f;
            public const int ANTIBIOTIC_DOSES_PER_TREATMENT = 2;

            public const float UNTREATED_CONDITION_LOSS_PER_HOUR = 2.5f;
            public const float TREATED_CONDITION_LOSS_PER_HOUR = 0.25f;

            private float m_SuppressedUntilTime = -1f;
            private bool m_HasShownDoseExpiredMessage = false;
            private float m_LastWholeMinute = -1f;

            public static bool IsActive { get; private set; } = false;

            public InstanceType Type { get; set; } = InstanceType.Single;

            public float Duration { get; set; } = TOTAL_DURATION_HOURS;
            public float EndTime { get; set; }

            public Tuple<string, int, int>[] RemedyItems { get; set; } =
            {
                Tuple.Create("GEAR_BottleAntibiotics", 2, 2),
            };

            public Tuple<string, int, int>[] AltRemedyItems { get; set; } = Array.Empty<Tuple<string, int, int>>();

            public bool InstantHeal { get; set; } = false;

            //public SepsisAffliction(AfflictionBodyArea bodyArea): base(NAME_KEY, CAUSE_KEY, DESC_KEY, null, UnityEngine.Random.Range(0f, 100f) < Settings.options.AltAfflictionIconChance ? ALT_ICON : ICON, bodyArea, true)
            public SepsisAffliction(AfflictionBodyArea bodyArea): base(NAME_KEY, CAUSE_KEY, DESC_KEY, null, "ico_injury_infectedWound", bodyArea)
            {
                float now = GameManager.GetTimeOfDayComponent().GetHoursPlayedNotPaused();
                EndTime = now + Duration;
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
                if (NeedsRemedy())
                {
                    int remaining = RemedyItems.Length > 0 ? RemedyItems[0].Item3 : 0;
                    int taken = ANTIBIOTIC_DOSES_PER_TREATMENT - remaining;

                    HUDMessage.AddMessage($"Sepsis treatment progress: {taken}/{ANTIBIOTIC_DOSES_PER_TREATMENT} doses.");
                    Core.Log($"Sepsis treatment progress: {taken}/{ANTIBIOTIC_DOSES_PER_TREATMENT} doses.");
                    return;
                }

                float now = GameManager.GetTimeOfDayComponent().GetHoursPlayedNotPaused();
                m_SuppressedUntilTime = now + DOSE_DURATION_HOURS;
                m_HasShownDoseExpiredMessage = false;

                HUDMessage.AddMessage("Sepsis treatment applied for 5 days.");
                Core.Log($"Sepsis treatment applied until hour {m_SuppressedUntilTime:0.##}.");
            }

            public void OnCure()
            {
                IsActive = false;
                CustomAffliction.ResetRemedyItems(this);
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

                ApplyHealthDrain();

                if (!IsTreatmentActive(now) && m_SuppressedUntilTime > 0f && !m_HasShownDoseExpiredMessage)
                {
                    m_HasShownDoseExpiredMessage = true;
                    m_SuppressedUntilTime = -1f;

                    CustomAffliction.ResetRemedyItems(this);

                    HUDMessage.AddMessage("Sepsis treatment expired. Symptoms are back at full strength.");
                    Core.Log("Sepsis treatment window expired, remedy progress reset.");
                }
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

                cond.AddHealth(-hpLoss, DamageSource.Unspecified);

                Core.Log($"Sepsis drain: {hpLoss:0.###} HP over {minuteDelta:0} min ({hpPerHour:0.##}/h)");
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