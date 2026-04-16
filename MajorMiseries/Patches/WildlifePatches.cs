using Il2CppTLD.AI;

namespace MajorMiseries.Patches
{
    internal static class WildlifePatches
    {
        static WildlifePatches()
        {
            Il2CppInterop.Runtime.Injection.ClassInjector.RegisterTypeInIl2Cpp<PredatorThreatController>();
        }

        private sealed class PendingKill
        {
            public AiSubType SubType;
            public DamageSource LastDamageSource;
        }

        public sealed class PredatorThreatController : MonoBehaviour
        {
            public PredatorThreatController(IntPtr ptr) : base(ptr) { }

            private BaseAi? _ai;
            private bool _initialized;
            private AiMode _lastLoggedMode = AiMode.None;
            private float _nextThinkTime = 0f;

            private float _nextForcedStalkAttemptTime = 0f;
            private float _nextForcedAttackAttemptTime = 0f;
            private float _suppressForceUntilTime = 0f;

            private float _nextForcedStalkLogTime = 0f;
            private float _nextForcedAttackLogTime = 0f;

            internal float _curiousFollowDistance;
            internal float _curiousEnterStalkingChance;

            internal float _stalkingFollowDistance;
            internal float _stalkingBeginChasingDistance;
            internal float _stalkingBeginChasingWeakTargetDistance;
            internal float _stalkingLoseInterestChance;
            internal float _stalkingChanceWhenTargetDetected;
            internal float _forceStalkPlayerDistance;

            internal float _passingAttackRange;

            internal float _smellRange;
            internal float _rangeFromScentFactor;
            internal float _rangeFromScentMax;

            internal void Initialize(BaseAi ai)
            {
                _ai = ai;

                if (_initialized)
                    return;

                CaptureBaseline();
                _initialized = true;

                ApplyPredatorThreatValues();
            }

            private void CaptureBaseline()
            {
                if (_ai == null)
                    return;

                _curiousFollowDistance = _ai.m_CuriousFollowDistance;
                _curiousEnterStalkingChance = _ai.m_CuriousEnterStalkingChance;

                _stalkingFollowDistance = _ai.m_StalkingFollowDistance;
                _stalkingBeginChasingDistance = _ai.m_StalkingBeginChasingDistance;
                _stalkingBeginChasingWeakTargetDistance = _ai.m_StalkingBeginChasingWeakTargetDistance;
                _stalkingLoseInterestChance = _ai.m_StalkingLoseInterestChance;
                _stalkingChanceWhenTargetDetected = _ai.m_StalkingChanceWhenTargetDetected;
                _forceStalkPlayerDistance = _ai.m_ForceStalkPlayerDistance;

                _passingAttackRange = _ai.m_PassingAttackRange;

                _smellRange = _ai.m_SmellRange;
                _rangeFromScentFactor = _ai.m_RangeFromScentFactor;
                _rangeFromScentMax = _ai.m_RangeFromScentMax;
            }

            private void LateUpdate()
            {
                if (_ai == null)
                    return;

                if (!IsPredatorThreatTracked(_ai))
                    return;

                if (GameManager.m_IsPaused || GameManager.s_IsGameplaySuspended || GameManager.s_IsAISuspended)
                    return;

                if (_ai.m_CurrentHP <= 0f || _ai.GetAiMode() == AiMode.Dead)
                    return;

                ApplyPredatorThreatValues();
                MaybeLogPredatorThreatThreshold(_ai, this);
                MaybeLogPredatorModeTransition();

                if (Time.time < _nextThinkTime)
                    return;

                _nextThinkTime = Time.time + 0.20f;

                TryForceThreatBehavior();
            }

