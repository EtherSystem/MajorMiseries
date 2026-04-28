using AfflictionComponent.Components;
using MajorMiseries.Persistence;
using static MajorMiseries.Afflictions.SevereAnkleSprain;
using static MajorMiseries.Afflictions.SevereAnkleSprainRisk;
using static MajorMiseries.Afflictions.SevereWristSprain;
using static MajorMiseries.Afflictions.SevereWristSprainRisk;

namespace MajorMiseries
{
    internal enum SevereSprainKind
    {
        Wrist,
        Ankle
    }

    internal enum SevereSprainJoint
    {
        None = 0,
        LeftWrist,
        RightWrist,
        LeftAnkle,
        RightAnkle
    }

    internal static class SevereSprainLogic
    {
        private const float RISK_START_VALUE = 99f;

        private static int SprainsRequiredForRisk
        {
            get
            {
                return Settings.options.SevereSprainPreset switch
                {
                    0 => 3, // Forgiving
                    1 => 2, // Standard
                    2 => 2, // Harsh
                    3 => 1, // Brutal
                    _ => 2
                };
            }
        }

        private static float TrackingWindowHours
        {
            get
            {
                return Settings.options.SevereSprainPreset switch
                {
                    0 => 120f, // Forgiving: 3 sprains within 5 days
                    1 => 72f,  // Standard: 2 sprains within 3 days
                    2 => 96f,  // Harsh: 2 sprains within 4 days
                    3 => 120f, // Brutal: 1 sprain within 5 days
                    _ => 72f
                };
            }
        }

        private static float RiskDurationHours
        {
            get
            {
                return Settings.options.SevereSprainPreset switch
                {
                    0 => 18f, // Forgiving
                    1 => 24f, // Standard
                    2 => 36f, // Harsh
                    3 => 48f, // Brutal
                    _ => 24f
                };
            }
        }

        internal static float SevereSprainDurationHours
        {
            get
            {
                return Settings.options.SevereSprainPreset switch
                {
                    0 => 48f,  // Forgiving
                    1 => 72f,  // Standard
                    2 => 96f,  // Harsh
                    3 => 120f, // Brutal
                    _ => 72f
                };
            }
        }

        internal static void ResetRuntime()
        {
            // Persistent values live in MMState. Nothing runtime-only for now.
        }

        internal static bool OnVanillaSprainStarted(SevereSprainKind kind, AfflictionBodyArea bodyArea)
        {
            if (!Settings.options.EnableSevereSprains) return false;

            SevereSprainJoint joint = GetJoint(kind, bodyArea);
            if (joint == SevereSprainJoint.None)
            {
                Core.Log($"SevereSprain ignored unknown body area -> kind={kind}, bodyArea={bodyArea}");
                return false;
            }

            Core.State ??= new MMState();

            if (HasSevereSprain(kind, bodyArea))
            {
                ClearRisk(joint);
                ResetTrackingWindow(joint);
                ApplySevereSprain(kind, bodyArea, "existing severe sprain reset");
                Core.Instance?.MarkDirty();
                return true;
            }

            if (GetRisk(joint) > 0f)
            {
                ClearRisk(joint);
                ResetTrackingWindow(joint);
                CureRiskAffliction(kind, bodyArea);
                ApplySevereSprain(kind, bodyArea, "risk converted");
                Core.Instance?.MarkDirty();
                return true;
            }

            RecordSprainForRisk(kind, bodyArea, joint);
            return false;
        }

