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

            private const string ICON = "Major_Miseries.Resources.Icons.Afflictions.Classic.COPoisoning.png";
            private const string ALT_ICON = "Major_Miseries.Resources.Icons.Afflictions.Alt.COPoisoning_ALT.png";

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
            }

            public override void OnUpdate()
            {
                IsActive = true;

                TimeOfDay? tod = GameManager.GetTimeOfDayComponent();
                if (tod == null)
                    return;

                if (tod.GetHoursPlayedNotPaused() >= EndTime)
                {
                    Core.Log("COPoisoning expired naturally.");
                    Cure();
                }

                // yes theres no direct effects, its intended
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