            private void ApplyPredatorThreatValues()
            {
                if (_ai == null)
                    return;

                float smellMultiplier = AfflictionLogic.GetPredatorSmellDistanceMultiplier();
                float stalkMultiplier = AfflictionLogic.GetPredatorRushDistanceMultiplier();
                float chanceMultiplier = 1f + ((stalkMultiplier - 1f) * 0.5f);

                _ai.m_CuriousFollowDistance = _curiousFollowDistance * stalkMultiplier;
                _ai.m_CuriousEnterStalkingChance = Mathf.Clamp(_curiousEnterStalkingChance * chanceMultiplier, 0f, 100f);

                _ai.m_StalkingFollowDistance = _stalkingFollowDistance * stalkMultiplier;
                _ai.m_CurrentStalkingFollowDistance = _ai.m_StalkingFollowDistance;

                _ai.m_StalkingBeginChasingDistance = _stalkingBeginChasingDistance * stalkMultiplier;
                _ai.m_StalkingBeginChasingWeakTargetDistance = _stalkingBeginChasingWeakTargetDistance * stalkMultiplier;

                _ai.m_StalkingChanceWhenTargetDetected =
                    Mathf.Clamp(Mathf.RoundToInt(_stalkingChanceWhenTargetDetected * chanceMultiplier), 0, 100);

                _ai.m_StalkingLoseInterestChance =
                    Mathf.Clamp(_stalkingLoseInterestChance / Mathf.Max(1f, stalkMultiplier), 0f, 100f);

                _ai.m_ForceStalkPlayerDistance = _forceStalkPlayerDistance * stalkMultiplier;
                _ai.m_PassingAttackRange = _passingAttackRange * stalkMultiplier;

                if (UsesForcedWolfThreatBehavior(_ai))
                {
                    _ai.m_SmellRange = _smellRange * smellMultiplier;
                    _ai.m_RangeFromScentFactor = _rangeFromScentFactor * smellMultiplier;
                    _ai.m_RangeFromScentMax = _rangeFromScentMax * smellMultiplier;
                }
            }

            private void TryForceThreatBehavior()
            {
                if (_ai == null)
                    return;

                if (!UsesForcedWolfThreatBehavior(_ai))
                    return;

                if (Time.time < _suppressForceUntilTime)
                    return;

                AiMode currentMode = _ai.GetAiMode();

                if (currentMode == AiMode.Dead
                    || currentMode == AiMode.Struggle
                    || currentMode == AiMode.Flee
                    || currentMode == AiMode.PassingAttack
                    || currentMode == AiMode.Stunned
                    || currentMode == AiMode.ScriptedSequence)
                {
                    return;
                }

                GameObject playerObject = GameManager.GetPlayerObject();
                if (playerObject == null)
                    return;

                AiTarget? playerTarget = playerObject.GetComponent<AiTarget>();
                if (playerTarget == null)
                    return;

                float distanceToPlayer = Vector3.Distance(_ai.transform.position, playerObject.transform.position);

                bool playerReachable = _ai.CanPlayerBeReached(
                    playerObject.transform.position,
                    MoveAgent.PathRequirement.FullPath
                );

                if (currentMode == AiMode.Investigate || currentMode == AiMode.InvestigateSmell)
                {
                    if (Time.time < _nextForcedStalkAttemptTime)
                        return;

                    float forcedStalkDistance = GetForcedWolfStalkingDistance();
                    if (forcedStalkDistance <= 0f)
                        return;

                    if (!playerReachable)
                        return;

                    if (distanceToPlayer > forcedStalkDistance)
                        return;

                    _ai.m_CurrentTarget = playerTarget;

                    if (!_ai.CanSeeTarget())
                        return;

                    if (!_ai.CanEnterStalking())
                        return;

                    _ai.SetAiMode(AiMode.Stalking);
                    _nextForcedStalkAttemptTime = Time.time + 1.5f;

                    if (Time.time >= _nextForcedStalkLogTime)
                    {
                        Core.Log($"{GetPredatorName(_ai.m_AiSubType)} #{_ai.GetInstanceID()} forced to STALKING at {distanceToPlayer:0.0} m");
                        _nextForcedStalkLogTime = Time.time + 2f;
                    }

                    return;
                }

                if (currentMode == AiMode.Stalking)
                {
                    if (Time.time < _nextForcedAttackAttemptTime)
                        return;

                    float forcedAttackDistance = GetForcedWolfAttackDistance();
                    if (forcedAttackDistance <= 0f)
                        return;

                    if (!playerReachable)
                        return;

                    if (distanceToPlayer > forcedAttackDistance)
                        return;

                    if (_ai.m_CurrentTarget == null || !_ai.m_CurrentTarget.IsPlayer())
                    {
                        _ai.m_CurrentTarget = playerTarget;
                    }

                    if (!_ai.CanSeeTarget())
                        return;

                    if (_ai.m_TimeInModeSeconds < 0.75f)
                        return;

                    _ai.SetAiMode(AiMode.Attack);
                    _nextForcedAttackAttemptTime = Time.time + 2f;

                    if (Time.time >= _nextForcedAttackLogTime)
                    {
                        Core.Log($"{GetPredatorName(_ai.m_AiSubType)} #{_ai.GetInstanceID()} forced to ATTACK at {distanceToPlayer:0.0} m | TimeInMode={_ai.m_TimeInModeSeconds:0.0}s");
                        _nextForcedAttackLogTime = Time.time + 2f;
                    }
                }
            }

