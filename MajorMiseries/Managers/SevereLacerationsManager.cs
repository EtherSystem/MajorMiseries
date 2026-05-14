using System.Collections;
using AfflictionComponent.Components;
using MajorMiseries.Persistence;
using static MajorMiseries.Afflictions.ScarredFlesh;

namespace MajorMiseries
{
    internal static partial class AfflictionLogic
    {
        // =======================================================================================
        //           BloodLoss converted to SevereLacerations Logic + ScarredFlesh
        // =======================================================================================

        private static bool _applyingSevere;
        private static bool _pendingSevere;
        private static bool _severeWasActive;
        private static bool _loggedPendingSevereSuppression;

        internal static bool IsApplyingSevereLacerationConversion => _applyingSevere;

        internal static void TryConvertPredatorBloodLossToSevereLaceration(BloodLoss bloodLoss, string cause)
        {
            RefreshEffectsIfNeeded();

            if (!RequiemStagesEffects.ShouldConvertPredatorBloodLossToSevere(GetCurrentStage())) return;

            PlayerManager player = GameManager.GetPlayerManagerComponent();
            if (player == null || player.PlayerIsDead()) return;

            string forwardedCause = string.IsNullOrWhiteSpace(cause) ? "Predator Attack" : cause;

            if (!IsPredatorBloodLossCause(forwardedCause))
            {
                Core.Log($"blood loss conversion ignored -> non-predator cause:'{forwardedCause}'");
                return;
            }

            SevereLacerations severe = GameManager.GetSevereLacerations();
            if (severe == null)
            {
                Core.Log($"blood loss conversion failed -> SevereLacerations component missing | cause:'{forwardedCause}'");
                return;
            }

            bool bloodLossStopped = TryStopConvertedBloodLoss(bloodLoss, forwardedCause);

            if (severe.HasAffliction())
            {
                Core.Log($"blood loss conversion consumed -> SevereLacerations already active | cause:'{forwardedCause}' | bloodLossStopped:{bloodLossStopped}");
                return;
            }

            if (_pendingSevere)
            {
                if (!_loggedPendingSevereSuppression)
                {
                    Core.Log($"blood loss conversion consumed -> SevereLacerations already pending | cause:'{forwardedCause}' | bloodLossStopped:{bloodLossStopped}");
                    _loggedPendingSevereSuppression = true;
                }

                return;
            }

            _pendingSevere = true;
            _loggedPendingSevereSuppression = false;

            Core.Log($"blood loss conversion queued -> SevereLacerations | cause:'{forwardedCause}' | bloodLossStopped:{bloodLossStopped} | stagesEnabled:{Settings.options.EnableRequiemStages} | stage:{GetCurrentStage()} | mode:{Settings.options.PredatorBloodLossToSevereLacerationsMode}");

            MelonCoroutines.Start(ApplySevereNextFrame(forwardedCause));
        }

        private static bool IsPredatorBloodLossCause(string cause)
        {
            if (string.IsNullOrWhiteSpace(cause)) return false;

            string lowerCause = cause.ToLowerInvariant();

            return lowerCause.Contains("wolf")
                || lowerCause.Contains("timberwolf")
                || lowerCause.Contains("bear")
                || lowerCause.Contains("cougar")
                || lowerCause.Contains("predator");
        }

        private static bool TryStopConvertedBloodLoss(BloodLoss bloodLoss, string cause)
        {
            if (bloodLoss == null) return false;

            try
            {
                int countBefore = bloodLoss.GetAfflictionsCount();
                if (countBefore <= 0)
                {
                    Core.Log($"blood loss conversion warning -> no active vanilla BloodLoss to stop | cause:'{cause}'");
                    return false;
                }

                int convertedIndex = countBefore - 1;

                bloodLoss.BloodLossEnd(convertedIndex, (AfflictionOptions)0);

                int countAfter = bloodLoss.GetAfflictionsCount();
                bool stopped = countAfter < countBefore;

                Core.Log($"blood loss conversion -> stopped vanilla BloodLoss via BloodLossEnd({convertedIndex}) | cause:'{cause}' | count:{countBefore}->{countAfter}");

                return stopped;
            }
            catch (Exception e)
            {
                Core.Warn($"blood loss conversion warning -> failed to stop vanilla BloodLoss via BloodLossEnd(): {e.Message} | cause:'{cause}'");
                return false;
            }
        }