        internal static void Update(float gameHoursPassed)
        {
            if (gameHoursPassed <= 0f) return;

            Core.State ??= new MMState();

            if (!Settings.options.EnableSevereSprains)
            {
                if (HasAnyStoredValue())
                {
                    ClearAllStoredValues();
                    Core.Instance?.MarkDirty();
                }

                CureAllAfflictionsOfType<SevereWristSprainRiskAffliction>();
                CureAllAfflictionsOfType<SevereAnkleSprainRiskAffliction>();
                CureAllAfflictionsOfType<SevereWristSprainAffliction>();
                CureAllAfflictionsOfType<SevereAnkleSprainAffliction>();
                return;
            }

            bool changed = false;

            changed |= UpdateJointTimers(SevereSprainJoint.LeftWrist, gameHoursPassed);
            changed |= UpdateJointTimers(SevereSprainJoint.RightWrist, gameHoursPassed);
            changed |= UpdateJointTimers(SevereSprainJoint.LeftAnkle, gameHoursPassed);
            changed |= UpdateJointTimers(SevereSprainJoint.RightAnkle, gameHoursPassed);

            if (changed) Core.Instance?.MarkDirty();
        }

        internal static void SyncFromState()
        {
            Core.State ??= new MMState();

            if (!Settings.options.EnableSevereSprains)
            {
                ClearAllStoredValues();
                CureAllAfflictionsOfType<SevereWristSprainRiskAffliction>();
                CureAllAfflictionsOfType<SevereAnkleSprainRiskAffliction>();
                CureAllAfflictionsOfType<SevereWristSprainAffliction>();
                CureAllAfflictionsOfType<SevereAnkleSprainAffliction>();
                Core.Instance?.MarkDirty();
                return;
            }

            bool changed = false;

            changed |= SyncJointFromPreset(SevereSprainJoint.LeftWrist, SevereSprainKind.Wrist, AfflictionBodyArea.HandLeft);
            changed |= SyncJointFromPreset(SevereSprainJoint.RightWrist, SevereSprainKind.Wrist, AfflictionBodyArea.HandRight);
            changed |= SyncJointFromPreset(SevereSprainJoint.LeftAnkle, SevereSprainKind.Ankle, AfflictionBodyArea.FootLeft);
            changed |= SyncJointFromPreset(SevereSprainJoint.RightAnkle, SevereSprainKind.Ankle, AfflictionBodyArea.FootRight);

            if (changed) Core.Instance?.MarkDirty();
        }

        internal static void ClampState()
        {
            Core.State ??= new MMState();

            ClampJoint(SevereSprainJoint.LeftWrist);
            ClampJoint(SevereSprainJoint.RightWrist);
            ClampJoint(SevereSprainJoint.LeftAnkle);
            ClampJoint(SevereSprainJoint.RightAnkle);
        }

        internal static float GetRiskValue(SevereSprainKind kind, AfflictionBodyArea bodyArea)
        {
            return GetRisk(GetJoint(kind, bodyArea));
        }

        internal static void DevApplyRisk(SevereSprainKind kind, AfflictionBodyArea bodyArea)
        {
            if (!Settings.options.EnableSevereSprains)
            {
                Core.Log("SevereSprain dev command ignored: Severe Sprains are disabled.");
                return;
            }

            Core.State ??= new MMState();

            SevereSprainJoint joint = GetJoint(kind, bodyArea);
            if (joint == SevereSprainJoint.None)
            {
                Core.Log($"SevereSprain dev command ignored unknown body area -> kind={kind}, bodyArea={bodyArea}");
                return;
            }

            SetCount(joint, SprainsRequiredForRisk);
            SetWindow(joint, TrackingWindowHours);
            SetRisk(joint, RISK_START_VALUE);

            ApplyRiskAffliction(kind, bodyArea);

            Core.Instance?.MarkDirty();
            Core.Log($"DEV: SevereSprainRisk applied -> {joint} at {RISK_START_VALUE:0.#}%");
        }

