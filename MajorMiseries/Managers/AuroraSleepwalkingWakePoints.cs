using Random = UnityEngine.Random;

namespace MajorMiseries.Managers
{
    internal static class AuroraSleepwalkingWakePoints
    {
        private static readonly Dictionary<string, Vector3[]> s_WakePointsByScene = new(StringComparer.OrdinalIgnoreCase)
        {
            // LOW EXPOSURE REGIONS
            { "TracksRegion",
                new[]
                {
                    new Vector3(636.25f, 239.08f, 1253.67f),
                    new Vector3(385.51f, 184.63f, 1297.11f),
                    new Vector3(220.32f, 185.01f, 866.56f),
                    new Vector3(579.70f, 198.97f, 563.46f),
                    new Vector3(552f, 200.08f, 680.48f),
                    new Vector3(252.21f, 185.23f, 700.86f),
                    new Vector3(776.30f, 91.80f, 357.33f),
                    new Vector3(818.43f, 236.56f, 334.62f),
                    new Vector3(825.37f, 235.98f, 444.86f),
                    new Vector3(667.70f, 201.04f, 567.53f),
                    new Vector3(509.39f, 247.72f, 764.40f),
                    new Vector3(167.37f, 243.64f, 802.78f),
                    new Vector3(-106.46f, 253.80f, 869.02f),
                    new Vector3(69.38f, 243.59f, 442.24f),
                    new Vector3(331.71f, 207.06f, 349.24f)
                }
            },
            { "MountainTownRegion",
                new[]
                {
                    new Vector3(618.20f, 237.78f, 1428.71f),
                    new Vector3(620.87f, 201.81f, 1187.64f),
                    new Vector3(479.52f, 5f, 729.03f),
                    new Vector3(867.84f, 58.44f, 885.63f),
                    new Vector3(1334.17f, 263.80f, 916.54f),
                    new Vector3(1375.69f, 264.76f, 1200.53f),
                    new Vector3(1428.18f, 327.87f, 1567.15f),
                    new Vector3(2027.31f, 373.58f, 1435.53f),
                    new Vector3(1706.78f, 347.72f, 1928.79f),
                    new Vector3(1759.68f, 348.23f, 2205.90f),
                    new Vector3(1150.75f, 260.80f, 1697.66f),
                    new Vector3(867.74f, 228.30f, 1669.23f),
                    new Vector3(1166.63f, 266.22f, 1352.579f),
                    new Vector3(966.10f, 304.15f, 1235.72f),
                    new Vector3(749.53f, 199.17f, 1244.07f)
                }
            },
            { "RiverValleyRegion",
                new[]
                {
                    new Vector3(1421.82f, 15.81f, 1047.79f),
                    new Vector3(1717.87f, 18.36f, 1025.14f),
                    new Vector3(944.76f, 15.97f, 1031.90f),
                    new Vector3(807.21f, 119.71f, 719.14f),
                    new Vector3(467.57f, 119.58f, 933.58f),
                    new Vector3(277.63f, 137.81f, 519.29f),
                    new Vector3(323.04f, 75.21f, 1060.89f),
                    new Vector3(608.50f, 98.43f, 1126.97f),
                    new Vector3(573.44f, 102.80f, 1583.97f),
                    new Vector3(1154.64f, 195.21f, 770.86f),
                    new Vector3(1366.27f, 92.68f, 1114.30f),
                    new Vector3(1048.19f, 152.23f, 1263.86f),
                    new Vector3(1100.90f, 162.24f, 1500.28f),
                    new Vector3(1324.68f, 228.65f, 1323.81f),
                    new Vector3(1463.53f, 285.09f, 1373.88f)
                }
            },
            { "MarshRegion",
                new[]
                {
                    new Vector3(1666.20f, -120.20f, 1089.04f),
                    new Vector3(1484.82f, -115.66f, 1452.72f),
                    new Vector3(1135.34f, -108.29f, 1448.46f),
                    new Vector3(1108.61f, -131.71f, 973.23f),
                    new Vector3(1087.23f, -121.46f, 977.94f),
                    new Vector3(617.34f, -133.01f, 978.63f),
                    new Vector3(155.66f, -133.31f, 657.88f),
                    new Vector3(75.99f, -105.07f, 1055.42f),
                    new Vector3(236.93f, -72.67f, 1311.57f),
                    new Vector3(166.93f, -102.32f, 617.19f),
                    new Vector3(555.44f, -132.93f, 337.04f),
                    new Vector3(1061.16f, -124.75f, 263.02f),
                    new Vector3(1579.51f, -103.74f, 79.01f),
                    new Vector3(1597.04f, -50.72f, 292.80f),
                    new Vector3(480.73f, -44.83f, 1363.35f)
                }
            },
            { "CanneryRegion",
                new[]
                {
                    new Vector3(687.50f, 65.52f, -288.68f),
                    new Vector3(477.36f, 46.76f, -117.11f),
                    new Vector3(94.14f, 24.32f, -473.27f),
                    new Vector3(141.23f, 27.96f, -694.93f),
                    new Vector3(311.01f, 59.69f, -549.78f),
                    new Vector3(-194.56f, 36.03f, -358.96f),
                    new Vector3(-815.72f, 25.43f, -913.45f),
                    new Vector3(-372.38f, 25.18f, -616.28f),
                    new Vector3(-354.78f, 32.66f, -510.40f),
                    new Vector3(-477.35f, 38.65f, -481.67f),
                    new Vector3(-243.96f, 25.18f, -472.69f),
                    new Vector3(-28.46f, 63.46f, 56.88f),
                    new Vector3(62.04f, 59.55f, 44.18f),
                    new Vector3(689.64f, 92.11f, 352.50f),
                    new Vector3(-1038.81f, 61.08f, -234.36f)
                }
            },
            { "DamRiverTransitionZoneB",
                new[]
                {
                    new Vector3(824.75f, 5.60f, 615.61f),
                    new Vector3(599.05f, 8.68f, 705.91f),
                    new Vector3(426.10f, 28.88f, 674.94f),
                    new Vector3(540.18f, 68.60f, 559.90f),
                    new Vector3(476.28f, 74.60f, 452.19f),
                    new Vector3(555.22f, 89.23f, 247.78f)
                }
            },
            { "LakeRegion",
                new[]
                {
                    new Vector3(1367.47f, 16.38f, 898.72f),
                    new Vector3(1374.88f, 72.51f, 584.78f),
                    new Vector3(1240.07f, 29.94f, 259.74f),
                    new Vector3(824.20f, 144.92f, 1076.85f),
                    new Vector3(1639.04f, 17.91f, 839.37f),
                    new Vector3(1306.85f, 37.84f, -105.05f),
                    new Vector3(1603.22f, 18.34f, 45.98f),
                    new Vector3(1444.31f, 45.89f, 397.49f),
                    new Vector3(371.47f, 13.39f, 512.72f),
                    new Vector3(391.65f, -2.31f, 420.69f),
                    new Vector3(847.72f, 111.11f, -56.21f),
                    new Vector3(140.23f, 3.08f, 52.59f),
                    new Vector3(1204.51f, 112.07f, -94.47f),
                    new Vector3(805.62f, 143.95f, 946.59f),
                    new Vector3(1173.57f, 162.62f, 691.21f),
                    new Vector3(426.42f, 160.14f, 1025.78f),
                    new Vector3(553.35f, 181.74f, 1630.39f),
                    new Vector3(793.23f, 186.36f, 993.31f),
                    new Vector3(968.16f, 38.88f, 845.62f),
                    new Vector3(1238.10f, 38.46f, 1180.18f)
                }
            },

            // MEDIUM EXPOSURE REGIONS
            {
                "CoastalRegion",
                new[]
                {
                    new Vector3(-427.44f, 23.05f, 260.59f),
                    new Vector3(1028.56f, 99.20f, 680.80f),
                    new Vector3(691.26f, 34.07f, -426.22f),
                    new Vector3(361.54f, 55.49f, 241.21f),
                    new Vector3(-426.13f, 23.05f, 257.60f),
                    new Vector3(-696.95f, 23.05f, 623.43f),
                    new Vector3(-719.61f, 132.81f, 887.69f),
                    new Vector3(-996.61f, 186.02f, 1205.84f),
                    new Vector3(-867.11f, 23.05f, 114.43f),
                    new Vector3(332.80f, 189.31f, 1149.75f),
                    new Vector3(205.56f, 26.71f, 279.30f),
                    new Vector3(612.74f, 23.31f, 290.64f),
                    new Vector3(18.74f, 23.05f, 714.23f),
                    new Vector3(57.80f, 37.92f, 981.78f),
                    new Vector3(197.51f, 223.59f, 1342.48f)
                }
            },
            { "RuralRegion",
                new[]
                {
                    new Vector3(2213.66f, 44.88f, 2324.62f),
                    new Vector3(2436.28f, 47.64f, 2309.18f),
                    new Vector3(1820.72f, 45.77f, 1948.89f),
                    new Vector3(1718.97f, 54.79f, 2535.68f),
                    new Vector3(1394.81f, 88.97f, 2694.21f),
                    new Vector3(945.55f, 139.47f, 2539.49f),
                    new Vector3(355.70f, 140.83f, 2624.34f),
                    new Vector3(670f, 140.96f, 2277.52f),
                    new Vector3(428.84f, 191.26f, 1614.09f),
                    new Vector3(474.99f, 62.46f, 590.79f),
                    new Vector3(708.87f, 50.44f, 776.57f),
                    new Vector3(668.34f, 41.99f, 1162.84f),
                    new Vector3(1453.43f, 38.60f, 1875.14f),
                    new Vector3(1435.34f, 36.94f, 2253.79f),
                    new Vector3(2448.22f, 53.20f, 2257.90f)
                }
            },
            { "HighwayTransitionZone",
                new[]
                {
                    new Vector3(225.79f, 46.08f, 289.15f),
                    new Vector3(356.72f, 46.08f, 184.25f),
                    new Vector3(586.77f, 46.08f, 176.66f),
                    new Vector3(721.40f, 70.48f, 411.42f),
                    new Vector3(392.90f, 66.19f, 459.96f),
                    new Vector3(656.81f, 70.39f, 485.16f)
                }
            },
            { "WhalingStationRegion",
                new[]
                {
                    new Vector3(570.94f, 77.69f, 1178.43f),
                    new Vector3(427.26f, 42.57f, 1125.52f),
                    new Vector3(616.07f, 15.89f, 833.28f),
                    new Vector3(877.23f, 15.82f, 731.22f),
                    new Vector3(1105.29f, 15.82f, 714.39f),
                    new Vector3(1250.45f, 15.82f, 817.60f),
                    new Vector3(1149.33f, 15.84f, 1302.60f),
                    new Vector3(980.38f, 23.04f, 1223.25f),
                    new Vector3(1015.50f, 15.82f, 1615.01f),
                    new Vector3(791.07f, 45.17f, 1421.16f),
                    new Vector3(806f, 15.82f, 1036.19f),
                    new Vector3(1036.12f, 28.90f, 1001.30f),
                    new Vector3(359.05f, 15.82f, 848.72f),
                    new Vector3(566.75f, 63.28f, 1105.70f),
                    new Vector3(714.72f, 42.46f, 740.02f)
                }
            },

            // HIGH EXPOSURE REGIONS
            { "BlackrockRegion",
                new[]
                {
                    new Vector3(-342.64f, 142.67f, -969.81f),
                    new Vector3(-329.31f, 138.04f, -842.51f),
                    new Vector3(-371.11f, 110.18f, -802.16f),
                    new Vector3(-752.64f, 77.28f, -729.92f),
                    new Vector3(-769.67f, 84.53f, -493.55f),
                    new Vector3(-1071.38f, 76.17f, -60.16f),
                    new Vector3(-825.08f, 127.21f, 36.85f),
                    new Vector3(-308.30f, 76.91f, 447.17f),
                    new Vector3(-468.86f, 112.10f, 132.16f),
                    new Vector3(-298.89f, 225.81f, 65.27f),
                    new Vector3(-8.27f, 202.79f, 109.39f),
                    new Vector3(160.82f, 156.23f, 193.46f),
                    new Vector3(374.37f, 289.63f, 616.21f),
                    new Vector3(623.77f, 361.92f, 846.31f),
                    new Vector3(893.96f, 229.09f, -92.84f)
                }
            },
            { "AshCanyonRegion",
                new[]
                {
                    new Vector3(-220.41f, 69.51f, 102.17f),
                    new Vector3(91.58f, 91.15f, 483.44f),
                    new Vector3(-280.83f, 50.28f, -384.58f),
                    new Vector3(-126.73f, 280.23f, 585.06f),
                    new Vector3(-298.31f, 144.82f, 832.24f),
                    new Vector3(124.08f, 177.83f, 53.51f),
                    new Vector3(-236.23f, 200.39f, -251.48f),
                    new Vector3(-714.45f, 225.89f, -74.19f),
                    new Vector3(-326.13f, 126.22f, -339.49f),
                    new Vector3(189.69f, 130.47f, -574.34f),
                    new Vector3(711.41f, 67.64f, -339.85f),
                    new Vector3(433.41f, 67.80f, -192.60f),
                    new Vector3(539.74f, 103.58f, 543.51f),
                    new Vector3(-378.75f, 160.68f, 724.23f),
                    new Vector3(-394.56f, 150.73f, 115.51f)
                }
            },
            { "CrashMountainRegion",
                new[]
                {
                    new Vector3(883.86f, 148.89f, 409.47f),
                    new Vector3(1136.32f, 132.50f, 747.79f),
                    new Vector3(1679.39f, 205.65f, 966.16f),
                    new Vector3(1785.43f, 149.09f, 706.08f),
                    new Vector3(1687.01f, 153.09f, 283.50f),
                    new Vector3(1470.37f, 151.07f, 1328.16f),
                    new Vector3(1470.35f, 226.08f, 1634.92f),
                    new Vector3(872.37f, 458.12f, 1504.37f),
                    new Vector3(375.73f, 344.51f, 1788.54f),
                    new Vector3(366.81f, 334.43f, 1233.84f),
                    new Vector3(868f, 348.06f, 1005.72f),
                    new Vector3(824.60f, 218.27f, 109.95f),
                    new Vector3(1459f, 61.16f, 954.57f),
                    new Vector3(1561.29f, 103.02f, 729.40f),
                    new Vector3(1371.49f, 146.04f, 1211.82f)
                }
            },
            { "AirfieldRegion",
                new[]
                {
                    new Vector3(-176.60f, 133.88f, -1173.70f),
                    new Vector3(-22.04f, 158.02f, -714.46f),
                    new Vector3(187.68f, 160.19f, -153.58f),
                    new Vector3(109.16f, 153.16f, 689.63f),
                    new Vector3(373.28f, 158.78f, 932.93f),
                    new Vector3(139.25f, 151.04f, 1355.45f),
                    new Vector3(-723.81f, 216.75f, 1180.38f),
                    new Vector3(-1329.48f, 251.79f, 442.91f),
                    new Vector3(-1065.59f, 197.73f, -57.43f),
                    new Vector3(-1134f, 274.89f, -576.59f),
                    new Vector3(-1143.92f, 151.01f, -770.96f),
                    new Vector3(-204.75f, 151.08f, -734.39f),
                    new Vector3(-559.60f, 275.55f, -1072.19f),
                    new Vector3(-138.49f, 141.42f, -1158.44f),
                    new Vector3(1205.72f, 308.46f, -699.46f)
                }
            },
            { "MiningRegion",
                new[]
                {
                    new Vector3(738.45f, 237.58f, -134.61f),
                    new Vector3(747.04f, 318.25f, 130.49f),
                    new Vector3(921.94f, 324.47f, 493.90f),
                    new Vector3(477.94f, 297.66f, 504.89f),
                    new Vector3(92.57f, 240.32f, 505.57f),
                    new Vector3(-478.68f, 202.81f, 451.72f),
                    new Vector3(-885.20f, 199.73f, 455.25f),
                    new Vector3(-761.33f, 214.17f, -158.24f),
                    new Vector3(-641.89f, 239.34f, -275.04f),
                    new Vector3(-459.86f, 310.24f, 85f),
                    new Vector3(-365.94f, 221.91f, -437.50f),
                    new Vector3(-317.57f, 150.36f, -74.60f),
                    new Vector3(-229.25f, 197.57f, 186.83f),
                    new Vector3(-240.52f, 197.99f, 106.68f),
                    new Vector3(212.56f, 135.64f, -1028.90f)
                }
            },
            { "MountainPassRegion",
                new[]
                {
                    new Vector3(270.83f, 8.26f, 1096.05f),
                    new Vector3(549.14f, 206.49f, 576.18f),
                    new Vector3(347.27f, 156.08f, 948.28f),
                    new Vector3(64.74f, 258.45f, 556.94f),
                    new Vector3(6.35f, 377.31f, 8.35f),
                    new Vector3(52.85f, 529.14f, -716.09f),
                    new Vector3(378.12f, 459.72f, -596.75f),
                    new Vector3(391.48f, 415.86f, -593.14f),
                    new Vector3(-188.98f, 431.80f, -346.31f),
                    new Vector3(-458.10f, 526.12f, -564.90f),
                    new Vector3(-188.30f, 595.27f, -1142.48f),
                    new Vector3(678.52f, 446.45f, -802.36f),
                    new Vector3(766.16f, 509.10f, -693.34f),
                    new Vector3(828.89f, 379.56f, 415.41f),
                    new Vector3(662.59f, 511.47f, -397.78f)
                }
            },
            { "HubRegion",
                new[]
                {
                    new Vector3(42.83f, 262.15f, -59.37f),
                    new Vector3(33.19f, 253.16f, 261.96f),
                    new Vector3(99.35f, 257.07f, 356.12f),
                    new Vector3(216.24f, 251.48f, 390.43f),
                    new Vector3(212.50f, 255.99f, 109.09f),
                    new Vector3(-128.36f, 309.25f, 545.13f)
                }
            },


            // TLDev MODDED REGIONS
            { "ModForsakenShore",
                new[]
                {
                    new Vector3(184.34f, 353.49f, 788.50f),
                    new Vector3(-264.89f, 353.98f, 271.17f),
                    new Vector3(73.46f, 353.98f, 285.77f),
                    new Vector3(177.76f, 376.59f, 295.94f),
                    new Vector3(691.39f, 353.36f, 646.94f),
                    new Vector3(934.68f, 408.29f, 1039.98f),
                    new Vector3(1207.20f, 405.04f, 1054.11f),
                    new Vector3(842.58f, 396.91f, 1447.82f),
                    new Vector3(748.47f, 575.93f, 1480.98f),
                    new Vector3(1564.43f, 359.60f, 686.29f),
                    new Vector3(1923.92f, 355.42f, 1154.73f),
                    new Vector3(2264.39f, 353.69f, 1087.87f),
                    new Vector3(1749.19f, 353.89f, 1056.35f),
                    new Vector3(1656.44f, 401.99f, 1341.14f),
                    new Vector3(829.35f, 353.43f, 144.95f)
                }
            },
            { "ModMountainPass",
                new[]
                {
                    new Vector3(2300.44f, 145.85f, 1054.81f),
                    new Vector3(2358.85f, 145.95f, 866.93f),
                    new Vector3(548.25f, 31.04f, 1735.00f),
                    new Vector3(1327.64f, 29.10f, 2043.91f),
                    new Vector3(1691.67f, 49.73f, 2327.11f),
                    new Vector3(1385.50f, 29.18f, 2161.86f),
                    new Vector3(1627.96f, 60.87f, 1312.34f),
                    new Vector3(1974.04f, 44.30f, 1367.32f),
                    new Vector3(2031.24f, 77.13f, 1093.96f),
                    new Vector3(1876.20f, 45.88f, 1259.15f)
                }
            },
            { "ModPrecariousCauseway",
                new[]
                {
                    new Vector3(847.89f, 221.12f, 2249.70f),
                    new Vector3(954.61f, 219.63f, 2453.56f),
                    new Vector3(723.28f, 220.83f, 2322.57f),
                    new Vector3(1137.18f, 221.43f, 2345.90f),
                    new Vector3(1243.88f, 219.48f, 2239.17f),
                    new Vector3(1131.91f, 219.83f, 1940.88f),
                    new Vector3(1089.14f, 219.56f, 2190.63f),
                    new Vector3(1057.49f, 219.47f, 2258.63f),
                    new Vector3(1051.67f, 228.31f, 2756.05f),
                    new Vector3(938.72f, 219.57f, 2736.32f),
                    new Vector3(830.73f, 219.52f, 3005.05f),
                    new Vector3(902.41f, 219.58f, 3051.99f),
                    new Vector3(987.85f, 249.56f, 2939.65f),
                    new Vector3(921.66f, 264.22f, 2990.49f),
                    new Vector3(1173.22f, 232.00f, 2988.71f),
                    new Vector3(1301.39f, 219.88f, 3174.67f),
                    new Vector3(1123.62f, 227.73f, 3191.87f),
                    new Vector3(1065.79f, 219.54f, 3087.88f)
                }
            },
            { "ModRockyThoroughfare",
                new[]
                {
                    new Vector3(1068.27f, 248.07f, 1311.84f),
                    new Vector3(1271.66f, 247.93f, 1354.92f),
                    new Vector3(1196.69f, 247.88f, 1428.74f),
                    new Vector3(1039.58f, 247.90f, 1573.71f),
                    new Vector3(1147.85f, 281.56f, 1560.73f),
                    new Vector3(1133.26f, 281.00f, 1357.65f),
                    new Vector3(748.69f, 247.98f, 1105.42f),
                    new Vector3(148.34f, 246.84f, 1102.77f),
                    new Vector3(25.32f, 324.62f, 745.12f),
                    new Vector3(-66.65f, 335.16f, 813.56f),
                    new Vector3(-298.39f, 262.47f, 1403.37f),
                    new Vector3(-226.32f, 246.48f, 1534.81f),
                    new Vector3(586.43f, 255.29f, 1195.98f),
                    new Vector3(33.25f, 255.80f, 1111.29f),
                    new Vector3(105.96f, 247.47f, 1258.53f)
                }
            },
            { "ModShatteredMarsh",
                new[]
                {
                    new Vector3(2385.18f, 438.05f, 5064.14f),
                    new Vector3(1763.55f, 457.79f, 4557.23f),
                    new Vector3(1528.42f, 437.24f, 4766.33f),
                    new Vector3(1758.75f, 438.32f, 5119.65f),
                    new Vector3(2227.48f, 437.21f, 5084.05f),
                    new Vector3(2173.15f, 437.00f, 5399.58f),
                    new Vector3(1840.20f, 437.00f, 5586.16f),
                    new Vector3(1568.83f, 437.00f, 5680.87f),
                    new Vector3(1164.99f, 437.23f, 5157.50f),
                    new Vector3(897.92f, 437.51f, 5395.00f),
                    new Vector3(828.99f, 437.66f, 5682.91f),
                    new Vector3(693.08f, 439.00f, 5058.30f),
                    new Vector3(751.25f, 437.00f, 4731.30f),
                    new Vector3(1125.26f, 438.39f, 4552.90f)
                }
            },
            { "ModProponentsReach",
                new[]
                {
                    new Vector3(598.18f, 192.46f, 390.84f),
                    new Vector3(271.92f, 221.19f, 613.51f),
                    new Vector3(472.97f, 188.84f, 900.88f),
                    new Vector3(271.69f, 130.41f, 754.84f),
                    new Vector3(535.74f, 130.41f, 648.26f),
                    new Vector3(704.22f, 130.32f, 746.00f),
                    new Vector3(685.15f, 130.41f, 885.69f)
                }
            },

            // TRANSITION REGIONS
            { "RavineTransitionZone",
                new[]
                {
                    new Vector3(-1019.87f, 127.31f, -92.54f),
                    new Vector3(-713.15f, 114.62f, -219.89f),
                    new Vector3(-489.93f, 18.15f, -166.10f),
                    new Vector3(-288.03f, 135f, 47.55f),
                    new Vector3(-8.52f, 133.53f, 19.61f),
                    new Vector3(-676.34f, 19.57f, -187.28f)
                }
            },
            { "LongRailTransitionZone",
                new[]
                {
                    new Vector3(75.08f, 0.74f, 460.65f),
                    new Vector3(-136.20f, -49.08f, 51.47f),
                    new Vector3(73.80f, -49.13f, 12.43f)
                }
            },
            { "BlackrockTransitionZone",
                new[]
                {
                    new Vector3(411.44f, 183.57f, -166.85f),
                    new Vector3(460.07f, 253.51f, 24.08f),
                    new Vector3(538.34f, 255.76f, -106.43f),
                    new Vector3(840.24f, 230.32f, -50.43f),
                    new Vector3(698.84f, 242.31f, -165.39f)
                }
            },
            { "CanyonRoadTransitionZone",
                new[]
                {
                    new Vector3(313.47f, 39.66f, 38.97f),
                    new Vector3(325.36f, 39.66f, 254.42f),
                    new Vector3(201.14f, 39.66f, 460.51f),
                    new Vector3(524.55f, 51.01f, 561.96f),
                    new Vector3(445.17f, 51.03f, 569.38f)
                }
            },
        };