            private void MaybeLogPredatorModeTransition()
            {
                if (_ai == null)
                    return;

                int key = _ai.GetInstanceID();
                AiMode currentMode = _ai.GetAiMode();

                if (_lastLoggedMode == AiMode.None)
                {
                    _lastLoggedMode = currentMode;
                    return;
                }

                if (currentMode == _lastLoggedMode)
                    return;

                AiMode lastMode = _lastLoggedMode;
                _lastLoggedMode = currentMode;

                if (UsesForcedWolfThreatBehavior(_ai) && lastMode == AiMode.Attack && currentMode == AiMode.Wander)
                {
                    _suppressForceUntilTime = Time.time + 4f;
                    _nextForcedStalkAttemptTime = Time.time + 2f;
                    _nextForcedAttackAttemptTime = Time.time + 2f;
                }

                if (!IsInterestingPredatorMode(lastMode) && !IsInterestingPredatorMode(currentMode))
                    return;

                float distance = GetDistanceToPlayer(_ai);
                int threat = AfflictionLogic.GetTotalPredatorThreatLevel();

                if (distance >= 0f)
                {
                    Core.Log($"{GetPredatorName(_ai.m_AiSubType)} #{key} : {lastMode} -> {currentMode} at {distance:0.0} m | Threat={threat}");
                }
                else
                {
                    Core.Log($"{GetPredatorName(_ai.m_AiSubType)} #{key} : {lastMode} -> {currentMode} | Threat={threat}");
                }
            }

            private static float GetForcedWolfStalkingDistance()
            {
                float t = GetThreatLerp01();
                return Mathf.Lerp(70f, 90f, t);
            }

            private static float GetForcedWolfAttackDistance()
            {
                float t = GetThreatLerp01();
                return Mathf.Lerp(35f, 50f, t);
            }
        }

        private static readonly Dictionary<int, PendingKill> _pendingPredatorKills = new();

        private static int _lastLoggedPredatorThreatLevel = -1;

        internal static void ResetRuntime()
        {
            _pendingPredatorKills.Clear();
            _lastLoggedPredatorThreatLevel = -1;
            Core.Log("predator hostility tracker reset");
        }

        private static bool TryGetTrackedPredatorSubtype(BaseAi ai, out AiSubType subType)
        {
            subType = AiSubType.None;

            if (ai == null)
                return false;

            subType = ai.m_AiSubType;

            return subType == AiSubType.Wolf
                || subType == AiSubType.Bear
                || subType == AiSubType.Moose
                || subType == AiSubType.Cougar;
        }

        private static bool IsPredatorThreatTracked(BaseAi ai)
        {
            return TryGetTrackedPredatorSubtype(ai, out _);
        }

        private static bool UsesForcedWolfThreatBehavior(BaseAi ai)
        {
            if (ai == null)
                return false;

            return ai is AiBaseWolf || ai.m_AiSubType == AiSubType.Wolf;
        }

        private static float GetThreatLerp01()
        {
            return Mathf.InverseLerp(4f, 12f, AfflictionLogic.GetTotalPredatorThreatLevel());
        }

        private static string GetPredatorName(AiSubType subType)
        {
            return subType switch
            {
                AiSubType.Wolf => "Wolf",
                AiSubType.Bear => "Bear",
                AiSubType.Moose => "Moose",
                AiSubType.Cougar => "Cougar",
                _ => subType.ToString()
            };
        }

        private static float GetPredatorHostilityGain(AiSubType subType)
        {
            return subType switch
            {
                AiSubType.Wolf => 1f,
                AiSubType.Moose => 2f,
                AiSubType.Bear => 3f,
                AiSubType.Cougar => 4f,
                _ => 0f
            };
        }

        private static PredatorThreatController? EnsureThreatController(BaseAi ai)
        {
            if (!IsPredatorThreatTracked(ai))
                return null;

            PredatorThreatController? controller = ai.gameObject.GetComponent<PredatorThreatController>() ?? ai.gameObject.AddComponent<PredatorThreatController>();
            controller.Initialize(ai);
            return controller;
        }