        internal static void DevApplySevereSprain(SevereSprainKind kind, AfflictionBodyArea bodyArea)
        {
            if (!Settings.options.EnableSevereSprains)
            {
                Core.Log("SevereSprain dev command ignored: Severe Sprains are disabled.");
                return;
            }

            Core.State ??= new MMState();

            SevereSprainJoint joint = GetJoint(kind, bodyArea);
            if (joint == SevereSprainJoint.None)
            {
                Core.Log($"SevereSprain dev command ignored unknown body area -> kind={kind}, bodyArea={bodyArea}");
                return;
            }

            ClearRisk(joint);
            ResetTrackingWindow(joint);
            CureRiskAffliction(kind, bodyArea);

            ApplySevereSprain(kind, bodyArea, "dev command");

            Core.Instance?.MarkDirty();
            Core.Log($"DEV: SevereSprain applied -> {joint}");
        }

        internal static bool HasSevereSprain(SevereSprainKind kind, AfflictionBodyArea bodyArea)
        {
            AfflictionManager mgr = AfflictionManager.GetAfflictionManagerInstance();
            if (mgr?.m_Afflictions == null) return false;

            for (int i = 0; i < mgr.m_Afflictions.Count; i++)
            {
                if (kind == SevereSprainKind.Wrist &&
                    mgr.m_Afflictions[i] is SevereWristSprainAffliction wrist &&
                    wrist.m_Location == bodyArea)
                {
                    return true;
                }

                if (kind == SevereSprainKind.Ankle &&
                    mgr.m_Afflictions[i] is SevereAnkleSprainAffliction ankle &&
                    ankle.m_Location == bodyArea)
                {
                    return true;
                }
            }

            return false;
        }

        private static void RecordSprainForRisk(SevereSprainKind kind, AfflictionBodyArea bodyArea, SevereSprainJoint joint)
        {
            int count = GetCount(joint);
            float window = GetWindow(joint);

            if (window <= 0f)
            {
                count = 0;
                window = TrackingWindowHours;
            }

            count++;

            SetCount(joint, count);
            SetWindow(joint, window);

            if (count >= SprainsRequiredForRisk)
            {
                SetRisk(joint, RISK_START_VALUE);
                ApplyRiskAffliction(kind, bodyArea);

                Core.Instance?.MarkDirty();
                Core.Log($"SevereSprainRisk started -> {joint} at {RISK_START_VALUE:0.#}% ({count}/{SprainsRequiredForRisk})");
                return;
            }

            Core.Instance?.MarkDirty();
            Core.Log($"SevereSprain tracking -> {joint} count={count}/{SprainsRequiredForRisk}, window={window:0.#}h");
        }

        private static bool SyncJointFromPreset(SevereSprainJoint joint, SevereSprainKind kind, AfflictionBodyArea bodyArea)
        {
            bool changed = false;

            if (HasSevereSprain(kind, bodyArea))
            {
                if (GetRisk(joint) > 0f)
                {
                    ClearRisk(joint);
                    changed = true;
                }

                if (GetCount(joint) > 0 || GetWindow(joint) > 0f)
                {
                    ResetTrackingWindow(joint);
                    changed = true;
                }

                CureRiskAffliction(kind, bodyArea);
                return changed;
            }

            int count = GetCount(joint);
            float window = GetWindow(joint);
            float risk = GetRisk(joint);

            if (count <= 0 && window > 0f)
            {
                SetWindow(joint, 0f);
                window = 0f;
                changed = true;
            }

            if (window <= 0f && count > 0)
            {
                SetCount(joint, 0);
                count = 0;
                changed = true;
            }

            bool shouldHaveRisk = count > 0 && window > 0f && count >= SprainsRequiredForRisk;

            if (shouldHaveRisk)
            {
                if (risk <= 0f)
                {
                    SetRisk(joint, RISK_START_VALUE);
                    changed = true;

                    Core.Log($"SevereSprainRisk started -> {joint} at {RISK_START_VALUE:0.#}% (preset sync, {count}/{SprainsRequiredForRisk})");
                }

                ApplyRiskAffliction(kind, bodyArea);
                return changed;
            }

            if (risk > 0f)
            {
                ClearRisk(joint);
                CureRiskAffliction(kind, bodyArea);
                changed = true;

                Core.Log($"SevereSprainRisk removed -> {joint} (preset sync, {count}/{SprainsRequiredForRisk})");
            }
            else
            {
                CureRiskAffliction(kind, bodyArea);
            }

            return changed;
        }

