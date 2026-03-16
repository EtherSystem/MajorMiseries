using AfflictionComponent.Components;
using AfflictionComponent.Enums;
using AfflictionComponent.Interfaces;
using MajorMiseries.Resources.Localization;

namespace MajorMiseries.Afflictions
{
    internal class Knell
    {
        public class KnellAffliction : CustomAffliction, IDuration, IRemedies, IInstance, ILocalizableAffliction
        {
            private const string NAME_KEY = "GAMEPLAY_KnellName";
            private const string CAUSE_KEY = "GAMEPLAY_KnellCause";
            private const string DESC_KEY = "GAMEPLAY_KnellDescription";

            public InstanceType Type { get; set; } = InstanceType.Single;
            public void OnFoundExistingInstance(CustomAffliction existingAffliction)
            {
                if (existingAffliction is KnellAffliction knell)
                {
                    knell.ResetAffliction(resetRemedies: false);
                    var now = GameManager.GetTimeOfDayComponent().GetHoursPlayedNotPaused();
                    knell.EndTime = now + knell.Duration;
                }
            }

            public float Duration { get; set; }
            public float EndTime { get; set; }

            public Tuple<string, int, int>[] RemedyItems { get; set; } = Array.Empty<Tuple<string, int, int>>();
            public Tuple<string, int, int>[] AltRemedyItems { get; set; } = Array.Empty<Tuple<string, int, int>>();

            public bool InstantHeal { get; set; } = true;

            //public KnellAffliction(AfflictionBodyArea bodyArea) : base(NAME_KEY, CAUSE_KEY, DESC_KEY, null, "Major_Miseries.Resources.Icons.Afflictions.Knell.png", bodyArea, true)
            public KnellAffliction(AfflictionBodyArea bodyArea) : base(NAME_KEY, CAUSE_KEY, DESC_KEY, null, "ico_injury_BrokenBody", bodyArea)
            {
            }

            public void CureSymptoms()
            {
                //cure symptoms but not the affliction
            }

            public void OnCure()
            {
                //when the affliction is cured, apply this code
            }

            public override void OnUpdate()
            {
                // yes theres no effects, its intended
            }

            public void RefreshLocalization()
            {
                string oldName = m_Name;

                m_Name = Localization.Get(NAME_KEY);
                m_CauseText = Localization.Get(CAUSE_KEY);
                m_Description = Localization.Get(DESC_KEY);
                m_DescriptionNoHeal = null;

                Core.Log($"Knell refresh -> '{oldName}' => '{m_Name}'");
            }
        }
    }
}