        private static void TrackPredatorPlayerDamage(BaseAi ai, DamageSource damageSource, string sourceHook)
        {
            if (!TryGetTrackedPredatorSubtype(ai, out AiSubType subType))
                return;

            if (damageSource != DamageSource.Player)
                return;

            if (ai.m_CurrentHP <= 0f)
                return;

            int key = ai.GetInstanceID();

            _pendingPredatorKills[key] = new PendingKill
            {
                SubType = subType,
                LastDamageSource = damageSource
            };

            Core.Log($"predator hostility track -> {GetPredatorName(subType)} marked as player-damaged ({sourceHook})");
        }

        private static void TryRegisterPredatorKill(BaseAi ai, string hookName)
        {
            if (!TryGetTrackedPredatorSubtype(ai, out _))
                return;

            int key = ai.GetInstanceID();

            if (!_pendingPredatorKills.TryGetValue(key, out PendingKill pending))
                return;

            _pendingPredatorKills.Remove(key);

            float hostilityAdded = GetPredatorHostilityGain(pending.SubType);
            if (hostilityAdded <= 0f)
                return;

            Core.Log($"predator hostility death -> {GetPredatorName(pending.SubType)} confirmed from {hookName}");
            AfflictionLogic.RegisterPredatorKill(hostilityAdded);
        }

        private static bool IsInterestingPredatorMode(AiMode mode)
        {
            return mode == AiMode.Investigate
                || mode == AiMode.InvestigateSmell
                || mode == AiMode.Stalking
                || mode == AiMode.Attack
                || mode == AiMode.PassingAttack
                || mode == AiMode.HoldGround;
        }

        private static float GetDistanceToPlayer(BaseAi ai)
        {
            if (ai == null)
                return -1f;

            GameObject playerObject = GameManager.GetPlayerObject();
            if (playerObject == null)
                return -1f;

            return Vector3.Distance(ai.transform.position, playerObject.transform.position);
        }

        private static void MaybeLogPredatorThreatThreshold(BaseAi ai, PredatorThreatController controller)
        {
            if (!IsPredatorThreatTracked(ai))
                return;

            int baseThreat = AfflictionLogic.GetBasePredatorThreatLevel();
            int dynamicThreat = AfflictionLogic.GetDynamicPredatorThreatLevel();
            int totalThreat = AfflictionLogic.GetTotalPredatorThreatLevel();

            if (totalThreat == _lastLoggedPredatorThreatLevel)
                return;

            _lastLoggedPredatorThreatLevel = totalThreat;

            float smellMultiplier = AfflictionLogic.GetPredatorSmellDistanceMultiplier();
            float stalkMultiplier = AfflictionLogic.GetPredatorRushDistanceMultiplier();
            float chanceMultiplier = 1f + ((stalkMultiplier - 1f) * 0.5f);

            float newCuriousFollow = controller._curiousFollowDistance * stalkMultiplier;
            float newCuriousEnter = Mathf.Clamp(controller._curiousEnterStalkingChance * chanceMultiplier, 0f, 100f);

            float newStalkingFollow = controller._stalkingFollowDistance * stalkMultiplier;
            float newBeginChase = controller._stalkingBeginChasingDistance * stalkMultiplier;
            float newWeakBeginChase = controller._stalkingBeginChasingWeakTargetDistance * stalkMultiplier;
            int newTargetDetected = Mathf.Clamp(Mathf.RoundToInt(controller._stalkingChanceWhenTargetDetected * chanceMultiplier), 0, 100);
            float newLoseInterest = Mathf.Clamp(controller._stalkingLoseInterestChance / Mathf.Max(1f, stalkMultiplier), 0f, 100f);
            float newForceStalk = controller._forceStalkPlayerDistance * stalkMultiplier;
            float newPassingAttack = controller._passingAttackRange * stalkMultiplier;

            Core.Log($"predator threat threshold -> Base:{baseThreat} | Dynamic:{dynamicThreat} | Total:{totalThreat} | Smell x{smellMultiplier:0.00} | Stalk x{stalkMultiplier:0.00}");

            if (UsesForcedWolfThreatBehavior(ai))
            {
                float newSmellRange = controller._smellRange * smellMultiplier;
                float newRangeFromScentFactor = controller._rangeFromScentFactor * smellMultiplier;
                float newRangeFromScentMax = controller._rangeFromScentMax * smellMultiplier;

                Core.Log(
                    $"predator smell ranges -> {GetPredatorName(ai.m_AiSubType)} | " +
                    $"SmellRange {controller._smellRange:0.0}->{newSmellRange:0.0} | " +
                    $"RangeFromScentFactor {controller._rangeFromScentFactor:0.00}->{newRangeFromScentFactor:0.00} | " +
                    $"RangeFromScentMax {controller._rangeFromScentMax:0.0}->{newRangeFromScentMax:0.0}"
                );
            }

            Core.Log(
                $"predator stalking ranges -> {GetPredatorName(ai.m_AiSubType)} | " +
                $"CuriousFollow {controller._curiousFollowDistance:0.0}->{newCuriousFollow:0.0} | " +
                $"StalkFollow {controller._stalkingFollowDistance:0.0}->{newStalkingFollow:0.0} | " +
                $"BeginChase {controller._stalkingBeginChasingDistance:0.0}->{newBeginChase:0.0} | " +
                $"WeakBeginChase {controller._stalkingBeginChasingWeakTargetDistance:0.0}->{newWeakBeginChase:0.0} | " +
                $"ForceStalk {controller._forceStalkPlayerDistance:0.0}->{newForceStalk:0.0} | " +
                $"PassingAttack {controller._passingAttackRange:0.0}->{newPassingAttack:0.0}"
            );

            Core.Log(
                $"predator stalking chances -> {GetPredatorName(ai.m_AiSubType)} | " +
                $"CuriousEnterStalk {controller._curiousEnterStalkingChance:0.00}->{newCuriousEnter:0.00} | " +
                $"TargetDetectedStalk {controller._stalkingChanceWhenTargetDetected:0.00}->{newTargetDetected:0.00} | " +
                $"LoseInterest {controller._stalkingLoseInterestChance:0.00}->{newLoseInterest:0.00}"
            );
        }