        private static bool UpdateJointTimers(SevereSprainJoint joint, float hours)
        {
            bool changed = false;

            float risk = GetRisk(joint);
            if (risk > 0f)
            {
                float duration = Mathf.Max(1f, RiskDurationHours);
                float decayPerHour = RISK_START_VALUE / duration;

                risk = Mathf.Max(0f, risk - decayPerHour * hours);
                SetRisk(joint, risk);
                changed = true;

                if (risk <= 0f)
                {
                    CureRiskAffliction(joint);
                    ResetTrackingWindow(joint);
                    Core.Log($"SevereSprainRisk expired -> {joint}");
                }

                return changed;
            }

            float window = GetWindow(joint);
            if (window > 0f)
            {
                window = Mathf.Max(0f, window - hours);
                SetWindow(joint, window);
                changed = true;

                if (window <= 0f)
                {
                    SetCount(joint, 0);
                    Core.Log($"SevereSprain tracking expired -> {joint}");
                }
            }

            return changed;
        }

        private static bool HasRiskAffliction(SevereSprainKind kind, AfflictionBodyArea bodyArea)
        {
            AfflictionManager mgr = AfflictionManager.GetAfflictionManagerInstance();
            if (mgr?.m_Afflictions == null) return false;

            for (int i = 0; i < mgr.m_Afflictions.Count; i++)
            {
                if (kind == SevereSprainKind.Wrist &&
                    mgr.m_Afflictions[i] is SevereWristSprainRiskAffliction wristRisk &&
                    wristRisk.m_Location == bodyArea) return true;

                if (kind == SevereSprainKind.Ankle &&
                    mgr.m_Afflictions[i] is SevereAnkleSprainRiskAffliction ankleRisk &&
                    ankleRisk.m_Location == bodyArea) return true;
            }

            return false;
        }

        private static void ApplyRiskAffliction(SevereSprainKind kind, AfflictionBodyArea bodyArea)
        {
            if (HasRiskAffliction(kind, bodyArea)) return;

            if (kind == SevereSprainKind.Wrist)
            {
                new SevereWristSprainRiskAffliction(bodyArea).Start();
                return;
            }

            new SevereAnkleSprainRiskAffliction(bodyArea).Start();
        }

        private static void ApplySevereSprain(SevereSprainKind kind, AfflictionBodyArea bodyArea, string reason)
        {
            float duration = Mathf.Max(1f, SevereSprainDurationHours);

            if (kind == SevereSprainKind.Wrist)
            {
                Core.Log($"SevereWristSprain applied -> {bodyArea}, {duration:0.#}h ({reason})");
                new SevereWristSprainAffliction(bodyArea, duration).Start();
                return;
            }

            Core.Log($"SevereAnkleSprain applied -> {bodyArea}, {duration:0.#}h ({reason})");
            new SevereAnkleSprainAffliction(bodyArea, duration).Start();
        }

        private static void CureRiskAffliction(SevereSprainJoint joint)
        {
            switch (joint)
            {
                case SevereSprainJoint.LeftWrist:
                    CureRiskAffliction(SevereSprainKind.Wrist, AfflictionBodyArea.HandLeft);
                    break;

                case SevereSprainJoint.RightWrist:
                    CureRiskAffliction(SevereSprainKind.Wrist, AfflictionBodyArea.HandRight);
                    break;

                case SevereSprainJoint.LeftAnkle:
                    CureRiskAffliction(SevereSprainKind.Ankle, AfflictionBodyArea.FootLeft);
                    break;

                case SevereSprainJoint.RightAnkle:
                    CureRiskAffliction(SevereSprainKind.Ankle, AfflictionBodyArea.FootRight);
                    break;
            }
        }

