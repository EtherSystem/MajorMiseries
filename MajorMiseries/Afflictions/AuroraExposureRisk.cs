using static MajorMiseries.Afflictions.VoidSickness;
using AfflictionComponent.Components;
using AfflictionComponent.Enums;
using AfflictionComponent.Interfaces;
using MajorMiseries.Managers;
using MajorMiseries.Resources.Localization;
using System.Collections;

namespace MajorMiseries.Afflictions
{
    internal class AuroraExposureRisk
    {
        public class AuroraExposureRiskAffliction : CustomAffliction, IRemedies, IInstance, IRiskPercentage, ILocalizableAffliction
        {
            private const string NAME_KEY = "GAMEPLAY_AuroraExposureName";
            private const string CAUSE_KEY = "GAMEPLAY_AuroraExposureCause";
            private const string DESC_KEY = "GAMEPLAY_AuroraExposureDescription";
            private const string AURORA_DESC_KEY = "GAMEPLAY_AuroraExposureDescriptionAurora";

            private const string ICON = "MajorMiseries.Resources.Icons.Afflictions.Classic.AuroraExposure.png";
            private const string ALT_ICON = "MajorMiseries.Resources.Icons.Afflictions.Alt.AuroraExposure_ALT.png";

            private const float VOID_SICKNESS_THRESHOLD = 99f;

            public static bool IsActive { get; private set; } = false;

            private float m_RiskValue = 0f;
            private bool? m_LastAuroraDescriptionState = null;

            public InstanceType Type { get; set; } = InstanceType.Single;
            public bool Risk { get; set; } = true;
            public bool DebugForced { get; set; } = false;
            public float DebugRiskValue { get; set; } = 50f;

            public Tuple<string, int, int>[] RemedyItems { get; set; } = [];
            public Tuple<string, int, int>[] AltRemedyItems { get; set; } = [];

            public bool InstantHeal { get; set; } = true;

            public AuroraExposureRiskAffliction(AfflictionBodyArea bodyArea) : base(NAME_KEY, CAUSE_KEY, DESC_KEY, null, UnityEngine.Random.Range(0f, 100f) < Settings.options.AltAfflictionIconChance ? ALT_ICON : ICON, bodyArea, true)
            {
                UpdateRiskValue();
                RefreshLocalization();
            }

            public void OnFoundExistingInstance(CustomAffliction existingAffliction)
            {
                if (existingAffliction is AuroraExposureRiskAffliction auroraExposure)
                {
                    auroraExposure.UpdateRiskValue();
                    auroraExposure.RefreshLocalization();
                }
            }

            public void CureSymptoms()
            {
            }

            public void OnCure()
            {
                IsActive = false;
            }

            public float GetRiskValue() => DebugForced ? DebugRiskValue : m_RiskValue;

            public override void OnUpdate()
            {
                IsActive = true;

                if (DebugForced)
                {
                    RefreshLocalizationIfNeeded();
                    return;
                }

                if (!Settings.options.EnableAuroraInfluence)
                {
                    Cure();
                    return;
                }

                if (!Risk) return;

                UpdateRiskValue();
                RefreshLocalizationIfNeeded();

                if (VoidSicknessAffliction.IsActive)
                {
                    Cure();
                    return;
                }

                if (m_RiskValue >= VOID_SICKNESS_THRESHOLD)
                {
                    Core.Log($"AuroraExposure evolved into VoidSickness -> exposure={m_RiskValue:0.##}/100.");
                    Cure(false);
                    MelonCoroutines.Start(StartVoidSicknessNextFrame);
                    return;
                }

                if (m_RiskValue <= 0f)
                {
                    Core.Log("AuroraExposure risk fully decayed.");
                    Cure();
                }
            }

            public void UpdateRiskValue()
            {
                Core.State ??= new Persistence.MMState();
                m_RiskValue = Mathf.Clamp(Core.State.AuroraInfluenceExposure, 0f, AuroraInfluenceManager.GetExposureMax());
            }

            private void RefreshLocalizationIfNeeded()
            {
                bool useAuroraDescription = ShouldUseAuroraDescription();
                if (m_LastAuroraDescriptionState.HasValue && m_LastAuroraDescriptionState.Value == useAuroraDescription) return;

                RefreshLocalization();
            }

            private bool ShouldUseAuroraDescription()
            {
                return AuroraInfluenceManager.IsAuroraActive() && GetRiskValue() > 25f;
            }

            private static IEnumerator StartVoidSicknessNextFrame
            {
                get
                {
                    yield return null;

                    if (!Settings.options.EnableAuroraInfluence) yield break;
                    if (Core.State == null || Core.State.AuroraInfluenceExposure < VOID_SICKNESS_THRESHOLD) yield break;

                    new VoidSicknessAffliction(AfflictionBodyArea.Head).Start();
                }
            }

            public void RefreshLocalization()
            {
                string oldName = m_Name;

                bool useAuroraDescription = ShouldUseAuroraDescription();

                m_Name = Localization.Get(NAME_KEY);
                m_CauseText = Localization.Get(CAUSE_KEY);
                m_Description = Localization.Get(useAuroraDescription ? AURORA_DESC_KEY : DESC_KEY);
                m_DescriptionNoHeal = null;
                m_LastAuroraDescriptionState = useAuroraDescription;

                Core.Log($"AuroraExposure refresh -> '{oldName}' => '{m_Name}' | AuroraDescription:{useAuroraDescription}");
            }
        }
    }
}