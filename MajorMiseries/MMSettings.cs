namespace MajorMiseries
{
    internal class MMSettings : JsonModSettings
    {
        [Section("Requiem Stages")]

        [Name("Enable Requiem Stages")]
        [Description("Enable or disable the Requiem stage system.")]
        public bool EnableRequiemStages = false;

        [Name("Predator Hostility")]
        [Description("Choose when predator hostility should be active.")]
        [Choice("Only with Requiem Stages", "Always", "Disabled")]
        public int PredatorHostilityMode = 0;

        [Name("Customize Stage Thresholds")]
        [Description("Override the default day thresholds for Omen, Dirge, Knell, and Requiem.")]
        public bool CustomizeStageThresholds = false;

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


        [Section("Predator Related Afflictions")]

        [Name("Bear broken limb chance")]
        [Description("Default: 20% - Chance for a bear struggle to cause a broken arm or leg.")]
        [Slider(1, 100, 100, NumberFormat = "{0:0}%")]
        public float BearBrokenLimbChance = 20f;

        [Name("Moose broken limb chance")]
        [Description("Default: 30% - Chance for a moose struggle to cause a broken arm or leg.")]
        [Slider(1, 100, 100, NumberFormat = "{0:0}%")]
        public float MooseBrokenLimbChance = 30f;

        [Name("Allow broken arm and broken leg together")]
        [Description("If enabled, the same struggle can cause both a broken arm and a broken leg.")]
        public bool AllowDoubleBrokenLimb = false;

        [Name("Broken Limb Duration")]
        [Description("Choose whether broken arm and broken leg use realistic or shortened recovery durations.")]
        [Choice("Realistic", "Unrealistic")]
        public int BrokenLimbDurationMode = 0;

        [Name("Predator Blood Loss to Severe Lacerations")]
        [Description("Choose when predator Blood Loss should be converted into Severe Lacerations.")]
        [Choice("Only with Requiem", "Always", "Disabled")]
        public int PredatorBloodLossToSevereLacerationsMode = 0;


        [Section("Coal Illnesses")]

        [Name("Enable Black Lung")]
        [Description("Enable or disable the Black Lung affliction system.")]
        public bool EnableBlackLung = true;

        [Name("Black Lung Duration")]
        [Description("Choose whether Black Lung uses realistic or shortened recovery durations.")]
        [Choice("Realistic", "Unrealistic")]
        public int BlackLungDurationMode = 0;

        [Name("Enable Carbon Monoxide")]
        [Description("Enable or disable the carbon monoxide affliction system.")]
        public bool EnableCarbonMonoxide = true;


        [Section("Affliction Systems")]

        [Name("Enable Scarred Flesh")]
        [Description("Enable or disable the Severe Lacerations to Scarred Flesh affliction chain.")]
        public bool EnableScarredFlesh = true;

        [Name("Enable Sepsis")]
        [Description("Enable or disable the Infection to Sepsis affliction chain.")]
        public bool EnableSepsis = true;


        [Section("Corpse Sickness")]

        [Name("Enable Corpse Sickness")]
        [Description("Enable or disable the corpse sickness affliction system.")]
        public bool EnableCorpseSickness = true;

        [Name("Human corpses can infect you")]
        [Description("If enabled, staying too close to human corpses for too long can make you sick.")]
        public bool EnableHumanCorpseExposure = true;

        [Name("Animal carcasses can infect you")]
        [Description("If enabled, staying too close to animal carcasses for too long can make you sick.")]
        public bool EnableAnimalCarcassExposure = true;

        [Name("Human corpse radius")]
        [Description("Maximum distance at which a human corpse counts for corpse sickness exposure.")]
        [Slider(1f, 30f, 30, NumberFormat = "{0:0}m")]
        public int HumanCorpseExposureRadiusMeters = 15;

        [Name("Animal carcass radius")]
        [Description("Maximum distance at which an animal carcass counts for corpse sickness exposure.")]
        [Slider(1f, 30f, 30, NumberFormat = "{0:0}m")]
        public int AnimalCarcassExposureRadiusMeters = 10;

        [Name("Human corpse time to risk")]
        [Description("How many in-game hours it takes, at full human corpse exposure, to reach 100 hidden corpse exposure.")]
        [Slider(1f, 24f, 24, NumberFormat = "{0:0}h")]
        public int HumanCorpseHoursToRisk = 1;

        [Name("Animal carcass time to risk")]
        [Description("How many in-game hours it takes, at full animal carcass exposure, to reach 100 hidden corpse exposure.")]
        [Slider(1f, 24f, 24, NumberFormat = "{0:0}h")]
        public int AnimalCarcassHoursToRisk = 5;


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

        [Name("Do you want to mess with affliction ?")]
        [Description("This will ruin everything...")]
        public bool RevealShinyAfflictionIconChance1 = false;

        [Name("Are you sure you want to interfer with your destiny ?")]
        [Description("Think twice")]
        public bool RevealShinyAfflictionIconChance2 = false;

        [Name("Fine. Let's tempt fate.")]
        [Description("This will reveals a true heresy. Don't touch it.")]
        public bool RevealShinyAfflictionIconChance3 = false;

        [Name("Your Destiny")]
        [Description("This slider allows you to choose how much you want to alter your destiny, please don't touch it.")]
        [Slider(0f, 100f, 1001, NumberFormat = "{0:0.0}")]
        public float AltAfflictionIconChance = 0.1f;

        protected override void OnChange(FieldInfo field, object? oldValue, object? newValue)
        {
            if (field.Name == nameof(CustomizeStageThresholds))
            {
                Settings.UpdateStageThresholdVisibility();
            }

            if (field.Name == nameof(EnableBlackLung))
            {
                Settings.UpdateBlackLungVisibility();
            }

            if (field.Name == nameof(EnableCorpseSickness))
            {
                Settings.UpdateCorpseSicknessVisibility();
            }

            if (field.Name == nameof(RevealShinyAfflictionIconChance1) ||
                field.Name == nameof(RevealShinyAfflictionIconChance2) ||
                field.Name == nameof(RevealShinyAfflictionIconChance3))
            {
                Settings.UpdateShinyAfflictionIconChanceVisibility();
            }

            base.OnChange(field, oldValue, newValue);

            bool requiresRuntimeSync =
                field.Name == nameof(EnableScarredFlesh) ||
                field.Name == nameof(EnableSepsis) ||
                field.Name == nameof(EnableCarbonMonoxide) ||
                field.Name == nameof(EnableBlackLung) ||
                field.Name == nameof(EnableCorpseSickness);

            if (requiresRuntimeSync)
            {
                AfflictionLogic.SyncSettingsControlledAfflictions();
            }
        }
    }

    internal static class Settings
    {
        public static MMSettings options;

        public static void OnLoad()
        {
            options = new MMSettings();
            options.AddToModSettings("Major Miseries");

            UpdateStageThresholdVisibility();
            UpdateBlackLungVisibility();
            UpdateCorpseSicknessVisibility();
            UpdateShinyAfflictionIconChanceVisibility();
        }

        internal static void UpdateStageThresholdVisibility()
        {
            bool showThresholds = options.CustomizeStageThresholds;

            options.SetFieldVisible(nameof(options.OmenThreshold), showThresholds);
            options.SetFieldVisible(nameof(options.DirgeThreshold), showThresholds);
            options.SetFieldVisible(nameof(options.KnellThreshold), showThresholds);
            options.SetFieldVisible(nameof(options.RequiemThreshold), showThresholds);
        }

        internal static void UpdateBlackLungVisibility()
        {
            bool showBlackLungDuration = options.EnableBlackLung;

            options.SetFieldVisible(nameof(options.BlackLungDurationMode), showBlackLungDuration);
        }

        internal static void UpdateCorpseSicknessVisibility()
        {
            bool showCorpseSicknessSettings = options.EnableCorpseSickness;

            options.SetFieldVisible(nameof(options.EnableHumanCorpseExposure), showCorpseSicknessSettings);
            options.SetFieldVisible(nameof(options.EnableAnimalCarcassExposure), showCorpseSicknessSettings);
            options.SetFieldVisible(nameof(options.HumanCorpseExposureRadiusMeters), showCorpseSicknessSettings);
            options.SetFieldVisible(nameof(options.AnimalCarcassExposureRadiusMeters), showCorpseSicknessSettings);
            options.SetFieldVisible(nameof(options.HumanCorpseHoursToRisk), showCorpseSicknessSettings);
            options.SetFieldVisible(nameof(options.AnimalCarcassHoursToRisk), showCorpseSicknessSettings);
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