using AfflictionComponent.Components;
using AfflictionComponent.Enums;
using AfflictionComponent.Interfaces;
using MajorMiseries.Resources.Localization;

namespace MajorMiseries.Afflictions
{
    internal class BrokenLeg
    {
        public class BrokenLegAffliction : CustomAffliction, IDuration, IRemedies, IInstance, ILocalizableAffliction
        {
            private const string NAME_KEY = "GAMEPLAY_BrokenLegName";
            private const string DEFAULT_CAUSE_KEY = "GAMEPLAY_BrokenLegCause";
            private const string DESC_KEY = "GAMEPLAY_BrokenLegDescription";

            private const string ICON = "Major_Miseries.Resources.Icons.Afflictions.Classic.BrokenLeg.png";
            private const string ALT_ICON = "Major_Miseries.Resources.Icons.Afflictions.Alt.BrokenLeg_ALT.png";

            private string _causeKey;

            public InstanceType Type { get; set; } = InstanceType.SingleLocation;

            public float Duration { get; set; }
            public float EndTime { get; set; }

            public Tuple<string, int, int>[] RemedyItems { get; set; } =
            {
                Tuple.Create("GEAR_HeavyBandage", 4, 4),
                Tuple.Create("GEAR_BottlePainKillers", 2, 2),
            };

            public Tuple<string, int, int>[] AltRemedyItems { get; set; } = Array.Empty<Tuple<string, int, int>>();

            public bool InstantHeal { get; set; } = false;

            //public BrokenLegAffliction(AfflictionBodyArea bodyArea, float durationHours, string? causeKey = null) : base(NAME_KEY, causeKey ?? DEFAULT_CAUSE_KEY, DESC_KEY, null, UnityEngine.Random.Range(0f, 100f) < Settings.options.AltAfflictionIconChance ? ALT_ICON : ICON, bodyArea, true)
            public BrokenLegAffliction(AfflictionBodyArea bodyArea, float durationHours, string? causeKey = null) : base(NAME_KEY, causeKey ?? DEFAULT_CAUSE_KEY, DESC_KEY, null, "ico_injury_BrokenBody", bodyArea)
            {
                Duration = durationHours;
                _causeKey = string.IsNullOrWhiteSpace(causeKey) ? DEFAULT_CAUSE_KEY : causeKey;

                TimeOfDay? tod = GameManager.GetTimeOfDayComponent();
                if (tod != null)
                {
                    EndTime = tod.GetHoursPlayedNotPaused() + Duration;
                }

                m_CauseText = Localization.Get(_causeKey);

                Core.Log($"BrokenLeg prepared on {bodyArea} for {Duration:0} hours with cause '{_causeKey}'");
            }

            public void OnFoundExistingInstance(CustomAffliction existingAffliction)
            {
                if (existingAffliction is BrokenLegAffliction brokenLeg)
                {
                    brokenLeg.ResetAffliction(resetRemedies: true);

                    brokenLeg.Duration = Duration;
                    brokenLeg._causeKey = _causeKey;

                    TimeOfDay? tod = GameManager.GetTimeOfDayComponent();
                    if (tod != null)
                    {
                        brokenLeg.EndTime = tod.GetHoursPlayedNotPaused() + brokenLeg.Duration;
                    }

                    brokenLeg.m_CauseText = Localization.Get(brokenLeg._causeKey);

                    Core.Log($"BrokenLeg refreshed on {brokenLeg.m_Location} for {brokenLeg.Duration:0} hours with cause '{brokenLeg._causeKey}'");
                }
            }

            public bool IsDurationUp()
            {
                TimeOfDay? tod = GameManager.GetTimeOfDayComponent();
                if (tod == null)
                    return false;

                return tod.GetHoursPlayedNotPaused() >= EndTime;
            }

            public void CureSymptoms()
            {
                // cure symptoms but not the affliction
            }

            public void OnCure()
            {
                // when the affliction is cured, apply this code
            }

            public override void OnUpdate()
            {
                // yes theres no effects, its intended
            }

            public void RefreshLocalization()
            {
                string oldName = m_Name;

                m_Name = Localization.Get(NAME_KEY);
                m_CauseText = Localization.Get(_causeKey);
                m_Description = Localization.Get(DESC_KEY);
                m_DescriptionNoHeal = null;

                Core.Log($"BrokenLeg refresh -> '{oldName}' => '{m_Name}' with cause '{_causeKey}'");
            }
        }
    }
}