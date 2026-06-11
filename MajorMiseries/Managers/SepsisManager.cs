using AfflictionComponent.Components;
using static MajorMiseries.Afflictions.SepsisRisk;

namespace MajorMiseries
{
    internal static partial class AfflictionLogic
    {
        private const float SEPSIS_INFECTION_RISK_ROLL_INTERVAL_HOURS = 1f;
        private const float SEPSIS_INFECTION_CONVERSION_RISK_BOOST = 10f;

        private static readonly Dictionary<AfflictionBodyArea, float> s_SepsisInfectionRiskLastRollHours = [];

        internal static void ResetSepsisInfectionRiskRollTracking()
        {
            s_SepsisInfectionRiskLastRollHours.Clear();
        }

        internal static float GetSepsisRiskProgressMultiplier()
        {
            return Settings.options.SepsisPreset switch
            {
                0 => 0.5f,
                2 => 1.5f,
                3 => 2f,
                4 => 3f,
                _ => 1f
            };
        }

        internal static int GetSepsisRequiredDoseIntakesPerWindow()
        {
            return Settings.options.SepsisPreset == 0 ? 1 : 2;
        }

        internal static float GetSepsisTreatmentWindowIntervalHours()
        {
            return Settings.options.SepsisPreset switch
            {
                2 => 96f,
                3 => 72f,
                4 => 48f,
                _ => 120f
            };
        }

        private static float GetSepsisInfectionRiskBaseRollChance()
        {
            return Settings.options.SepsisPreset switch
            {
                0 => 0f,
                2 => 10f,
                3 => 17f,
                4 => 25f,
                _ => 5f
            };
        }

        private static float GetSepsisInfectionRiskImmunityMultiplier()
        {
            if (!Settings.options.EnableImmunityShield) return 1f;

            float shield = Mathf.Clamp(Core.State?.ImmunityShield ?? 100f, 0f, 100f);

            if (shield >= 90f) return 0.25f;
            if (shield >= 70f) return 0.5f;
            if (shield >= 50f) return 1f;
            if (shield >= 25f) return 1.5f;
            return 2f;
        }

        internal static float GetSepsisInfectionRiskRollChance()
        {
            return Mathf.Clamp(GetSepsisInfectionRiskBaseRollChance() * GetSepsisInfectionRiskImmunityMultiplier(), 0f, 100f);
        }

        internal static void UpdateSepsisInfectionRiskRolls(float gameHoursPassed)
        {
            if (gameHoursPassed <= 0f) return;

            if (!Settings.options.EnableSepsis)
            {
                s_SepsisInfectionRiskLastRollHours.Clear();
                return;
            }

            TimeOfDay tod = GameManager.GetTimeOfDayComponent();
            if (tod == null) return;

            float nowHours = tod.GetHoursPlayedNotPaused();
            GetVanillaInfectionRiskAreas(out HashSet<AfflictionBodyArea> activeAreas, out HashSet<AfflictionBodyArea> untreatedAreas, out HashSet<AfflictionBodyArea> treatedAreas);

            CureGeneratedSepsisRisksWithoutActiveInfectionRisk(activeAreas, treatedAreas);

            foreach (AfflictionBodyArea staleArea in s_SepsisInfectionRiskLastRollHours.Keys.ToList())
            {
                if (!untreatedAreas.Contains(staleArea)) s_SepsisInfectionRiskLastRollHours.Remove(staleArea);
            }

            float rollChance = GetSepsisInfectionRiskRollChance();
            if (rollChance <= 0f) return;

            foreach (AfflictionBodyArea area in untreatedAreas)
            {
                SepsisRiskAffliction? existingRisk = FindSepsisRiskForArea(area);
                if (existingRisk != null) continue;

                if (!s_SepsisInfectionRiskLastRollHours.TryGetValue(area, out float lastRollHour))
                {
                    s_SepsisInfectionRiskLastRollHours[area] = nowHours;
                    continue;
                }

                float elapsed = nowHours - lastRollHour;
                if (elapsed < SEPSIS_INFECTION_RISK_ROLL_INTERVAL_HOURS) continue;

                int rollCount = Mathf.FloorToInt(elapsed / SEPSIS_INFECTION_RISK_ROLL_INTERVAL_HOURS);
                if (rollCount <= 0) continue;

                for (int i = 0; i < rollCount; i++)
                {
                    float roll = UnityEngine.Random.Range(0f, 100f);
                    Core.Log($"InfectionRisk sepsis roll -> area={area} chance={rollChance:0.##}% roll={roll:0.##}");

                    if (roll <= rollChance)
                    {
                        new SepsisRiskAffliction(area, generatedFromInfectionRisk: true).Start();
                        AfflictionSaveHelper.QueueSurvivalSave();
                        Core.Log($"InfectionRisk sepsis roll succeeded -> SepsisRisk applied on {area}.");
                        break;
                    }
                }

                s_SepsisInfectionRiskLastRollHours[area] = lastRollHour + rollCount * SEPSIS_INFECTION_RISK_ROLL_INTERVAL_HOURS;
            }
        }

