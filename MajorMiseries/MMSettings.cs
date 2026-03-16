namespace MajorMiseries
{
    internal class MMSettings : JsonModSettings
    {
        [Section("Requiem Stages")]

        [Name("Enable Requiem Stages")]
        [Description("Renable or disable the Requiem stage system / afflictions.")]
        public bool EnableRequiemStages = false;

        [Name("Omen threshold")]
        [Description("Days survived before Omen starts.")]
        [Slider(1, 365, 365, NumberFormat = "{0:0}d")]
        public int OmenThreshold = 1;

        [Name("Dirge threshold")]
        [Description("Days survived before Dirge starts.")]
        [Slider(1, 365, 365, NumberFormat = "{0:0}d")]
        public int DirgeThreshold = 2;

        [Name("Knell threshold")]
        [Description("Days survived before Knell starts.")]
        [Slider(1, 365, 365, NumberFormat = "{0:0}d")]
        public int KnellThreshold = 3;

        [Name("Requiem threshold")]
        [Description("Days survived before Requiem starts.")]
        [Slider(1, 365, 365, NumberFormat = "{0:0}d")]
        public int RequiemThreshold = 4;

        [Section("Broken Limbs")]

        [Name("Broken Limb Duration")]
        [Description("Choose whether broken arm and broken leg use realistic or shortened recovery durations.")]
        [Choice("Realistic", "Unrealistic")]
        public int BrokenLimbDurationMode = 0;

        [Section("Predator Struggle Afflictions")]

        [Name("Bear broken limb chance")]
        [Description("Default : 20% - Chance for a bear struggle to cause a broken arm or leg.")]
        [Slider(1, 100, 100, NumberFormat = "{0:0}%")]
        public float BearBrokenLimbChance = 20f;

        [Name("Moose broken limb chance")]
        [Description("Default : 30% - Chance for a moose struggle to cause a broken arm or leg.")]
        [Slider(1, 100, 100, NumberFormat = "{0:0}%")]
        public float MooseBrokenLimbChance = 30f;

        [Name("Allow broken arm and broken leg together")]
        [Description("If enabled, the same struggle can cause both a broken arm and a broken leg.")]
        public bool AllowDoubleBrokenLimb = false;


        [Section("Advanced")]

        [Name("ML Logging")]
        [Description("Add logs for debugging in the ML console.")]
        public bool IsLogging = false;
    }

    internal static class Settings
    {
        public static MMSettings options;

        public static void OnLoad()
        {
            options = new MMSettings();
            options.AddToModSettings("Major Miseries");
        }
    }
}