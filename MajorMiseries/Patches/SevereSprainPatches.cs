namespace MajorMiseries.Patches
{
    [HarmonyPatch(typeof(SprainedAnkle), nameof(SprainedAnkle.SprainedAnkleStart))]
    internal static class SprainedAnkle_SprainedAnkleStart
    {
        private static void Prefix(SprainedAnkle __instance, out int __state)
        {
            __state = __instance != null ? __instance.GetAfflictionsCount() : 0;
        }

        private static void Postfix(SprainedAnkle __instance, int __state)
        {
            if (__instance == null)
                return;

            int newCount = __instance.GetAfflictionsCount();
            if (newCount <= __state)
                return;

            for (int i = newCount - 1; i >= __state; i--)
            {
                AfflictionBodyArea location = __instance.GetLocation(i);
                bool replaceVanilla = SevereSprainLogic.OnVanillaSprainStarted(SevereSprainKind.Ankle, location);

                if (replaceVanilla)
                {
                    __instance.SprainedAnkleEnd(i, default);
                    Core.Log($"Vanilla ankle sprain replaced by severe sprain -> {location}");
                }
            }
        }
    }

    [HarmonyPatch(typeof(SprainedWrist), nameof(SprainedWrist.SprainedWristStart))]
    internal static class SprainedWrist_SprainedWristStart
    {
        private static void Prefix(SprainedWrist __instance, out int __state)
        {
            __state = __instance != null ? __instance.GetAfflictionsCount() : 0;
        }

        private static void Postfix(SprainedWrist __instance, int __state)
        {
            if (__instance == null)
                return;

            int newCount = __instance.GetAfflictionsCount();
            if (newCount <= __state)
                return;

            for (int i = newCount - 1; i >= __state; i--)
            {
                AfflictionBodyArea location = __instance.GetLocation(i);
                bool replaceVanilla = SevereSprainLogic.OnVanillaSprainStarted(SevereSprainKind.Wrist, location);

                if (replaceVanilla)
                {
                    __instance.SprainedWristEnd(i, default);
                    Core.Log($"Vanilla wrist sprain replaced by severe sprain -> {location}");
                }
            }
        }
    }
}