        private static readonly Dictionary<string, string> s_DefaultOutdoorSceneByLogicalRegion = new(StringComparer.OrdinalIgnoreCase)
        {
            { "TracksRegion", "TracksRegion" },
            { "MountainTownRegion", "MountainTownRegion" },
            { "RiverValleyRegion", "RiverValleyRegion" },
            { "MarshRegion", "MarshRegion" },
            { "CanneryRegion", "CanneryRegion" },
            { "DamRiverTransitionZone", "DamRiverTransitionZoneB" },
            { "DamRiverTransitionZoneB", "DamRiverTransitionZoneB" },
            { "LakeRegion", "LakeRegion" },
            { "CoastalRegion", "CoastalRegion" },
            { "RuralRegion", "RuralRegion" },
            { "HighwayTransitionZone", "HighwayTransitionZone" },
            { "WhalingStationRegion", "WhalingStationRegion" },
            { "BlackrockRegion", "BlackrockRegion" },
            { "AshCanyonRegion", "AshCanyonRegion" },
            { "CrashMountainRegion", "CrashMountainRegion" },
            { "AirfieldRegion", "AirfieldRegion" },
            { "MiningRegion", "MiningRegion" },
            { "MountainPassRegion", "MountainPassRegion" },
            { "HubRegion", "HubRegion" },

            // TLDev modded regions
            { "ModForsakenShore", "ModForsakenShore" },
            { "ModMountainPass", "ModMountainPass" },
            { "ModPrecariousCauseway", "ModPrecariousCauseway" },
            { "ModPrecariousCavern", "ModPrecariousCauseway" },
            { "ModRockyThoroughfare", "ModRockyThoroughfare" },
            { "ModShatteredMarsh", "ModShatteredMarsh" },
            { "ModProponentsReach", "ModProponentsReach" },

            { "RavineTransitionZone", "RavineTransitionZone" },
            { "LongRailTransitionZone", "LongRailTransitionZone" },
            { "BlackrockTransitionZone", "BlackrockTransitionZone" },
            { "CanyonRoadTransitionZone", "CanyonRoadTransitionZone" }
        };

        internal static bool HasSceneEntry(string sceneName)
        {
            return !string.IsNullOrEmpty(sceneName) && s_WakePointsByScene.ContainsKey(sceneName);
        }

        internal static bool TryGetDefaultOutdoorScene(string logicalRegion, out string sceneName)
        {
            if (!string.IsNullOrEmpty(logicalRegion) && s_DefaultOutdoorSceneByLogicalRegion.TryGetValue(logicalRegion, out string? resolvedScene))
            {
                sceneName = resolvedScene;
                return true;
            }

            sceneName = string.Empty;
            return false;
        }

        internal static bool TrySelectWakePoint(string sceneName, out Vector3 wakePoint, out int configuredPointCount)
        {
            wakePoint = Vector3.zero;
            configuredPointCount = 0;

            if (string.IsNullOrEmpty(sceneName)) return false;
            if (!s_WakePointsByScene.TryGetValue(sceneName, out Vector3[]? points)) return false;

            configuredPointCount = points.Length;
            if (points.Length <= 0) return false;

            wakePoint = points[Random.Range(0, points.Length)];
            return true;
        }
    }
}