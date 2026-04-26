using AfflictionComponent.Components;
using AfflictionComponent.Enums;
using AfflictionComponent.Interfaces;
using MajorMiseries.Resources.Localization;

namespace MajorMiseries.Afflictions
{
    internal class SevereAnkleSprain
    {
        public class SevereAnkleSprainAffliction : CustomAffliction, IDuration, IRemedies, IInstance, ILocalizableAffliction, ILimp
        {
            private const string NAME_KEY = "GAMEPLAY_SevereAnkleSprainName";
            private const string CAUSE_KEY = "GAMEPLAY_SevereSprainCause";
            private const string DESC_KEY = "GAMEPLAY_SevereAnkleSprainDescription";

            private const string ICON = "MajorMiseries.Resources.Icons.Afflictions.Classic.SevereAnkleSprain.png";
            private const string ALT_ICON = "MajorMiseries.Resources.Icons.Afflictions.Alt.SevereAnkleSprain_ALT.png";

            public InstanceType Type { get; set; } = InstanceType.SingleLocation;

            public float Duration { get; set; }
            public float EndTime { get; set; }
            public bool IsLimping { get; set; } = true;
            public Tuple<string, int, int>[] RemedyItems { get; set; } =
            {
                Tuple.Create("GEAR_HeavyBandage", 2, 2),
                Tuple.Create("GEAR_BottlePainKillers", 1, 1),
            };

            public Tuple<string, int, int>[] AltRemedyItems { get; set; } = Array.Empty<Tuple<string, int, int>>();
            public bool InstantHeal { get; set; } = false;

            //public SevereAnkleSprainAffliction(AfflictionBodyArea bodyArea, float durationHours) : base(NAME_KEY, CAUSE_KEY, DESC_KEY, null, UnityEngine.Random.Range(0f, 100f) < Settings.options.AltAfflictionIconChance ? ALT_ICON : ICON, bodyArea, true)
            public SevereAnkleSprainAffliction(AfflictionBodyArea bodyArea, float durationHours) : base(NAME_KEY, CAUSE_KEY, DESC_KEY, null, "ico_injury_sprainedAnkle", bodyArea)
            {
                Duration = durationHours;
                ResetEndTime();

                Core.Log($"SevereAnkleSprain prepared on {bodyArea} for {Duration:0.#} hours");
            }

            public void OnFoundExistingInstance(CustomAffliction existingAffliction)
            {
                if (existingAffliction is SevereAnkleSprainAffliction ankle)
                {
                    ankle.ResetAffliction(resetRemedies: true);
                    ankle.Duration = Duration;
                    ankle.ResetEndTime();
                    Core.Log($"SevereAnkleSprain refreshed on {ankle.m_Location} for {ankle.Duration:0.#} hours");
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
                // Treatment stabilizes the injury but does not remove it.
            }

            public void OnCure()
            {
                // Nothing extra.
            }

            public override void OnUpdate()
            {
                if (!Settings.options.EnableSevereSprains)
                    Cure();
            }

            private void ResetEndTime()
            {
                TimeOfDay? tod = GameManager.GetTimeOfDayComponent();
                if (tod != null)
                {
                    EndTime = tod.GetHoursPlayedNotPaused() + Duration;
                }
            }

            public void RefreshLocalization()
            {
                string oldName = m_Name;

                m_Name = Localization.Get(NAME_KEY);
                m_CauseText = Localization.Get(CAUSE_KEY);
                m_Description = Localization.Get(DESC_KEY);
                m_DescriptionNoHeal = null;

                Core.Log($"SevereAnkleSprain refresh -> '{oldName}' => '{m_Name}'");
            }
        }
    }
}