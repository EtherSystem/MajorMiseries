using AfflictionComponent.Components;
using AfflictionComponent.Enums;
using AfflictionComponent.Interfaces;
using MajorMiseries.Resources.Localization;

namespace MajorMiseries.Afflictions
{
    internal class CorpseSickness
    {
        public class CorpseSicknessAffliction : CustomAffliction, IDuration, IRemedies, IInstance, ILocalizableAffliction
        {
            private const string NAME_KEY = "GAMEPLAY_CorpseSicknessName";
            private const string CAUSE_KEY = "GAMEPLAY_CorpseSicknessCause";
            private const string DESC_KEY = "GAMEPLAY_CorpseSicknessDescription";

            private const string ICON = "MajorMiseries.Resources.Icons.Afflictions.Classic.CorpseSickness.png";
            private const string ALT_ICON = "MajorMiseries.Resources.Icons.Afflictions.Alt.CorpseSickness_ALT.png";

            public const float FATIGUE_INCREASE_MULTIPLIER = 2f;
            public const float CONDITION_LOSS_PER_HOUR = 1.5f;

            private float m_LastWholeMinute = -1f;
            private const float DRAIN_LOG_INTERVAL_MINUTES = 60f;
            private float m_DrainLogHpLoss = 0f;
            private float m_DrainLogMinutes = 0f;

            public static bool IsActive { get; private set; } = false;

            public InstanceType Type { get; set; } = InstanceType.Single;

            public float Duration { get; set; }
            public float EndTime { get; set; }

            public Tuple<string, int, int>[] RemedyItems { get; set; } = [];
            public Tuple<string, int, int>[] AltRemedyItems { get; set; } = [];

            public bool InstantHeal { get; set; } = true;

            public CorpseSicknessAffliction(AfflictionBodyArea bodyArea, float durationHours) : base(NAME_KEY, CAUSE_KEY, DESC_KEY, null, UnityEngine.Random.Range(0f, 100f) < Settings.options.AltAfflictionIconChance ? ALT_ICON : ICON, bodyArea, true)
            {
                Duration = durationHours;

                TimeOfDay? tod = GameManager.GetTimeOfDayComponent();
                if (tod != null)
                {
                    EndTime = tod.GetHoursPlayedNotPaused() + Duration;
                }

                Core.Log($"CorpseSickness prepared for {Duration:0.##} hours.");
            }

            public void OnFoundExistingInstance(CustomAffliction existingAffliction)
            {
                if (existingAffliction is CorpseSicknessAffliction corpseSickness)
                {
                    corpseSickness.ResetAffliction(resetRemedies: false);
                    corpseSickness.Duration = Duration;

                    TimeOfDay? tod = GameManager.GetTimeOfDayComponent();
                    if (tod != null)
                    {
                        corpseSickness.EndTime = tod.GetHoursPlayedNotPaused() + corpseSickness.Duration;
                    }

                    Core.Log($"CorpseSickness refreshed for {corpseSickness.Duration:0.##} hours.");
                }
            }

            public void CureSymptoms()
            {
            }

            public void OnCure()
            {
                FlushDrainLog();

                IsActive = false;
                m_LastWholeMinute = -1f;

                if (Core.State != null && Core.State.CorpseExposure > 0f)
                {
                    Core.State.CorpseExposure = 0f;
                    Core.Instance?.MarkDirty();
                    Core.Log("CorpseSickness cured -> CorpseExposure reset to 0.");
                }
                AfflictionSaveHelper.QueueSurvivalSave();
            }

            public override void OnUpdate()
            {
                Panel_FirstAid firstAid = InterfaceManager.GetPanel<Panel_FirstAid>();
                if (firstAid != null && firstAid.isActiveAndEnabled)
                    return;

                IsActive = true;

                if (!Settings.options.EnableCorpseSickness)
                {
                    Cure();
                    return;
                }

                TimeOfDay? tod = GameManager.GetTimeOfDayComponent();
                if (tod == null)
                    return;

                float now = tod.GetHoursPlayedNotPaused();

                if (now >= EndTime)
                {
                    Core.Log("CorpseSickness expired naturally.");
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

                Condition? condition = GameManager.GetConditionComponent();
                if (condition == null || condition.m_CurrentHP <= 0f)
                    return;

                float hpLoss = hoursDelta * CONDITION_LOSS_PER_HOUR;
                if (hpLoss <= 0f)
                    return;

                float appliedHpLoss = AfflictionLogic.ApplyChunkedConditionDrain(condition, hpLoss);

                if (appliedHpLoss <= 0f)
                    return;

                m_DrainLogHpLoss += appliedHpLoss;
                m_DrainLogMinutes += minuteDelta;

                if (m_DrainLogMinutes >= DRAIN_LOG_INTERVAL_MINUTES)
                    FlushDrainLog();
            }

            private void FlushDrainLog()
            {
                if (m_DrainLogMinutes <= 0f || m_DrainLogHpLoss <= 0f)
                    return;

                Core.Log($"CorpseSickness drain: {m_DrainLogHpLoss:0.###} HP over {m_DrainLogMinutes:0} min.");

                m_DrainLogHpLoss = 0f;
                m_DrainLogMinutes = 0f;
            }

            public void RefreshLocalization()
            {
                string oldName = m_Name;

                m_Name = Localization.Get(NAME_KEY);
                m_CauseText = Localization.Get(CAUSE_KEY);
                m_Description = Localization.Get(DESC_KEY);
                m_DescriptionNoHeal = null;

                Core.Log($"CorpseSickness refresh -> '{oldName}' => '{m_Name}'");
            }
        }
    }
}