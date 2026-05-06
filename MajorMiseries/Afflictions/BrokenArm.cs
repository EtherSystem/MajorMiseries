using AfflictionComponent.Components;
using AfflictionComponent.Enums;
using AfflictionComponent.Interfaces;
using MajorMiseries.Resources.Localization;

namespace MajorMiseries.Afflictions
{
    internal class BrokenArm
    {
        public class BrokenArmAffliction : CustomAffliction, IDuration, IRemedies, IInstance, ILocalizableAffliction
        {
            private const string NAME_KEY = "GAMEPLAY_BrokenArmName";
            private const string CAUSE_KEY = "GAMEPLAY_BrokenArmCause";
            private const string DESC_KEY = "GAMEPLAY_BrokenArmDescription";

            private const string ICON = "MajorMiseries.Resources.Icons.Afflictions.Classic.BrokenArm.png";
            private const string ALT_ICON = "MajorMiseries.Resources.Icons.Afflictions.Alt.BrokenArm_ALT.png";

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

            // public BrokenArmAffliction(AfflictionBodyArea bodyArea, float durationHours) : base(NAME_KEY, CAUSE_KEY, DESC_KEY, null, UnityEngine.Random.Range(0f, 100f) < Settings.options.AltAfflictionIconChance ? ALT_ICON : ICON, bodyArea, true)
            public BrokenArmAffliction(AfflictionBodyArea bodyArea, float durationHours) : base(NAME_KEY, CAUSE_KEY, DESC_KEY, null, "ico_injury_BrokenBody", bodyArea)
            {
                Duration = durationHours;

                TimeOfDay? tod = GameManager.GetTimeOfDayComponent();
                if (tod != null)
                {
                    EndTime = tod.GetHoursPlayedNotPaused() + Duration;
                }

                Core.Log($"BrokenArm prepared on {bodyArea} for {Duration:0} hours");
            }

            public void OnFoundExistingInstance(CustomAffliction existingAffliction)
            {
                if (existingAffliction is BrokenArmAffliction brokenArm)
                {
                    brokenArm.ResetAffliction(resetRemedies: true);

                    brokenArm.Duration = Duration;

                    TimeOfDay? tod = GameManager.GetTimeOfDayComponent();
                    if (tod != null)
                    {
                        brokenArm.EndTime = tod.GetHoursPlayedNotPaused() + brokenArm.Duration;
                    }

                    Core.Log($"BrokenArm refreshed on {brokenArm.m_Location} for {brokenArm.Duration:0} hours");
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
            }

            public void OnCure()
            {
            }

            public override void OnUpdate()
            {
            }

            public void RefreshLocalization()
            {
                string oldName = m_Name;

                m_Name = Localization.Get(NAME_KEY);
                m_CauseText = Localization.Get(CAUSE_KEY);
                m_Description = Localization.Get(DESC_KEY);
                m_DescriptionNoHeal = null;

                Core.Log($"BrokenArm refresh -> '{oldName}' => '{m_Name}'");
            }
        }
    }
}