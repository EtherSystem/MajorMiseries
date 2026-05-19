namespace MajorMiseries
{
    internal static class AfflictionSaveHelper
    {
        private static bool _saveQueued = false;

        internal static void QueueSurvivalSave()
        {
            if (!Core.IsGameplayEnabled) return;
            if (_saveQueued) return;
            _saveQueued = true;

            MelonCoroutines.Start(DelayedSurvivalSave());
        }

        private static System.Collections.IEnumerator DelayedSurvivalSave()
        {
            yield return null;
            yield return null;

            _saveQueued = false;

            if (GameManager.m_Instance != null && Core.IsGameplayEnabled)
            {
                GameManager.TriggerSurvivalSaveAndDisplayHUDMessage();
            }
        }
    }
}