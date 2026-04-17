using AfflictionComponent.Components;
using AfflictionComponent.Enums;
using AfflictionComponent.Interfaces;
using MajorMiseries.Resources.Localization;

namespace MajorMiseries.Afflictions
{
    internal class COPoisoning
    {
        public class COPoisoningAffliction : CustomAffliction, IDuration, IRemedies, IInstance, ILocalizableAffliction
        {
            private const string NAME_KEY = "GAMEPLAY_COPoisoningName";
            private const string CAUSE_KEY = "GAMEPLAY_COPoisoningCause";
            private const string DESC_KEY = "GAMEPLAY_COPoisoningDescription";

            private const string ICON = "MajorMiseries.Resources.Icons.Afflictions.Classic.COPoisoning.png";
            private const string ALT_ICON = "MajorMiseries.Resources.Icons.Afflictions.Alt.COPoisoning_ALT.png";

            public const float PASSIVE_FATIGUE_PER_HOUR = 6f;
            public const float CONDITION_LOSS_PER_HOUR = 5f;

            private float m_LastWholeMinute = -1f;

            public static bool IsActive { get; private set; } = false;

            public InstanceType Type { get; set; } = InstanceType.Single;

            public float Duration { get; set; }
            public float EndTime { get; set; }

            public Tuple<string, int, int>[] RemedyItems { get; set; } = Array.Empty<Tuple<string, int, int>>();
            public Tuple<string, int, int>[] AltRemedyItems { get; set; } = Array.Empty<Tuple<string, int, int>>();

            public bool InstantHeal { get; set; } = true;

            //public COPoisoningAffliction(AfflictionBodyArea bodyArea, float durationHours) : base(NAME_KEY, CAUSE_KEY, DESC_KEY, null, UnityEngine.Random.Range(0f, 100f) < Settings.options.AltAfflictionIconChance ? ALT_ICON : ICON, bodyArea, true)
            public COPoisoningAffliction(AfflictionBodyArea bodyArea, float durationHours) : base(NAME_KEY, CAUSE_KEY, DESC_KEY, null, "ico_injury_BrokenBody", bodyArea)
            {
                Duration = durationHours;

                TimeOfDay? tod = GameManager.GetTimeOfDayComponent();
                if (tod != null)
                {
                    EndTime = tod.GetHoursPlayedNotPaused() + Duration;
                }

                Core.Log($"COPoisoning prepared for {Duration:0.##} hours.");
            }

            public void OnFoundExistingInstance(CustomAffliction existingAffliction)
            {
                if (existingAffliction is COPoisoningAffliction coPoisoning)
                {
                    coPoisoning.ResetAffliction(resetRemedies: false);
                    coPoisoning.Duration = Duration;

                    TimeOfDay? tod = GameManager.GetTimeOfDayComponent();
                    if (tod != null)
                    {
                        coPoisoning.EndTime = tod.GetHoursPlayedNotPaused() + coPoisoning.Duration;
                    }

                    Core.Log($"COPoisoning refreshed for {coPoisoning.Duration:0.##} hours.");
                }
            }

            public void CureSymptoms()
            {
                // cure symptoms but not the affliction
            }

            public void OnCure()
            {
                IsActive = false;
                m_LastWholeMinute = -1f;
            }

            public override void OnUpdate()
            {
                Panel_FirstAid firstAid = InterfaceManager.GetPanel<Panel_FirstAid>();
                if (firstAid != null && firstAid.isActiveAndEnabled)
                    return;

                IsActive = true;

                TimeOfDay? tod = GameManager.GetTimeOfDayComponent();
                if (tod == null)
                    return;

                float now = tod.GetHoursPlayedNotPaused();

                if (now >= EndTime)
                {
                    Core.Log("COPoisoning expired naturally.");
                    Cure();
                    return;
                }

                ApplyTimedEffects();
            }

            private static float GetCurrentWholeMinute()
            {
                TimeOfDay tod = GameManager.GetTimeOfDayComponent();
                if (tod == null)
                    return 0f;

                return Mathf.Floor(tod.GetHoursPlayedNotPaused() * 60f);
            }

            private void ApplyTimedEffects()
            {
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

                float hoursDelta = minuteDelta / 60f;

                Fatigue? fatigue = GameManager.GetFatigueComponent();
                fatigue?.AddFatigue(hoursDelta * PASSIVE_FATIGUE_PER_HOUR);

                Condition? condition = GameManager.GetConditionComponent();
                if (condition == null || condition.m_CurrentHP <= 0f)
                    return;

                float hpLoss = hoursDelta * CONDITION_LOSS_PER_HOUR;
                if (hpLoss <= 0f)
                    return;

                condition.AddHealth(-hpLoss, DamageSource.Unspecified);

                Core.Log($"COPoisoning drain: {hpLoss:0.###} HP over {minuteDelta:0} min ({CONDITION_LOSS_PER_HOUR:0.##}/h)");
            }

            public void RefreshLocalization()
            {
                string oldName = m_Name;

                m_Name = Localization.Get(NAME_KEY);
                m_CauseText = Localization.Get(CAUSE_KEY);
                m_Description = Localization.Get(DESC_KEY);
                m_DescriptionNoHeal = null;

                Core.Log($"CarbonMonoxidePoisoning refresh -> '{oldName}' => '{m_Name}'");
            }
        }
    }
}