using AfflictionComponent.Components;
using AfflictionComponent.Enums;
using AfflictionComponent.Interfaces;
using MajorMiseries.Resources.Localization;

namespace MajorMiseries.Afflictions
{
    internal class BlackLung
    {
        public class BlackLungAffliction : CustomAffliction, IDuration, IRemedies, IInstance, ILocalizableAffliction
        {
            private const string NAME_KEY = "GAMEPLAY_BlackLungName";
            private const string CAUSE_KEY = "GAMEPLAY_BlackLungCause";
            private const string DESC_KEY = "GAMEPLAY_BlackLungDescription";

            private const string ICON = "MajorMiseries.Resources.Icons.Afflictions.Classic.BlackLung.png";
            private const string ALT_ICON = "MajorMiseries.Resources.Icons.Afflictions.Alt.BlackLung_ALT.png";

            public static bool IsActive { get; private set; } = false;

            public InstanceType Type { get; set; } = InstanceType.Single;

            public float Duration { get; set; }
            public float EndTime { get; set; }

            public Tuple<string, int, int>[] RemedyItems { get; set; } = Array.Empty<Tuple<string, int, int>>();
            public Tuple<string, int, int>[] AltRemedyItems { get; set; } = Array.Empty<Tuple<string, int, int>>();

            public bool InstantHeal { get; set; } = true;

            //public BlackLungAffliction(AfflictionBodyArea bodyArea, float durationHours) : base(NAME_KEY, CAUSE_KEY, DESC_KEY, null, UnityEngine.Random.Range(0f, 100f) < Settings.options.AltAfflictionIconChance ? ALT_ICON : ICON, bodyArea, true)
            public BlackLungAffliction(AfflictionBodyArea bodyArea, float durationHours) : base(NAME_KEY, CAUSE_KEY, DESC_KEY, null, "ico_injury_BrokenBody", bodyArea)
            {
                Duration = durationHours;

                TimeOfDay? tod = GameManager.GetTimeOfDayComponent();
                if (tod != null)
                {
                    EndTime = tod.GetHoursPlayedNotPaused() + Duration;
                }

                Core.Log($"BlackLung prepared for {Duration:0.##} hours.");
            }

            public void OnFoundExistingInstance(CustomAffliction existingAffliction)
            {
                if (existingAffliction is BlackLungAffliction blackLung)
                {
                    blackLung.ResetAffliction(resetRemedies: false);

                    blackLung.Duration = Duration;

                    TimeOfDay? tod = GameManager.GetTimeOfDayComponent();
                    if (tod != null)
                    {
                        blackLung.EndTime = tod.GetHoursPlayedNotPaused() + blackLung.Duration;
                    }

                    Core.Log($"BlackLung refreshed for {blackLung.Duration:0.##} hours.");
                }
            }

            public void CureSymptoms()
            {
                // cure symptoms but not the affliction
            }

            public void OnCure()
            {
                IsActive = false;

                if (Core.State != null && Core.State.BlackLungExposure > 0f)
                {
                    Core.State.BlackLungExposure = 0f;
                    Core.Instance?.MarkDirty();
                    Core.Log("BlackLung cured -> BlackLungExposure reset to 0.");
                }
            }

            public override void OnUpdate()
            {
                IsActive = true;

                TimeOfDay? tod = GameManager.GetTimeOfDayComponent();
                if (tod == null)
                    return;

                if (tod.GetHoursPlayedNotPaused() >= EndTime)
                {
                    Core.Log("BlackLung expired naturally.");
                    Cure();
                }
            }

            public void RefreshLocalization()
            {
                string oldName = m_Name;

                m_Name = Localization.Get(NAME_KEY);
                m_CauseText = Localization.Get(CAUSE_KEY);
                m_Description = Localization.Get(DESC_KEY);
                m_DescriptionNoHeal = null;

                Core.Log($"BlackLung refresh -> '{oldName}' => '{m_Name}'");
            }
        }
    }
}