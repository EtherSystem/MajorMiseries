namespace MajorMiseries.Managers
{
    internal readonly struct AuroraRegionExposureInfo
    {
        internal readonly string Tier;
        internal readonly float Multiplier;
        internal readonly string Reason;

        internal AuroraRegionExposureInfo(string tier, float multiplier, string reason)
        {
            Tier = tier;
            Multiplier = multiplier;
            Reason = reason;
        }
    }

    internal static class AuroraRegionExposureSolver
    {
        internal static readonly AuroraRegionExposureInfo Unknown = new("Unknown", 1f, "No Aurora exposure mapping found; using medium baseline.");
        private static readonly AuroraRegionExposureInfo Low = new("Low", 0.75f, "Low lore exposure region.");
        private static readonly AuroraRegionExposureInfo Medium = new("Medium", 1f, "Medium lore exposure region.");
        private static readonly AuroraRegionExposureInfo High = new("High", 1.5f, "High lore exposure region.");
        private static readonly AuroraRegionExposureInfo LowMediumTransition = new("Low/Medium Transition", 0.875f, "Transition zone between low and medium exposure regions.");
        private static readonly AuroraRegionExposureInfo MediumHighTransition = new("Medium/High Transition", 1.25f, "Transition zone between medium and high exposure regions.");

        private static readonly Dictionary<string, AuroraRegionExposureInfo> s_ExposureByLogicalRegion = new(StringComparer.OrdinalIgnoreCase)
        {
            // LOW EXPOSURE
            { "TracksRegion", Low },                  // Broken Railroad
            { "MountainTownRegion", Low },            // Mountain Town
            { "RiverValleyRegion", Low },             // Hushed River Valley
            { "MarshRegion", Low },                   // Forlorn Muskeg
            { "CanneryRegion", Low },                 // Bleak Inlet
            { "DamRiverTransitionZone", Low },        // Winding River
            { "DamRiverTransitionZoneB", Low },
            { "LakeRegion", Low },                    // Mystery Lake

            // MEDIUM EXPOSURE
            { "RuralRegion", Medium },                // Pleasant Valley
            { "CoastalRegion", Medium },              // Coastal Highway
            { "HighwayTransitionZone", Medium },      // Crumbling Highway
            { "WhalingStationRegion", Medium },       // Desolation Point
            { "ModForsakenShore", Medium },           // TLDev maps
            { "ModMountainPass", Medium },
            { "ModPrecariousCauseway", Medium },
            { "ModRockyThoroughfare", Medium },
            { "ModShatteredMarsh", Medium },

            // HIGH EXPOSURE
            { "BlackrockRegion", High },              // Blackrock
            { "BlackrockPrisonSurvivalZone", High },
            { "AshCanyonRegion", High },              // Ash Canyon
            { "CrashMountainRegion", High },          // Timberwolf Mountain
            { "AirfieldRegion", High },               // Forsaken Airfield
            { "MiningRegion", High },                 // Zone of Contamination
            { "MountainPassRegion", High },           // Sundered Pass
            { "HubRegion", High },                    // Transfer Pass / Far Territory hub

            // LOGICAL TRANSITIONS BETWEEN NEIGHBOURING REGIONS
            { "RavineTransitionZone", LowMediumTransition },      // Mystery Lake <-> Coastal Highway
            { "CanyonRoadTransitionZone", MediumHighTransition }, // Pleasant Valley <-> Blackrock route
            { "BlackrockTransitionZone", MediumHighTransition },  // Keeper's Pass North / Blackrock approach
            { "CanyonRoadCave", MediumHighTransition },
            { "MountainTownCaveTransitionZone", Low },            // Milton <-> Mystery Lake style low exposure
            { "CaveTransitionZone", Medium },
            { "MineTransitionZone", Medium },
            { "HubCaveTransitionZone", High },
            { "FarTerritoryCaveSystem", High }
        };

        internal static AuroraRegionExposureInfo Resolve(string? logicalRegion)
        {
            if (string.IsNullOrEmpty(logicalRegion)) return Unknown;
            return s_ExposureByLogicalRegion.TryGetValue(logicalRegion, out AuroraRegionExposureInfo info) ? info : Unknown;
        }
    }
}