        private static void CureRiskAffliction(SevereSprainKind kind, AfflictionBodyArea bodyArea)
        {
            AfflictionManager mgr = AfflictionManager.GetAfflictionManagerInstance();
            if (mgr?.m_Afflictions == null) return;

            for (int i = mgr.m_Afflictions.Count - 1; i >= 0; i--)
            {
                if (kind == SevereSprainKind.Wrist &&
                    mgr.m_Afflictions[i] is SevereWristSprainRiskAffliction wristRisk &&
                    wristRisk.m_Location == bodyArea)
                {
                    wristRisk.Cure();
                }

                if (kind == SevereSprainKind.Ankle &&
                    mgr.m_Afflictions[i] is SevereAnkleSprainRiskAffliction ankleRisk &&
                    ankleRisk.m_Location == bodyArea)
                {
                    ankleRisk.Cure();
                }
            }
        }

        private static void CureAllAfflictionsOfType<TAffliction>() where TAffliction : class
        {
            AfflictionManager mgr = AfflictionManager.GetAfflictionManagerInstance();
            if (mgr?.m_Afflictions == null) return;

            for (int i = mgr.m_Afflictions.Count - 1; i >= 0; i--)
            {
                if (mgr.m_Afflictions[i] is TAffliction afflictionObject && afflictionObject is CustomAffliction affliction)
                {
                    affliction.Cure();
                }
            }
        }

        private static SevereSprainJoint GetJoint(SevereSprainKind kind, AfflictionBodyArea bodyArea)
        {
            if (kind == SevereSprainKind.Wrist)
            {
                if (bodyArea == AfflictionBodyArea.HandLeft) return SevereSprainJoint.LeftWrist;

                if (bodyArea == AfflictionBodyArea.HandRight) return SevereSprainJoint.RightWrist;

                return SevereSprainJoint.None;
            }

            if (bodyArea == AfflictionBodyArea.FootLeft) return SevereSprainJoint.LeftAnkle;

            if (bodyArea == AfflictionBodyArea.FootRight) return SevereSprainJoint.RightAnkle;

            return SevereSprainJoint.None;
        }

        private static int GetCount(SevereSprainJoint joint)
        {
            Core.State ??= new MMState();

            return joint switch
            {
                SevereSprainJoint.LeftWrist => Core.State.LeftWristSprainCount,
                SevereSprainJoint.RightWrist => Core.State.RightWristSprainCount,
                SevereSprainJoint.LeftAnkle => Core.State.LeftAnkleSprainCount,
                SevereSprainJoint.RightAnkle => Core.State.RightAnkleSprainCount,
                _ => 0
            };
        }

        private static void SetCount(SevereSprainJoint joint, int value)
        {
            Core.State ??= new MMState();
            value = Mathf.Max(0, value);

            switch (joint)
            {
                case SevereSprainJoint.LeftWrist: Core.State.LeftWristSprainCount = value; break;
                case SevereSprainJoint.RightWrist: Core.State.RightWristSprainCount = value; break;
                case SevereSprainJoint.LeftAnkle: Core.State.LeftAnkleSprainCount = value; break;
                case SevereSprainJoint.RightAnkle: Core.State.RightAnkleSprainCount = value; break;
            }
        }

        private static float GetWindow(SevereSprainJoint joint)
        {
            Core.State ??= new MMState();

            return joint switch
            {
                SevereSprainJoint.LeftWrist => Core.State.LeftWristSprainWindowHours,
                SevereSprainJoint.RightWrist => Core.State.RightWristSprainWindowHours,
                SevereSprainJoint.LeftAnkle => Core.State.LeftAnkleSprainWindowHours,
                SevereSprainJoint.RightAnkle => Core.State.RightAnkleSprainWindowHours,
                _ => 0f
            };
        }

