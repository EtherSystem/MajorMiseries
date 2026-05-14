using AfflictionComponent.Components;
using AfflictionComponent.Enums;
using AfflictionComponent.Interfaces;
using MajorMiseries.Resources.Localization;

namespace MajorMiseries.Afflictions
{
    internal class SevereWristSprain
    {
        public class SevereWristSprainAffliction : CustomAffliction, IDuration, IRemedies, IInstance, ILocalizableAffliction
        {
            private const string NAME_KEY = "GAMEPLAY_SevereWristSprainName";
            private const string CAUSE_KEY = "GAMEPLAY_SevereSprainCause";
            private const string DESC_KEY = "GAMEPLAY_SevereWristSprainDescription";

            private const string LEFT_ICON = "MajorMiseries.Resources.Icons.Afflictions.Classic.SevereWristSprain_Left.png";
            private const string RIGHT_ICON = "MajorMiseries.Resources.Icons.Afflictions.Classic.SevereWristSprain_Right.png";

            public InstanceType Type { get; set; } = InstanceType.SingleLocation;

            public float Duration { get; set; }
            public float EndTime { get; set; }

            public Tuple<string, int, int>[] RemedyItems { get; set; } =
            [
                Tuple.Create("GEAR_HeavyBandage", 2, 2),
                Tuple.Create("GEAR_BottlePainKillers", 1, 1),
            ];

            public Tuple<string, int, int>[] AltRemedyItems { get; set; } = [];
            public bool InstantHeal { get; set; } = false;

            public SevereWristSprainAffliction(AfflictionBodyArea bodyArea, float durationHours) : base(NAME_KEY, CAUSE_KEY, DESC_KEY, null, GetIcon(bodyArea), bodyArea, true)
            {
                Duration = durationHours;
                ResetEndTime();

                Core.Log($"SevereWristSprain prepared on {bodyArea} for {Duration:0.#} hours");
            }

            internal static string GetIcon(AfflictionBodyArea bodyArea)
            {
                return bodyArea switch
                {
                    AfflictionBodyArea.HandRight => RIGHT_ICON,
                    _ => LEFT_ICON,
                };
            }

            public void OnFoundExistingInstance(CustomAffliction existingAffliction)
            {
                if (existingAffliction is SevereWristSprainAffliction wrist)
                {
                    wrist.ResetAffliction(resetRemedies: true);
                    wrist.Duration = Duration;
                    wrist.ResetEndTime();
                    Core.Log($"SevereWristSprain refreshed on {wrist.m_Location} for {wrist.Duration:0.#} hours");
                }
            }

            public bool IsDurationUp()
            {
                TimeOfDay? tod = GameManager.GetTimeOfDayComponent();
                if (tod == null) return false;

                return tod.GetHoursPlayedNotPaused() >= EndTime;
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
                if (!Settings.options.EnableSevereSprains) Cure();
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

                Core.Log($"SevereWristSprain refresh -> '{oldName}' => '{m_Name}'");
            }
        }
    }
}
