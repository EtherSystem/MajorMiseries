using System.Collections;
using static MajorMiseries.Afflictions.BrokenArm;
using static MajorMiseries.Afflictions.BrokenLeg;
using Random = UnityEngine.Random;

namespace MajorMiseries
{
    internal static partial class AfflictionLogic
    {
        // ======================================================================================
        //                               Broken Leg & Arm Logic
        // ======================================================================================

        private const int BROKEN_LEG_MIN_HOURS = 1008;
        private const int BROKEN_LEG_MAX_HOURS = 2016;
        private const int BROKEN_ARM_MIN_HOURS = 1008;
        private const int BROKEN_ARM_MAX_HOURS = 1344;

        private enum BrokenLegCause
        {
            Generic = 0,
            FallDamage = 1,
            BearAttack = 2,
            MooseAttack = 3
        }

        private static string GetBrokenLegCauseKey(BrokenLegCause cause)
        {
            return cause switch
            {
                BrokenLegCause.FallDamage => "GAMEPLAY_BrokenLegCause_FallDamage",
                BrokenLegCause.BearAttack => "GAMEPLAY_BrokenLegCause_BearAttack",
                BrokenLegCause.MooseAttack => "GAMEPLAY_BrokenLegCause_MooseAttack",
                _ => "GAMEPLAY_BrokenLegCause"
            };
        }

        internal static void TryApplyBearStruggleFracture()
        {
            if (!IsReady()) return;

            PlayerManager player = GameManager.GetPlayerManagerComponent();
            if (player == null || player.PlayerIsDead()) return;

            float chance = Settings.options.BearBrokenLimbChance;

            Fatigue fatigue = GameManager.GetFatigueComponent();
            if (fatigue != null && fatigue.m_CurrentFatigue <= 40f) chance += 10f;

            if (!RollChance(chance))
            {
                Core.Log($"bear struggle fracture avoided ({chance:0.#}%)");
                return;
            }

            Core.Log($"bear struggle fracture triggered ({chance:0.#}%)");
            ApplyRandomBrokenLimb("Bear Attack", BrokenLegCause.BearAttack);
        }

        internal static void TryApplyMooseStruggleFracture()
        {
            if (!IsReady()) return;

            PlayerManager player = GameManager.GetPlayerManagerComponent();
            if (player == null || player.PlayerIsDead()) return;

            float chance = Settings.options.MooseBrokenLimbChance;

            Fatigue fatigue = GameManager.GetFatigueComponent();
            if (fatigue != null && fatigue.m_CurrentFatigue <= 40f) chance += 10f;

            if (!RollChance(chance))
            {
                Core.Log($"moose struggle fracture avoided ({chance:0.#}%)");
                return;
            }

            Core.Log($"moose struggle fracture triggered ({chance:0.#}%)");
            ApplyRandomBrokenLimb("Moose Attack", BrokenLegCause.MooseAttack);
        }

        private static void ApplyRandomBrokenLimb(string causeText, BrokenLegCause brokenLegCause)
        {
            if (Settings.options.AllowDoubleBrokenLimb)
            {
                AfflictionBodyArea armArea = Random.Range(0, 2) == 0 ? AfflictionBodyArea.ArmLeft : AfflictionBodyArea.ArmRight;

                AfflictionBodyArea legArea = Random.Range(0, 2) == 0 ? AfflictionBodyArea.LegLeft : AfflictionBodyArea.LegRight;

                float armDuration = Random.Range(BROKEN_ARM_MIN_HOURS, BROKEN_ARM_MAX_HOURS + 1);
                float legDuration = Random.Range(BROKEN_LEG_MIN_HOURS, BROKEN_LEG_MAX_HOURS + 1);

                if (Settings.options.BrokenLimbDurationMode == 1)
                {
                    armDuration /= 10f;
                    legDuration /= 10f;
                }

                Core.Log($"{causeText} -> applying BrokenArm ({armArea}) for {armDuration:0.#}h and BrokenLeg ({legArea}) for {legDuration:0.#}h");

                new BrokenArmAffliction(armArea, armDuration).Start();
                new BrokenLegAffliction(legArea, legDuration, GetBrokenLegCauseKey(brokenLegCause)).Start();
                AfflictionSaveHelper.QueueSurvivalSave();
                return;
            }

            bool applyArm = Random.Range(0, 2) == 0;

            if (applyArm)
            {
                AfflictionBodyArea armArea = Random.Range(0, 2) == 0 ? AfflictionBodyArea.ArmLeft : AfflictionBodyArea.ArmRight;

                float armDuration = Random.Range(BROKEN_ARM_MIN_HOURS, BROKEN_ARM_MAX_HOURS + 1);

                if (Settings.options.BrokenLimbDurationMode == 1)
                {
                    armDuration /= 10f;
                }

                Core.Log($"{causeText} -> applying BrokenArm ({armArea}) for {armDuration:0.#}h");
                new BrokenArmAffliction(armArea, armDuration).Start();
                AfflictionSaveHelper.QueueSurvivalSave();
            }
            else
            {
                AfflictionBodyArea legArea = Random.Range(0, 2) == 0 ? AfflictionBodyArea.LegLeft : AfflictionBodyArea.LegRight;

                float legDuration = Random.Range(BROKEN_LEG_MIN_HOURS, BROKEN_LEG_MAX_HOURS + 1);

                if (Settings.options.BrokenLimbDurationMode == 1)
                {
                    legDuration /= 10f;
                }

                Core.Log($"{causeText} -> applying BrokenLeg ({legArea}) for {legDuration:0.#}h");
                new BrokenLegAffliction(legArea, legDuration, GetBrokenLegCauseKey(brokenLegCause)).Start();
                AfflictionSaveHelper.QueueSurvivalSave();
            }
        }