        internal static void TryHandleSevereLacerationHealing(SevereLacerations severe)
        {
            PlayerManager player = GameManager.GetPlayerManagerComponent();
            if (player == null || player.PlayerIsDead())
            {
                _severeWasActive = false;
                return;
            }

            if (severe == null) return;

            bool isActive = severe.HasAffliction();

            if (isActive)
            {
                _severeWasActive = true;
                return;
            }

            if (!_severeWasActive) return;

            _severeWasActive = false;

            AfflictionManager mgr = AfflictionManager.GetAfflictionManagerInstance();
            if (mgr == null) return;

            AddScarredFleshStack("SevereLacerations healed");
        }

        private static IEnumerator ApplySevereNextFrame(string cause)
        {
            yield return null;

            try
            {
                PlayerManager player = GameManager.GetPlayerManagerComponent();
                if (player == null || player.PlayerIsDead()) yield break;

                SevereLacerations severe = GameManager.GetSevereLacerations();
                if (severe == null || severe.HasAffliction()) yield break;

                Condition condition = GameManager.GetConditionComponent();

                float hpBefore = condition != null ? condition.m_CurrentHP : -1f;
                float normalizedBefore = condition != null ? condition.GetNormalizedCondition() : -1f;

                _applyingSevere = true;

                severe.ApplySevereLacerations(cause);

                float hpAfter = condition != null ? condition.m_CurrentHP : -1f;
                float normalizedAfter = condition != null ? condition.GetNormalizedCondition() : -1f;

                Core.Log($"blood loss converted -> SevereLacerations applied | cause:'{cause}' | active:{severe.HasAffliction()} | condition:{hpBefore:0.##}->{hpAfter:0.##} HP | normalized:{normalizedBefore * 100f:0.#}%->{normalizedAfter * 100f:0.#}%");
            }
            catch (Exception e)
            {
                Core.Log($"blood loss conversion failed while applying SevereLacerations: {e}");
            }
            finally
            {
                _applyingSevere = false;
                _pendingSevere = false;
                _loggedPendingSevereSuppression = false;
            }
        }

        internal static int GetScarredFleshCount()
        {
            Core.State ??= new MMState();
            return Settings.options.EnableScarredFlesh ? Mathf.Max(0, Core.State.ScarredFleshHistoryCount) : 0;
        }

        internal static void AddScarredFleshStack(string source)
        {
            Core.State ??= new MMState();

            Core.State.ScarredFleshHistoryCount = Mathf.Max(0, Core.State.ScarredFleshHistoryCount) + 1;
            Core.Instance?.MarkDirty();

            if (!Settings.options.EnableScarredFlesh)
            {
                Core.Log($"{source} -> ScarredFlesh history increased to {Core.State.ScarredFleshHistoryCount}, but the system is disabled.");
                ForceRefreshEffects();
                return;
            }

            Core.Log($"{source} -> ScarredFlesh stack is now x{Core.State.ScarredFleshHistoryCount}.");

            EnsureScarredFleshDisplay();
            ForceRefreshEffects();
        }

        internal static void SetScarredFleshStack(int value)
        {
            Core.State ??= new MMState();

            Core.State.ScarredFleshHistoryCount = Mathf.Max(0, value);
            Core.Instance?.MarkDirty();

            EnsureScarredFleshDisplay();
            ForceRefreshEffects();
        }

        private static void EnsureScarredFleshDisplay()
        {
            Core.State ??= new MMState();

            if (!Settings.options.EnableScarredFlesh || Core.State.ScarredFleshHistoryCount <= 0)
            {
                CureAllAfflictionsOfType<ScarredFleshAffliction>();
                return;
            }

            ScarredFleshAffliction? activeScarredFlesh = null;

            AfflictionManager mgr = AfflictionManager.GetAfflictionManagerInstance();
            var list = mgr?.m_Afflictions;

            if (list != null)
            {
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    if (list[i] is ScarredFleshAffliction scarredFlesh)
                    {
                        if (activeScarredFlesh == null)
                        {
                            activeScarredFlesh = scarredFlesh;
                        }
                        else
                        {
                            scarredFlesh.Cure();
                        }
                    }
                }
            }

            if (activeScarredFlesh != null)
            {
                activeScarredFlesh.RefreshStackDisplay();
                return;
            }

            new ScarredFleshAffliction(AfflictionBodyArea.Chest).Start();
            AfflictionSaveHelper.QueueSurvivalSave();
        }
    }
}