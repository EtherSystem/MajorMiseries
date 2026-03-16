using Il2CppTLD.Gameplay;

namespace MajorMiseries.Patches
{
    internal static class DisplayStagePopup
    {
        internal static void ShowStagePopup(int stage, string stageNameLocId)
        {
            MiseryHUD miseryHud = UnityEngine.Object.FindObjectOfType<MiseryHUD>();

            if (miseryHud == null)
            {
                MiseryHUD[] allHud = UnityEngine.Resources.FindObjectsOfTypeAll<MiseryHUD>();

                if (allHud != null && allHud.Length > 0)
                {
                    miseryHud = allHud[0];
                }
            }

            if (miseryHud == null)
            {
                Core.Log("MiseryHUD not found.");
                return;
            }

            Core.Log($"Sending popup: stage={stage}, locId={stageNameLocId}");

            miseryHud.EnqueueStage(stage, stageNameLocId);
        }
    }
}