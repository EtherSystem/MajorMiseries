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

        [Section("Coal Illnesses")]

        [Name("Black Lung Duration")]
        [Description("Choose whether Black Lung uses realistic or shortened recovery durations.")]
        [Choice("Realistic", "Unrealistic")]
        public int BlackLungDurationMode = 0;

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

        [Name("Hunger lock arc rotation")]
        [Description("Adjust the hunger red lock arc rotation.")]
        [Slider(-10f, 10f, 201, NumberFormat = "{0:0.0}°")]
        public float HungerLockArcRotation = 0f;

        [Name("Thirst lock arc rotation")]
        [Description("Adjust the thirst red lock arc rotation.")]
        [Slider(-10f, 10f, 201, NumberFormat = "{0:0.0}°")]
        public float ThirstLockArcRotation = 0f;

        [Name("Fatigue lock arc rotation")]
        [Description("Adjust the fatigue red lock arc rotation.")]
        [Slider(-10f, 10f, 201, NumberFormat = "{0:0.0}°")]
        public float FatigueLockArcRotation = 0f;

        [Name("Cold lock arc rotation")]
        [Description("Adjust the cold red lock arc rotation.")]
        [Slider(-10f, 10f, 201, NumberFormat = "{0:0.0}°")]
        public float ColdLockArcRotation = 0f;

        [Name("Sour Stomach extra hunger rotation")]
        [Description("Additional hunger rotation offset when Sour Stomach is active.")]
        [Slider(-10f, 10f, 201, NumberFormat = "{0:0.0}°")]
        public float SourStomachExtraHungerRotation = -3f;

        [Name("ML Logging")]
        [Description("Add logs for debugging in the ML console.")]
        public bool IsLogging = false;

        [Name("Do you really want to mess with affliction ?")]
        [Description("This will possibly ruin everything...")]
        public bool RevealShinyAfflictionIconChance1 = false;

        [Name("Are you sure you want to interfer with your destiny ?")]
        [Description("Think twice")]
        public bool RevealShinyAfflictionIconChance2 = false;

        [Name("Fine. Let's tempt fate.")]
        [Description("This reveals the alternative affliction icon chance slider. Don't touch it.")]
        public bool RevealShinyAfflictionIconChance3 = false;

        [Name("Shiny affliction icon chance")]
        [Description("Chance for a newly contracted affliction to use its alternative icon instead of the normal one.")]
        [Slider(0f, 100f, 1001, NumberFormat = "{0:0.0}%")]
        public float AltAfflictionIconChance = 0.1f;

        protected override void OnChange(FieldInfo field, object? oldValue, object? newValue)
        {
            if (field.Name == nameof(RevealShinyAfflictionIconChance1) ||
                field.Name == nameof(RevealShinyAfflictionIconChance2) ||
                field.Name == nameof(RevealShinyAfflictionIconChance3))
            {
                Settings.UpdateShinyAfflictionIconChanceVisibility();
            }

            base.OnChange(field, oldValue, newValue);
        }
    }
    internal static class Settings
    {
        public static MMSettings options;

        public static void OnLoad()
        {
            options = new MMSettings();
            options.AddToModSettings("Major Miseries");

            UpdateShinyAfflictionIconChanceVisibility();
        }


        internal static void UpdateShinyAfflictionIconChanceVisibility()
        {
            bool showSecond = options.RevealShinyAfflictionIconChance1;
            bool showThird = showSecond && options.RevealShinyAfflictionIconChance2;
            bool showChance = showThird && options.RevealShinyAfflictionIconChance3;

            if (!showSecond)
            {
                options.RevealShinyAfflictionIconChance2 = false;
                options.RevealShinyAfflictionIconChance3 = false;
            }

            if (!showThird)
            {
                options.RevealShinyAfflictionIconChance3 = false;
            }

            options.SetFieldVisible(nameof(options.RevealShinyAfflictionIconChance2), showSecond);
            options.SetFieldVisible(nameof(options.RevealShinyAfflictionIconChance3), showThird);
            options.SetFieldVisible(nameof(options.AltAfflictionIconChance), showChance);
        }
    }
}