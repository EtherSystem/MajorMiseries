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

        [Name("Vitamin C Drain")]
        [Description("Choose when Vitamin C drain should be accelerated.")]
        [Choice("Only with Requiem Stages", "Always", "Disabled")]
        public int VitaminCDrainMode = 0;

        [Name("Vitamin C Drain Preset")]
        [Description("Choose how strongly Vitamin C drain is multiplied when active. Forgiving x1.5, Standard x2, Harsh x3, Brutal x4, Who Wants to Play Like This? x5.")]
        [Choice("Forgiving", "Standard", "Harsh", "Brutal", "Who Wants to Play Like This?")]
        public int VitaminCDrainPreset = 1;

        [Name("Show Stage Thresholds Customization")]
        [Description("Show or hide day threshold customization sliders for Omen, Dirge, Knell and Requiem.")]
        public bool CustomizeStageThresholds = false;

        [Name("Omen threshold")]
        [Description("Days survived before Omen starts.")]
        [Slider(1, 365, 365, NumberFormat = "{0:0}d")]
        public int OmenThreshold = 10;

        [Name("Dirge threshold")]
        [Description("Days survived before Dirge starts.")]
        [Slider(2, 365, 364, NumberFormat = "{0:0}d")]
        public int DirgeThreshold = 20;

        [Name("Knell threshold")]
        [Description("Days survived before Knell starts.")]
        [Slider(3, 365, 363, NumberFormat = "{0:0}d")]
        public int KnellThreshold = 35;

        [Name("Requiem threshold")]
        [Description("Days survived before Requiem starts.")]
        [Slider(4, 365, 362, NumberFormat = "{0:0}d")]
        public int RequiemThreshold = 50;


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


        [Section("Severe Sprains")]

        [Name("Enable Severe Sprains")]
        [Description("Enable or disable the severe sprain recurrence system.")]
        public bool EnableSevereSprains = true;

        [Name("Severe Sprain Preset")]
        [Description("Choose how quickly repeated sprains can become severe.")]
        [Choice("Forgiving", "Standard", "Harsh", "Brutal", "Who Wants to Play Like This?")]
        public int SevereSprainPreset = 1;


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


        [Section("Regional Afflictions")]

        [Name("Enable Regional Afflictions")]
        [Description("Enable or disable Home Sickness, Home Comfort, and Regional Distress.")]
        public bool EnableRegionalAfflictions = true;

        [Name("Home Region")]
        [Description("A region where you feel at home.")]
        [Choice("None",
            "Mystery Lake",
            "Coastal Highway",
            "Pleasant Valley",
            "Mountain Town",
            "Forlorn Muskeg",
            "Broken Railroad",
            "Desolation Point",
            "Timberwolf Mountain",
            "Ash Canyon",
            "Hushed River Valley",
            "Bleak Inlet",
            "Blackrock",
            "Forsaken Airfield",
            "Zone of Contamination",
            "Sundered Pass",
            "Transfer Pass",
            "TLDev - Forsaken Shore",
            "TLDev - Mountain Pass",
            "TLDev - Precarious Causeway",
            "TLDev - Rocky Thoroughfare",
            "TLDev - Shattered Marsh"
        )]
        public int HomeRegion = 0;

        [Name("Home Sickness Delay")]
        [Description("Hours spent away from the home region before Home Sickness appears.")]
        [Slider(1f, 336f, 336, NumberFormat = "{0:0}h")]
        public int HomeSicknessDelayHours = 72;

        [Name("Regional Distress Region")]
        [Description("Region where you feel uncomfortable. This cannot affect your home region.")]
        [Choice(
            "None",
            "Mystery Lake",
            "Coastal Highway",
            "Pleasant Valley",
            "Mountain Town",
            "Forlorn Muskeg",
            "Broken Railroad",
            "Desolation Point",
            "Timberwolf Mountain",
            "Ash Canyon",
            "Hushed River Valley",
            "Bleak Inlet",
            "Blackrock",
            "Forsaken Airfield",
            "Zone of Contamination",
            "Sundered Pass",
            "Keeper's Pass South",
            "Ravine",
            "Keeper's Pass North",
            "Winding River",
            "Crumbling Highway",
            "Transfer Pass",
            "Far Territory Cave System",
            "TLDev - Forsaken Shore",
            "TLDev - Mountain Pass",
            "TLDev - Precarious Causeway",
            "TLDev - Rocky Thoroughfare",
            "TLDev - Shattered Marsh"
        )]
        public int RegionalDistressRegion = 0;

        [Name("Regional Distress Delay")]
        [Description("Hours spent in the distress region before Regional Distress appears.")]
        [Slider(1f, 336f, 336, NumberFormat = "{0:0}h")]
        public int RegionalDistressDelayHours = 72;


        [Section("Immunity Shield")]

        [Name("Enable Immunity Shield")]
        [Description("Enable or disable the Immunity Shield system.")]
        public bool EnableImmunityShield = true;

        [Name("Max Condition Affects Immunity Regen")]
        [Description("If enabled, regen requires more than 50 condition. If disabled, regen only requires more than 20 condition.")]
        public bool MaxConditionPenaltiesBlockImmunityRegen = true;


        [Section("Body Temperature")]

        [Name("Enable Body Temperature")]
        [Description("Enable or disable the internal body temperature system.")]
        public bool EnableBodyHeat = true;

        [Name("Environmental Warming Rate")]
        [Description("Default: 2.5°C/h - Maximum internal body temperature recovery from warm ambient conditions. Scales from 0% at 10°C feels-like to 100% at 30°C feels-like.")]
        [Slider(0f, 10f, 101, NumberFormat = "{0:0.00}°C/h")]
        public float PassiveHeatGain = 2.5f;

        [Name("Walking Warming Rate")]
        [Description("Default: 0.25°C/h - Internal body temperature gain while walking. Only applies while the cold meter is above 25%.")]
        [Slider(0f, 2f, 41, NumberFormat = "{0:0.00}°C/h")]
        public float WalkHeatGain = 0.25f;

        [Name("Encumbered Warming Rate")]
        [Description("Default: 1.25°C/h - Extra internal body temperature gain while encumbered. Only applies while the cold meter is above 25%.")]
        [Slider(0f, 5f, 101, NumberFormat = "{0:0.00}°C/h")]
        public float EncumberedHeatGain = 1.25f;

        [Name("Sprinting Warming Rate")]
        [Description("Default: 5.00°C/h - Internal body temperature gain while sprinting. Only applies while the cold meter is above 25% and body temperature is below 39°C.")]
        [Slider(0f, 15f, 151, NumberFormat = "{0:0.00}°C/h")]
        public float SprintHeatGain = 5f;

        [Name("Climbing Warming Rate")]
        [Description("Default: 10.00°C/h - Internal body temperature gain while climbing. Only applies while the cold meter is above 25% and body temperature is below 39°C.")]
        [Slider(0f, 20f, 201, NumberFormat = "{0:0.00}°C/h")]
        public float ClimbHeatGain = 10f;

        [Name("Cold Exposure Cooling Rate")]
        [Description("Default: 0.50°C/h - Base internal body temperature loss in cold conditions. This is multiplied by severe ambient cold and becomes much stronger when the cold meter drops below 25%.")]
        [Slider(0f, 3f, 61, NumberFormat = "{0:0.00}°C/h")]
        public float CoolingLoss = 0.5f;


        [Section("Advanced")]

        //[Name("Hunger lock arc rotation")]
        //[Description("Adjust the hunger red lock arc rotation.")]
        //[Slider(-10f, 10f, 201, NumberFormat = "{0:0.0}°")]
        //public float HungerLockArcRotation = 0f;

        //[Name("Thirst lock arc rotation")]
        //[Description("Adjust the thirst red lock arc rotation.")]
        //[Slider(-10f, 10f, 201, NumberFormat = "{0:0.0}°")]
        //public float ThirstLockArcRotation = 0f;

        //[Name("Fatigue lock arc rotation")]
        //[Description("Adjust the fatigue red lock arc rotation.")]
        //[Slider(-10f, 10f, 201, NumberFormat = "{0:0.0}°")]
        //public float FatigueLockArcRotation = 0f;

        //[Name("Cold lock arc rotation")]
        //[Description("Adjust the cold red lock arc rotation.")]
        //[Slider(-10f, 10f, 201, NumberFormat = "{0:0.0}°")]
        //public float ColdLockArcRotation = 0f;

        //[Name("Sour Stomach extra hunger rotation")]
        //[Description("Additional hunger rotation offset when Sour Stomach is active.")]
        //[Slider(-10f, 10f, 201, NumberFormat = "{0:0.0}°")]
        //public float SourStomachExtraHungerRotation = -3f;

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

            if (field.Name == nameof(VitaminCDrainMode))
            {
                Settings.UpdateVitaminCDrainVisibility();
            }

            if (field.Name == nameof(EnableBlackLung))
            {
                Settings.UpdateBlackLungVisibility();
            }

            if (field.Name == nameof(EnableSevereSprains))
            {
                Settings.UpdateSevereSprainVisibility();
            }

            if (field.Name == nameof(EnableCorpseSickness))
            {
                Settings.UpdateCorpseSicknessVisibility();
            }

            if (field.Name == nameof(EnableImmunityShield))
            {
                Settings.UpdateImmunityShieldVisibility();
            }

            if (field.Name == nameof(EnableBodyHeat))
            {
                Settings.UpdateBodyHeatVisibility();
            }

            bool regionalVisibilityChanged =
                field.Name == nameof(EnableRegionalAfflictions) ||
                field.Name == nameof(HomeRegion) ||
                field.Name == nameof(RegionalDistressRegion);

            if (regionalVisibilityChanged)
            {
                Settings.UpdateRegionalAfflictionVisibility();
            }

            bool regionalRuntimeSettingChanged =
                field.Name == nameof(EnableRegionalAfflictions) ||
                field.Name == nameof(HomeSicknessDelayHours) ||
                field.Name == nameof(RegionalDistressRegion) ||
                field.Name == nameof(RegionalDistressDelayHours);

            if (regionalRuntimeSettingChanged)
            {
                RegionalAfflictionLogic.RequestSettingsSync();
            }

            if (field.Name == nameof(RevealShinyAfflictionIconChance1) ||
                field.Name == nameof(RevealShinyAfflictionIconChance2) ||
                field.Name == nameof(RevealShinyAfflictionIconChance3))
            {
                Settings.UpdateShinyAfflictionIconChanceVisibility();
            }

            base.OnChange(field, oldValue, newValue);

            bool requiemStagesJustDisabled = field.Name == nameof(EnableRequiemStages) && oldValue is bool oldEnabled && newValue is bool newEnabled && oldEnabled && !newEnabled;

            if (requiemStagesJustDisabled)
            {
                AfflictionLogic.LogRequiemStagesDisabledByPlayer();
            }

            bool requiresStageReapply =
                field.Name == nameof(EnableRequiemStages) ||
                field.Name == nameof(OmenThreshold) ||
                field.Name == nameof(DirgeThreshold) ||
                field.Name == nameof(KnellThreshold) ||
                field.Name == nameof(RequiemThreshold);

            if (requiresStageReapply)
            {
                AfflictionLogic.ApplyCurrentStageFromGame();
            }

            bool requiresRuntimeSync =
                field.Name == nameof(EnableScarredFlesh) ||
                field.Name == nameof(EnableSepsis) ||
                field.Name == nameof(EnableCarbonMonoxide) ||
                field.Name == nameof(EnableBlackLung) ||
                field.Name == nameof(EnableCorpseSickness) ||
                field.Name == nameof(EnableSevereSprains) ||
                field.Name == nameof(SevereSprainPreset);

            if (requiresRuntimeSync)
            {
                AfflictionLogic.SyncSettingsControlledAfflictions();
            }
        }

        protected override void OnConfirm()
        {
            base.OnConfirm();

            Settings.UpdateRegionalAfflictionVisibility();

            RegionalAfflictionLogic.SyncFromSettings(logSettingsChanges: true, allowHomeRegionChange: true);
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
            UpdateVitaminCDrainVisibility();
            UpdateBlackLungVisibility();
            UpdateCorpseSicknessVisibility();
            UpdateSevereSprainVisibility();
            UpdateRegionalAfflictionVisibility();
            UpdateImmunityShieldVisibility();
            UpdateBodyHeatVisibility();
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

        internal static void UpdateVitaminCDrainVisibility()
        {
            bool showPreset = options.VitaminCDrainMode != 2;

            options.SetFieldVisible(nameof(options.VitaminCDrainPreset), showPreset);
        }

        internal static void UpdateBlackLungVisibility()
        {
            bool showBlackLungDuration = options.EnableBlackLung;

            options.SetFieldVisible(nameof(options.BlackLungDurationMode), showBlackLungDuration);
        }

        internal static void UpdateSevereSprainVisibility()
        {
            bool showSevereSprainSettings = options.EnableSevereSprains;

            options.SetFieldVisible(nameof(options.SevereSprainPreset), showSevereSprainSettings);
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

        internal static void UpdateRegionalAfflictionVisibility()
        {
            bool showRegionalSettings = options.EnableRegionalAfflictions;

            options.SetFieldVisible(nameof(options.HomeRegion), showRegionalSettings);
            options.SetFieldVisible(nameof(options.HomeSicknessDelayHours), showRegionalSettings && options.HomeRegion > 0);
            options.SetFieldVisible(nameof(options.RegionalDistressRegion), showRegionalSettings);
            options.SetFieldVisible(nameof(options.RegionalDistressDelayHours), showRegionalSettings && options.RegionalDistressRegion > 0);
        }

        internal static void UpdateImmunityShieldVisibility()
        {
            bool showImmunitySettings = options.EnableImmunityShield;

            options.SetFieldVisible(nameof(options.MaxConditionPenaltiesBlockImmunityRegen), showImmunitySettings);
        }

        internal static void UpdateBodyHeatVisibility()
        {
            bool showBodyHeatSettings = options.EnableBodyHeat;

            options.SetFieldVisible(nameof(options.PassiveHeatGain), showBodyHeatSettings);
            options.SetFieldVisible(nameof(options.WalkHeatGain), showBodyHeatSettings);
            options.SetFieldVisible(nameof(options.EncumberedHeatGain), showBodyHeatSettings);
            options.SetFieldVisible(nameof(options.SprintHeatGain), showBodyHeatSettings);
            options.SetFieldVisible(nameof(options.ClimbHeatGain), showBodyHeatSettings);
            options.SetFieldVisible(nameof(options.CoolingLoss), showBodyHeatSettings);
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