        internal static void ApplyOrBoostSepsisRiskFromVanillaInfection(AfflictionBodyArea bodyArea)
        {
            if (!Settings.options.EnableSepsis) return;

            SepsisRiskAffliction? existingRisk = FindSepsisRiskForArea(bodyArea);
            if (existingRisk != null)
            {
                if (existingRisk.GeneratedFromInfectionRisk)
                {
                    existingRisk.GeneratedFromInfectionRisk = false;
                    existingRisk.AddRiskProgress(SEPSIS_INFECTION_CONVERSION_RISK_BOOST);
                    s_SepsisInfectionRiskLastRollHours.Remove(bodyArea);
                    AfflictionSaveHelper.QueueSurvivalSave();
                    Core.Log($"Vanilla infection started on {bodyArea}; existing InfectionRisk-generated SepsisRisk boosted by {SEPSIS_INFECTION_CONVERSION_RISK_BOOST:0.#}% instead of reapplied.");
                    return;
                }

                Core.Log($"Vanilla infection started on {bodyArea}; SepsisRisk already exists, not reapplied.");
                return;
            }

            SepsisRiskAffliction newRisk = new(bodyArea);
            newRisk.AddRiskProgress(100f);
            newRisk.Start();
            AfflictionSaveHelper.QueueSurvivalSave();
            Core.Log($"Vanilla infection started on {bodyArea}, applying SepsisRisk at 100%.");
        }

        internal static bool IsVanillaInfectionRiskActiveFor(AfflictionBodyArea bodyArea)
        {
            GetVanillaInfectionRiskAreas(out HashSet<AfflictionBodyArea> activeAreas, out _, out _);
            return activeAreas.Contains(bodyArea);
        }

        internal static bool IsVanillaInfectionRiskTreatedFor(AfflictionBodyArea bodyArea)
        {
            GetVanillaInfectionRiskAreas(out _, out _, out HashSet<AfflictionBodyArea> treatedAreas);
            return treatedAreas.Contains(bodyArea);
        }

        private static void CureGeneratedSepsisRisksWithoutActiveInfectionRisk(HashSet<AfflictionBodyArea> activeAreas, HashSet<AfflictionBodyArea> treatedAreas)
        {
            var mgr = AfflictionManager.GetAfflictionManagerInstance();
            if (mgr?.m_Afflictions == null) return;

            for (int i = mgr.m_Afflictions.Count - 1; i >= 0; i--)
            {
                if (mgr.m_Afflictions[i] is not SepsisRiskAffliction sepsisRisk) continue;
                if (!sepsisRisk.GeneratedFromInfectionRisk) continue;

                AfflictionBodyArea area = sepsisRisk.BodyArea;

                if (IsVanillaInfectionActiveFor(area))
                {
                    sepsisRisk.GeneratedFromInfectionRisk = false;
                    continue;
                }

                if (treatedAreas.Contains(area) || !activeAreas.Contains(area))
                {
                    Core.Log($"SepsisRisk [{area}] -> linked InfectionRisk treated or removed, curing generated risk.");
                    sepsisRisk.Cure();
                }
            }
        }

        private static bool IsVanillaInfectionActiveFor(AfflictionBodyArea bodyArea)
        {
            Infection infection = GameManager.GetInfectionComponent();
            if (infection == null) return false;

            int count = infection.GetAfflictionsCount();
            for (int i = 0; i < count; i++)
            {
                if (infection.GetLocation(i) == bodyArea) return true;
            }

            return false;
        }

        private static SepsisRiskAffliction? FindSepsisRiskForArea(AfflictionBodyArea bodyArea)
        {
            var mgr = AfflictionManager.GetAfflictionManagerInstance();
            if (mgr?.m_Afflictions == null) return null;

            for (int i = 0; i < mgr.m_Afflictions.Count; i++)
            {
                if (mgr.m_Afflictions[i] is SepsisRiskAffliction sepsisRisk && sepsisRisk.BodyArea == bodyArea)
                    return sepsisRisk;
            }

            return null;
        }

        private static void GetVanillaInfectionRiskAreas(out HashSet<AfflictionBodyArea> activeAreas, out HashSet<AfflictionBodyArea> untreatedAreas, out HashSet<AfflictionBodyArea> treatedAreas)
        {
            activeAreas = [];
            untreatedAreas = [];
            treatedAreas = [];

            InfectionRisk infectionRisk = GameManager.GetInfectionRiskComponent();
            if (infectionRisk == null || !infectionRisk.HasInfectionRisk()) return;

            int count = infectionRisk.GetAfflictionsCount();
            for (int i = 0; i < count; i++)
            {
                AfflictionBodyArea area = infectionRisk.GetLocation(i);
                activeAreas.Add(area);

                bool treated = !infectionRisk.RequiresAntiseptic(i);
                if (treated) treatedAreas.Add(area);
                else untreatedAreas.Add(area);
            }
        }
    }
}