        internal static float GetBrokenLegCarryCapacityMultiplier()
        {
            RefreshEffectsIfNeeded();

            return _cache.BrokenLegCount switch
            {
                >= 2 => 0.50f,
                1 => 0.75f,
                _ => 1f
            };
        }

        internal static bool HasBrokenArm()
        {
            RefreshEffectsIfNeeded();
            return _cache.BrokenArmCount > 0;
        }

        internal static bool HasBrokenLeg()
        {
            RefreshEffectsIfNeeded();
            return _cache.BrokenLegCount > 0;
        }

        internal static bool HasBothBrokenLegs()
        {
            RefreshEffectsIfNeeded();
            return _cache.BrokenLegLeft && _cache.BrokenLegRight;
        }

        internal static bool HasBothBrokenArms()
        {
            RefreshEffectsIfNeeded();
            return _cache.BrokenArmLeft && _cache.BrokenArmRight;
        }

        // ===========================================================================
        //                          BrokenLeg fall logic
        // ===========================================================================

        private static bool _processingFallInjury;
        private static float _conditionBeforeFall = -1f;

        private const float FALL_BROKEN_LEG_CHANCE_THRESHOLD = 0.15f;
        private const float FALL_BROKEN_LEG_GUARANTEED_THRESHOLD = 0.25f;
        private const float FALL_BROKEN_LEG_CHANCE = 35f;

        internal static void BeginFallDamageEvaluation()
        {
            if (!IsReady())
            {
                _conditionBeforeFall = -1f;
                return;
            }

            Condition cond = GameManager.GetConditionComponent();
            if (cond == null)
            {
                _conditionBeforeFall = -1f;
                return;
            }

            _conditionBeforeFall = cond.GetNormalizedCondition();
        }

        internal static void EndFallDamageEvaluation()
        {
            if (_processingFallInjury) return;

            if (!IsReady()) return;

            if (_conditionBeforeFall < 0f) return;

            MelonCoroutines.Start(EvaluateFallDamageNextFrame());
        }

        private static IEnumerator EvaluateFallDamageNextFrame()
        {
            yield return null;

            if (_processingFallInjury) yield break;

            if (!IsReady()) yield break;

            PlayerManager player = GameManager.GetPlayerManagerComponent();
            if (player == null || player.PlayerIsDead()) yield break;

            Condition cond = GameManager.GetConditionComponent();
            if (cond == null) yield break;

            if (_conditionBeforeFall < 0f) yield break;

            float conditionAfter = cond.GetNormalizedCondition();
            float lost = Mathf.Max(0f, _conditionBeforeFall - conditionAfter);

            if (lost >= FALL_BROKEN_LEG_CHANCE_THRESHOLD) Core.Log($"fall damage delayed end -> conditionBefore={_conditionBeforeFall:0.###}, conditionAfter={conditionAfter:0.###}, lost={lost:0.###}");

            _conditionBeforeFall = -1f;

            TryApplyBrokenLegFromFallDamage(lost);
        }

        private static void TryApplyBrokenLegFromFallDamage(float normalizedConditionLost)
        {
            if (_processingFallInjury) return;

            if (!IsReady()) return;

            PlayerManager player = GameManager.GetPlayerManagerComponent();
            if (player == null || player.PlayerIsDead()) return;

            RefreshEffectsIfNeeded(force: true);

            if (_cache.BrokenLegLeft && _cache.BrokenLegRight)
            {
                Core.Log("fall fracture skipped -> both legs already broken");
                return;
            }

            bool guaranteed = normalizedConditionLost >= FALL_BROKEN_LEG_GUARANTEED_THRESHOLD;
            bool eligibleChance = normalizedConditionLost >= FALL_BROKEN_LEG_CHANCE_THRESHOLD;

            if (!guaranteed && !eligibleChance)
            {
                if (normalizedConditionLost > 0.001f) Core.Log($"fall fracture skipped -> damage too low ({normalizedConditionLost:0.###})");
                return;
            }

            if (!guaranteed && !RollChance(FALL_BROKEN_LEG_CHANCE))
            {
                Core.Log($"fall fracture avoided -> rolled against {FALL_BROKEN_LEG_CHANCE:0.#}%");
                return;
            }

            _processingFallInjury = true;

            try
            {
                ApplyBrokenLegFromFall("Fall Damage", normalizedConditionLost);
            }
            finally
            {
                _processingFallInjury = false;
            }
        }

        private static void ApplyBrokenLegFromFall(string cause, float normalizedConditionLost)
        {
            RefreshEffectsIfNeeded(force: true);

            AfflictionBodyArea legArea;

            if (_cache.BrokenLegLeft && !_cache.BrokenLegRight) legArea = AfflictionBodyArea.LegRight;
            else if (_cache.BrokenLegRight && !_cache.BrokenLegLeft) legArea = AfflictionBodyArea.LegLeft;
            else legArea = Random.Range(0, 2) == 0 ? AfflictionBodyArea.LegLeft : AfflictionBodyArea.LegRight;

            float duration = Random.Range(BROKEN_LEG_MIN_HOURS, BROKEN_LEG_MAX_HOURS + 1);

            if (Settings.options.BrokenLimbDurationMode == 1) duration /= 10f;

            Core.Log($"{cause} -> applying BrokenLeg ({legArea}) for {duration:0.#}h after losing {normalizedConditionLost * 100f:0.#}% condition");

            new BrokenLegAffliction(legArea, duration, GetBrokenLegCauseKey(BrokenLegCause.FallDamage)).Start();
            AfflictionSaveHelper.QueueSurvivalSave();
            ForceRefreshEffects();
        }
    }
}