        // ------------------------------------------------------------------------
        //                                PATCHES
        // ------------------------------------------------------------------------

        [HarmonyPatch(typeof(BaseAi), nameof(BaseAi.Start))]
        internal static class BaseAi_Start_Patch
        {
            private static void Postfix(BaseAi __instance)
            {
                EnsureThreatController(__instance);
            }
        }

        [HarmonyPatch(typeof(BaseAi), nameof(BaseAi.Update))]
        internal static class BaseAi_Update_EnsureController_Patch
        {
            private static void Prefix(BaseAi __instance)
            {
                EnsureThreatController(__instance);
            }
        }

        [HarmonyPatch(typeof(BaseAi), nameof(BaseAi.IsDamageFatal))]
        internal static class BaseAi_IsDamageFatal_Patch
        {
            private static void Prefix(BaseAi __instance, DamageSource damageSource)
            {
                TrackPredatorPlayerDamage(__instance, damageSource, "BaseAi.IsDamageFatal");
            }
        }

        [HarmonyPatch(typeof(BaseAi), nameof(BaseAi.ApplyDamage), typeof(float), typeof(DamageSource), typeof(string))]
        internal static class BaseAi_ApplyDamage_ShortPatch
        {
            private static void Prefix(BaseAi __instance, DamageSource damageSource)
            {
                TrackPredatorPlayerDamage(__instance, damageSource, "BaseAi.ApplyDamage(short)");
            }
        }

        [HarmonyPatch(typeof(BaseAi), nameof(BaseAi.ApplyDamage), typeof(float), typeof(float), typeof(DamageSource), typeof(string))]
        internal static class BaseAi_ApplyDamage_FullPatch
        {
            private static void Prefix(BaseAi __instance, DamageSource damageSource)
            {
                TrackPredatorPlayerDamage(__instance, damageSource, "BaseAi.ApplyDamage(full)");
            }
        }

        [HarmonyPatch(typeof(BaseAi), nameof(BaseAi.EnterDead))]
        internal static class BaseAi_EnterDead_Patch
        {
            private static void Prefix(BaseAi __instance)
            {
                TryRegisterPredatorKill(__instance, "BaseAi.EnterDead");
            }
        }

        [HarmonyPatch(typeof(AiCougar), nameof(AiCougar.EnterDead))]
        internal static class AiCougar_EnterDead_Patch
        {
            private static void Prefix(AiCougar __instance)
            {
                TryRegisterPredatorKill(__instance, "AiCougar.EnterDead");
            }
        }

        [HarmonyPatch(typeof(BaseAi), nameof(BaseAi.OnDestroy))]
        internal static class BaseAi_OnDestroy_Patch
        {
            private static void Prefix(BaseAi __instance)
            {
                if (__instance == null)
                    return;

                int key = __instance.GetInstanceID();
                _pendingPredatorKills.Remove(key);
            }
        }
    }
}