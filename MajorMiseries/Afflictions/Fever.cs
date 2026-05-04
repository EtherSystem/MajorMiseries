using AfflictionComponent.Components;
using AfflictionComponent.Enums;
using AfflictionComponent.Interfaces;
using MajorMiseries.Resources.Localization;

namespace MajorMiseries.Afflictions
{
    internal class Fever
    {
        public class FeverAffliction : CustomAffliction, IRemedies, IInstance, ILocalizableAffliction
        {
            private const string NAME_KEY = "GAMEPLAY_FeverName";
            private const string CAUSE_KEY = "GAMEPLAY_FeverCause";
            private const string DESC_KEY = "GAMEPLAY_FeverDescription";

            private const float DRAIN_LOG_INTERVAL_MINUTES = 60f;

            private float m_LastWholeMinute = -1f;
            private float m_DrainLogMinutes = 0f;
            private float m_DrainLogFatigue = 0f;
            private float m_DrainLogThirst = 0f;

            public static bool IsActive { get; private set; } = false;

            public InstanceType Type { get; set; } = InstanceType.Single;

            public Tuple<string, int, int>[] RemedyItems { get; set; } = Array.Empty<Tuple<string, int, int>>();
            public Tuple<string, int, int>[] AltRemedyItems { get; set; } = Array.Empty<Tuple<string, int, int>>();

            public bool InstantHeal { get; set; } = true;

            public FeverAffliction(AfflictionBodyArea bodyArea) : base(NAME_KEY, CAUSE_KEY, DESC_KEY, null, "ico_injury_diabetes", bodyArea)
            {
            }

            public void OnFoundExistingInstance(CustomAffliction existingAffliction)
            {
                if (existingAffliction is FeverAffliction existing)
                {
                    existing.ResetAffliction(resetRemedies: false);
                }
            }

            public void CureSymptoms()
            {
                // Fever is a visible immune response. Its lifecycle is driven by ImmunityManager.
            }

            public void OnCure()
            {
                FlushDrainLog();

                IsActive = false;
                m_LastWholeMinute = -1f;
                m_DrainLogMinutes = 0f;
                m_DrainLogFatigue = 0f;
                m_DrainLogThirst = 0f;
            }

            public override void OnUpdate()
            {
                IsActive = true;

                if (!Settings.options.EnableImmunityShield)
                {
                    Cure();
                    return;
                }

                if (!ImmunityManager.ShouldFeverAfflictionBeVisible())
                {
                    Cure();
                    return;
                }

                Panel_FirstAid firstAid = InterfaceManager.GetPanel<Panel_FirstAid>();
                if (firstAid != null && firstAid.isActiveAndEnabled)
                    return;

                ApplyTimedEffects();
            }

            private static float GetCurrentWholeMinute()
            {
                TimeOfDay tod = GameManager.GetTimeOfDayComponent();
                if (tod == null) return 0f;

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
                if (minuteDelta <= 0f) return;

                m_LastWholeMinute = currentMinute;

                float hoursDelta = minuteDelta / 60f;

                float fatiguePerHour = ImmunityManager.GetFeverFatiguePerHour();
                float thirstPerHour = ImmunityManager.GetFeverThirstPerHour();

                float fatigueToAdd = hoursDelta * fatiguePerHour;
                float thirstToAdd = hoursDelta * thirstPerHour;

                Fatigue fatigue = GameManager.GetFatigueComponent();
                if (fatigue != null && fatigueToAdd > 0f)
                {
                    fatigue.AddFatigue(fatigueToAdd);
                    m_DrainLogFatigue += fatigueToAdd;
                }

                Thirst thirst = GameManager.GetThirstComponent();
                if (thirst != null && thirstToAdd > 0f)
                {
                    thirst.AddThirst(thirstToAdd);
                    m_DrainLogThirst += thirstToAdd;
                }

                m_DrainLogMinutes += minuteDelta;

                if (m_DrainLogMinutes >= DRAIN_LOG_INTERVAL_MINUTES)
                    FlushDrainLog();
            }

            private void FlushDrainLog()
            {
                if (m_DrainLogMinutes <= 0f) return;
                if (m_DrainLogFatigue <= 0f && m_DrainLogThirst <= 0f) return;

                Core.Log($"Fever drain: +{m_DrainLogFatigue:0.###} fatigue, +{m_DrainLogThirst:0.###} thirst over {m_DrainLogMinutes:0} min.");

                m_DrainLogMinutes = 0f;
                m_DrainLogFatigue = 0f;
                m_DrainLogThirst = 0f;
            }

            public void RefreshLocalization()
            {
                string oldName = m_Name;

                m_Name = Localization.Get(NAME_KEY);
                m_CauseText = Localization.Get(CAUSE_KEY);
                m_Description = Localization.Get(DESC_KEY);
                m_DescriptionNoHeal = null;

                Core.Log($"Fever refresh -> '{oldName}' => '{m_Name}'");
            }
        }
    }
}