        private static void SetWindow(SevereSprainJoint joint, float value)
        {
            Core.State ??= new MMState();
            value = Mathf.Max(0f, value);

            switch (joint)
            {
                case SevereSprainJoint.LeftWrist: Core.State.LeftWristSprainWindowHours = value; break;
                case SevereSprainJoint.RightWrist: Core.State.RightWristSprainWindowHours = value; break;
                case SevereSprainJoint.LeftAnkle: Core.State.LeftAnkleSprainWindowHours = value; break;
                case SevereSprainJoint.RightAnkle: Core.State.RightAnkleSprainWindowHours = value; break;
            }
        }

        private static float GetRisk(SevereSprainJoint joint)
        {
            Core.State ??= new MMState();

            return joint switch
            {
                SevereSprainJoint.LeftWrist => Core.State.LeftWristSevereSprainRisk,
                SevereSprainJoint.RightWrist => Core.State.RightWristSevereSprainRisk,
                SevereSprainJoint.LeftAnkle => Core.State.LeftAnkleSevereSprainRisk,
                SevereSprainJoint.RightAnkle => Core.State.RightAnkleSevereSprainRisk,
                _ => 0f
            };
        }

        private static void SetRisk(SevereSprainJoint joint, float value)
        {
            Core.State ??= new MMState();
            value = Mathf.Clamp(value, 0f, RISK_START_VALUE);

            switch (joint)
            {
                case SevereSprainJoint.LeftWrist: Core.State.LeftWristSevereSprainRisk = value; break;
                case SevereSprainJoint.RightWrist: Core.State.RightWristSevereSprainRisk = value; break;
                case SevereSprainJoint.LeftAnkle: Core.State.LeftAnkleSevereSprainRisk = value; break;
                case SevereSprainJoint.RightAnkle: Core.State.RightAnkleSevereSprainRisk = value; break;
            }
        }

        private static void ClearRisk(SevereSprainJoint joint)
        {
            SetRisk(joint, 0f);
        }

        private static void ResetTrackingWindow(SevereSprainJoint joint)
        {
            SetCount(joint, 0);
            SetWindow(joint, 0f);
        }

        private static bool HasAnyStoredValue()
        {
            Core.State ??= new MMState();

            return Core.State.LeftWristSprainCount > 0 || Core.State.LeftWristSprainWindowHours > 0f || Core.State.LeftWristSevereSprainRisk > 0f ||
                   Core.State.RightWristSprainCount > 0 || Core.State.RightWristSprainWindowHours > 0f || Core.State.RightWristSevereSprainRisk > 0f ||
                   Core.State.LeftAnkleSprainCount > 0 || Core.State.LeftAnkleSprainWindowHours > 0f || Core.State.LeftAnkleSevereSprainRisk > 0f ||
                   Core.State.RightAnkleSprainCount > 0 || Core.State.RightAnkleSprainWindowHours > 0f || Core.State.RightAnkleSevereSprainRisk > 0f;
        }

        private static void ClearAllStoredValues()
        {
            Core.State ??= new MMState();

            Core.State.LeftWristSprainCount = 0;
            Core.State.LeftWristSprainWindowHours = 0f;
            Core.State.LeftWristSevereSprainRisk = 0f;
            Core.State.RightWristSprainCount = 0;
            Core.State.RightWristSprainWindowHours = 0f;
            Core.State.RightWristSevereSprainRisk = 0f;
            Core.State.LeftAnkleSprainCount = 0;
            Core.State.LeftAnkleSprainWindowHours = 0f;
            Core.State.LeftAnkleSevereSprainRisk = 0f;
            Core.State.RightAnkleSprainCount = 0;
            Core.State.RightAnkleSprainWindowHours = 0f;
            Core.State.RightAnkleSevereSprainRisk = 0f;
        }

        private static void ClampJoint(SevereSprainJoint joint)
        {
            SetCount(joint, GetCount(joint));
            SetWindow(joint, GetWindow(joint));
            SetRisk(joint, GetRisk(joint));
        }
    }
}