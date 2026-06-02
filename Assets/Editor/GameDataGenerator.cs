// Assets/Editor/GameDataGenerator.cs
// Unity menüsünde "Sinirsizcilar/Generate All Game Data" seçeneğiyle çalıştırılır.
// Oyun akışına uygun tüm ScriptableObject'leri oluşturur ve aralarındaki referansları otomatik kurar.
// Wave veri setleri WaveScaler mantığıyla birebir örtüşür (Wave 1-10 elle tanımlı, 11+ otomatik).

using System.IO;
using UnityEditor;
using UnityEngine;

public static class GameDataGenerator
{
    // ── Çıktı klasörleri ──────────────────────────────────────────────────
    private const string ROOT        = "Assets/Data";
    private const string SHIPS_DIR   = ROOT + "/Ships";
    private const string WAVES_DIR   = ROOT + "/Waves";
    private const string BANDITS_DIR = ROOT + "/Bandits";
    private const string CARAVAN_DIR = ROOT + "/Caravans";
    private const string DEFENSE_DIR = ROOT + "/Defenses";
    private const string WEAPON_DIR  = ROOT + "/Weapons";
    private const string RECIPE_DIR  = ROOT + "/Recipes";
    private const string UPGRADE_DIR = ROOT + "/Upgrades";
    private const string POTION_DIR  = ROOT + "/Potions";
    private const string RESOURCE_DIR= ROOT + "/Resources";
    private const string WHEELBARROW_DIR = ROOT + "/Wheelbarrows";

    // ── Ana giriş noktası ─────────────────────────────────────────────────
    [MenuItem("Sinirsizcilar/Generate All Game Data %#g")]
    public static void GenerateAll()
    {
        if (!EditorUtility.DisplayDialog(
            "Oyun Verilerini Oluştur",
            "Tüm ScriptableObject varlıkları Assets/Data/ klasörüne oluşturulacak.\n\n" +
            "Zaten var olan varlıklar atlanır (üzerine yazılmaz).\n\nDevam etmek istiyor musunuz?",
            "Evet, Oluştur",
            "İptal"))
            return;

        EnsureDirectories();

        // Bağımsız varlıklar önce (cross-reference'lar bunlara ihtiyaç duyar)
        var ships = GenerateShips();
        GenerateBandits();
        GenerateDefenses();
        GenerateWeapons();
        var potions = GeneratePotions();
        GenerateResources();

        // Bağımlı varlıklar (ships array'ini kullanır)
        GenerateWaves(ships);
        GenerateCaravans();
        GenerateRecipes(potions);
        GenerateUpgrades();
        GenerateWheelbarrows();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "✅ Tamamlandı",
            "Tüm oyun verileri Assets/Data/ klasörüne oluşturuldu!\n\n" +
            "WaveSpawner'a atanacak WaveData'lar:\nAssets/Data/Waves/\n\n" +
            "Prefab referansları daha sonra Inspector'dan bağlanmalıdır.",
            "Tamam");

        // Oluşturulan klasörü Project pencereside aç
        Object dataFolder = AssetDatabase.LoadAssetAtPath<Object>(ROOT);
        if (dataFolder != null) Selection.activeObject = dataFolder;

        Debug.Log("[GameDataGenerator] Tüm varlıklar başarıyla oluşturuldu.");
    }

    // ── Klasör garantisi ──────────────────────────────────────────────────
    private static void EnsureDirectories()
    {
        string[] dirs = { ROOT, SHIPS_DIR, WAVES_DIR, BANDITS_DIR, CARAVAN_DIR,
                          DEFENSE_DIR, WEAPON_DIR, RECIPE_DIR, UPGRADE_DIR,
                          POTION_DIR, RESOURCE_DIR, WHEELBARROW_DIR };

        foreach (string dir in dirs)
        {
            if (!AssetDatabase.IsValidFolder(dir))
            {
                string parent = Path.GetDirectoryName(dir).Replace("\\", "/");
                string folder = Path.GetFileName(dir);
                AssetDatabase.CreateFolder(parent, folder);
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // GEMİLER
    // ShipType enum: Light, Medium, Heavy, Boss
    // WaveScaler: wave 1 = 3 Light; wave 5 = boss; wave 6+ prosedürel
    // ─────────────────────────────────────────────────────────────────────
    private static ShipData[] GenerateShips()
    {
        var sloop = CreateAsset<ShipData>(SHIPS_DIR, "SD_Sloop", so =>
        {
            so.shipType      = ShipType.Light;
            so.displayName   = "Sloop (Hafif Gemi)";
            so.maxHealth     = 100f;
            so.moveSpeed     = 5f;
            so.attackDamage  = 10f;
            so.attackInterval = 3f;
        });

        var brigantine = CreateAsset<ShipData>(SHIPS_DIR, "SD_Brigantine", so =>
        {
            so.shipType      = ShipType.Medium;
            so.displayName   = "Brigantine (Orta Gemi)";
            so.maxHealth     = 250f;
            so.moveSpeed     = 3.5f;
            so.attackDamage  = 22f;
            so.attackInterval = 2.5f;
        });

        var galleon = CreateAsset<ShipData>(SHIPS_DIR, "SD_Galleon", so =>
        {
            so.shipType      = ShipType.Heavy;
            so.displayName   = "Galleon (Ağır Gemi)";
            so.maxHealth     = 600f;
            so.moveSpeed     = 2f;
            so.attackDamage  = 45f;
            so.attackInterval = 2f;
        });

        var boss = CreateAsset<ShipData>(SHIPS_DIR, "SD_BossShip", so =>
        {
            so.shipType      = ShipType.Boss;
            so.displayName   = "Korsan Amiral Gemisi (Boss)";
            so.maxHealth     = 1800f;
            so.moveSpeed     = 1.8f;
            so.attackDamage  = 70f;
            so.attackInterval = 1.5f;
        });

        return new ShipData[] { sloop, brigantine, galleon, boss };
    }

    // ─────────────────────────────────────────────────────────────────────
    // DALGALAR (Wave 1-10 elle dengeli; WaveSpawner bunları sırayla kullanır)
    // WaveScaler HandBalancedPlan: 1=3L, 2=5L, 3=5L+2M, 4=6L+3M, 5=4L+2M+1B
    // ─────────────────────────────────────────────────────────────────────
    private static void GenerateWaves(ShipData[] ships)
    {
        ShipData sloop   = ships[0]; // Light
        ShipData brig    = ships[1]; // Medium
        ShipData galleon = ships[2]; // Heavy
        ShipData boss    = ships[3]; // Boss

        // Wave 1 — sadece hafif gemiler, öğrenme eğrisi
        CreateAsset<WaveData>(WAVES_DIR, "WD_Wave01", w =>
        {
            w.waveNumber        = 1;
            w.prepPhaseDuration = 90f;
            w.spawnInterval     = 2.0f;
            w.isBossWave        = false;
            w.ships = new WaveShipEntry[]
            {
                new WaveShipEntry { shipData = sloop, count = 3 }
            };
        });

        // Wave 2 — 5 hafif gemi, tempo biraz artar
        CreateAsset<WaveData>(WAVES_DIR, "WD_Wave02", w =>
        {
            w.waveNumber        = 2;
            w.prepPhaseDuration = 85f;
            w.spawnInterval     = 1.8f;
            w.isBossWave        = false;
            w.ships = new WaveShipEntry[]
            {
                new WaveShipEntry { shipData = sloop, count = 5 }
            };
        });

        // Wave 3 — ilk kervan wave'i (CARAVAN_FIRST_WAVE=3), orta gemiler giriyor
        CreateAsset<WaveData>(WAVES_DIR, "WD_Wave03", w =>
        {
            w.waveNumber        = 3;
            w.prepPhaseDuration = 80f;
            w.spawnInterval     = 1.6f;
            w.isBossWave        = false;
            w.ships = new WaveShipEntry[]
            {
                new WaveShipEntry { shipData = sloop,   count = 5 },
                new WaveShipEntry { shipData = brig,    count = 2 }
            };
        });

        // Wave 4 — yoğunluk artıyor, hazırlık süresi kısalıyor
        CreateAsset<WaveData>(WAVES_DIR, "WD_Wave04", w =>
        {
            w.waveNumber        = 4;
            w.prepPhaseDuration = 75f;
            w.spawnInterval     = 1.4f;
            w.isBossWave        = false;
            w.ships = new WaveShipEntry[]
            {
                new WaveShipEntry { shipData = sloop, count = 6 },
                new WaveShipEntry { shipData = brig,  count = 3 }
            };
        });

        // Wave 5 — BOSS wave (BOSS_WAVE_INTERVAL=5), bossPrepBonus=20 eklenir
        CreateAsset<WaveData>(WAVES_DIR, "WD_Wave05", w =>
        {
            w.waveNumber        = 5;
            w.prepPhaseDuration = 95f; // GamePhaseController bossPrepBonus=20 ekler; taban değer
            w.spawnInterval     = 1.2f;
            w.isBossWave        = true;
            w.ships = new WaveShipEntry[]
            {
                new WaveShipEntry { shipData = sloop, count = 4 },
                new WaveShipEntry { shipData = brig,  count = 2 },
                new WaveShipEntry { shipData = boss,  count = 1 }
            };
        });

        // Wave 6 — prosedürel eşiği aşıldı, ağır gemiler giriyor (scale≈1.26)
        CreateAsset<WaveData>(WAVES_DIR, "WD_Wave06", w =>
        {
            w.waveNumber        = 6;
            w.prepPhaseDuration = 70f;
            w.spawnInterval     = 1.25f;
            w.isBossWave        = false;
            w.ships = new WaveShipEntry[]
            {
                new WaveShipEntry { shipData = sloop,   count = 4 },
                new WaveShipEntry { shipData = brig,    count = 2 },
                new WaveShipEntry { shipData = galleon, count = 1 }
            };
        });

        // Wave 7 — kervan wave'i (CARAVAN_INTERVAL=2; (7-3)%2==0)
        CreateAsset<WaveData>(WAVES_DIR, "WD_Wave07", w =>
        {
            w.waveNumber        = 7;
            w.prepPhaseDuration = 65f;
            w.spawnInterval     = 1.1f;
            w.isBossWave        = false;
            w.ships = new WaveShipEntry[]
            {
                new WaveShipEntry { shipData = sloop,   count = 5 },
                new WaveShipEntry { shipData = brig,    count = 3 },
                new WaveShipEntry { shipData = galleon, count = 1 }
            };
        });

        // Wave 8
        CreateAsset<WaveData>(WAVES_DIR, "WD_Wave08", w =>
        {
            w.waveNumber        = 8;
            w.prepPhaseDuration = 60f;
            w.spawnInterval     = 1.0f;
            w.isBossWave        = false;
            w.ships = new WaveShipEntry[]
            {
                new WaveShipEntry { shipData = sloop,   count = 5 },
                new WaveShipEntry { shipData = brig,    count = 4 },
                new WaveShipEntry { shipData = galleon, count = 2 }
            };
        });

        // Wave 9 — kervan wave'i
        CreateAsset<WaveData>(WAVES_DIR, "WD_Wave09", w =>
        {
            w.waveNumber        = 9;
            w.prepPhaseDuration = 57f;
            w.spawnInterval     = 0.9f;
            w.isBossWave        = false;
            w.ships = new WaveShipEntry[]
            {
                new WaveShipEntry { shipData = sloop,   count = 6 },
                new WaveShipEntry { shipData = brig,    count = 4 },
                new WaveShipEntry { shipData = galleon, count = 2 }
            };
        });

        // Wave 10 — BOSS wave (10 % BOSS_WAVE_INTERVAL==0); çift boss
        CreateAsset<WaveData>(WAVES_DIR, "WD_Wave10", w =>
        {
            w.waveNumber        = 10;
            w.prepPhaseDuration = 90f; // boss bonus
            w.spawnInterval     = 0.8f;
            w.isBossWave        = true;
            w.ships = new WaveShipEntry[]
            {
                new WaveShipEntry { shipData = sloop,   count = 4 },
                new WaveShipEntry { shipData = brig,    count = 3 },
                new WaveShipEntry { shipData = galleon, count = 2 },
                new WaveShipEntry { shipData = boss,    count = 2 }
            };
        });
    }

    // ─────────────────────────────────────────────────────────────────────
    // HAYDUTLAR
    // BanditType enum: Raider, Brute
    // GameConstants: BANDIT_BASE_CHANCE=0.3, BANDIT_CHANCE_INCREASE=0.05
    // ─────────────────────────────────────────────────────────────────────
    private static BanditData[] GenerateBandits()
    {
        var raider = CreateAsset<BanditData>(BANDITS_DIR, "BD_Raider", b =>
        {
            b.banditType     = BanditType.Raider;
            b.displayName    = "Raider (Hızlı Haydut)";
            b.maxHealth      = 50f;
            b.moveSpeed      = 4f;
            b.attackDamage   = 8f;
            b.attackInterval = 1.2f;
        });

        var brute = CreateAsset<BanditData>(BANDITS_DIR, "BD_Brute", b =>
        {
            b.banditType     = BanditType.Brute;
            b.displayName    = "Brute (Güçlü Haydut)";
            b.maxHealth      = 160f;
            b.moveSpeed      = 2.5f;
            b.attackDamage   = 20f;
            b.attackInterval = 1.8f;
        });

        return new BanditData[] { raider, brute };
    }

    // ─────────────────────────────────────────────────────────────────────
    // KERVAN
    // Kervan her CARAVAN_INTERVAL(2) wave'de bir, CARAVAN_FIRST_WAVE(3)'den başlar.
    // minWaveForAdvanced=5 → wave 5'ten sonra Steel/Gold/Crystal getirebilir
    // ─────────────────────────────────────────────────────────────────────
    private static void GenerateCaravans()
    {
        CreateAsset<CaravanData>(CARAVAN_DIR, "CD_BasicCaravan", c =>
        {
            c.displayName          = "Tüccar Kervanı";
            c.maxHealth            = 200f;
            c.moveSpeed            = 3f;
            c.banditChance         = 0.3f;  // GameConstants.BANDIT_BASE_CHANCE
            c.minWaveForAdvanced   = 5;
            // Başlangıç kargosunu CaravanCargoBuilder.Build() dinamik olarak doldurur —
            // burada temsili bir temel kargo tanımlanır (inspector'da düzenlenebilir).
            c.cargo = new CaravanCargoEntry[]
            {
                new CaravanCargoEntry { resourceType = ResourceType.Wood,  amount = 3 },
                new CaravanCargoEntry { resourceType = ResourceType.Stone, amount = 3 },
                new CaravanCargoEntry { resourceType = ResourceType.Iron,  amount = 2 }
            };
        });
    }

    // ─────────────────────────────────────────────────────────────────────
    // SAVUNMA VERİLERİ
    // DefenseType enum: Wall, CannonTower, ArcherTower
    // ─────────────────────────────────────────────────────────────────────
    private static DefenseData[] GenerateDefenses()
    {
        var wall = CreateAsset<DefenseData>(DEFENSE_DIR, "DD_Wall", d =>
        {
            d.defenseType  = DefenseType.Wall;
            d.displayName  = "Kale Suru";
            d.maxHealth    = 500f;
            d.damage       = 0f;
            d.range        = 0f;
            d.fireRate     = 0f;
            d.splashRadius = 0f;
        });

        var cannon = CreateAsset<DefenseData>(DEFENSE_DIR, "DD_CannonTower", d =>
        {
            d.defenseType  = DefenseType.CannonTower;
            d.displayName  = "Top Kulesi";
            d.maxHealth    = 800f;
            d.damage       = 65f;
            d.range        = 40f;
            d.fireRate     = 0.5f;   // saniyede 0.5 atış → FireInterval = 2s
            d.splashRadius = 3f;
        });

        var archer = CreateAsset<DefenseData>(DEFENSE_DIR, "DD_ArcherTower", d =>
        {
            d.defenseType  = DefenseType.ArcherTower;
            d.displayName  = "Ok Kulesi";
            d.maxHealth    = 600f;
            d.damage       = 22f;
            d.range        = 50f;
            d.fireRate     = 2f;     // saniyede 2 atış → FireInterval = 0.5s
            d.splashRadius = 0f;
        });

        return new DefenseData[] { wall, cannon, archer };
    }

    // ─────────────────────────────────────────────────────────────────────
    // SİLAHLAR
    // WeaponType enum: Sword, Shield
    // UpgradeLevel enum: Tier1, Tier2, Tier3
    // GameConstants: SWORD_BASE_DAMAGE=10, SHIELD_BASE_BLOCK=0.3
    // ─────────────────────────────────────────────────────────────────────
    private static WeaponData[] GenerateWeapons()
    {
        // ── Kılıçlar (3 tier) ─────────────────────────────────────────────
        var sword1 = CreateAsset<WeaponData>(WEAPON_DIR, "WD_Sword_T1", w =>
        {
            w.weaponType   = WeaponType.Sword;
            w.tier         = UpgradeLevel.Tier1;
            w.displayName  = "Demir Kılıç";
            w.damage       = 10f;    // SWORD_BASE_DAMAGE
            w.blockAmount  = 0f;
            w.attackSpeed  = 1.2f;
        });

        var sword2 = CreateAsset<WeaponData>(WEAPON_DIR, "WD_Sword_T2", w =>
        {
            w.weaponType   = WeaponType.Sword;
            w.tier         = UpgradeLevel.Tier2;
            w.displayName  = "Çelik Kılıç";
            w.damage       = 16f;
            w.blockAmount  = 0f;
            w.attackSpeed  = 1.4f;
        });

        var sword3 = CreateAsset<WeaponData>(WEAPON_DIR, "WD_Sword_T3", w =>
        {
            w.weaponType   = WeaponType.Sword;
            w.tier         = UpgradeLevel.Tier3;
            w.displayName  = "Kristal Kılıç";
            w.damage       = 25f;
            w.blockAmount  = 0f;
            w.attackSpeed  = 1.8f;
        });

        // ── Kalkanlar (3 tier) ────────────────────────────────────────────
        var shield1 = CreateAsset<WeaponData>(WEAPON_DIR, "WD_Shield_T1", w =>
        {
            w.weaponType   = WeaponType.Shield;
            w.tier         = UpgradeLevel.Tier1;
            w.displayName  = "Tahta Kalkan";
            w.damage       = 0f;
            w.blockAmount  = 0.3f;   // SHIELD_BASE_BLOCK — %30 hasar azaltma
            w.attackSpeed  = 0f;
        });

        var shield2 = CreateAsset<WeaponData>(WEAPON_DIR, "WD_Shield_T2", w =>
        {
            w.weaponType   = WeaponType.Shield;
            w.tier         = UpgradeLevel.Tier2;
            w.displayName  = "Demir Kalkan";
            w.damage       = 0f;
            w.blockAmount  = 0.5f;
            w.attackSpeed  = 0f;
        });

        var shield3 = CreateAsset<WeaponData>(WEAPON_DIR, "WD_Shield_T3", w =>
        {
            w.weaponType   = WeaponType.Shield;
            w.tier         = UpgradeLevel.Tier3;
            w.displayName  = "Çelik Kalkan";
            w.damage       = 0f;
            w.blockAmount  = 0.7f;
            w.attackSpeed  = 0f;
        });

        return new WeaponData[] { sword1, sword2, sword3, shield1, shield2, shield3 };
    }

    // ─────────────────────────────────────────────────────────────────────
    // CRAFT TARİFLERİ
    // CraftingStation.CanCraft(): recipe.requiredStationLevel <= station.level
    // ─────────────────────────────────────────────────────────────────────
    private static void GenerateRecipes(PotionData[] potions)
    {
        PotionData pdStrength = (potions != null && potions.Length > 0) ? potions[0] : null;
        PotionData pdHearing = (potions != null && potions.Length > 1) ? potions[1] : null;

        // ── Tier 1 tarifler (başlangıçtan itibaren) ───────────────────────

        // Güç İksiri — Wood×2 + Stone×1
        CreateAsset<RecipeData>(RECIPE_DIR, "R_Potion_Strength", r =>
        {
            r.recipeName           = "Güç İksiri";
            r.requiredStationLevel = UpgradeLevel.Tier1;
            r.craftDuration        = 12f;
            r.outputWeapon         = null;
            r.outputDefense        = null;
            r.outputPotion         = pdStrength;
            r.ingredients = new RecipeIngredient[]
            {
                new RecipeIngredient { resourceType = ResourceType.Wood,  amount = 2 },
                new RecipeIngredient { resourceType = ResourceType.Stone, amount = 1 }
            };
        });

        // İşitme İksiri — Iron×1 + Wood×1
        CreateAsset<RecipeData>(RECIPE_DIR, "R_Potion_Hearing", r =>
        {
            r.recipeName           = "İşitme İksiri";
            r.requiredStationLevel = UpgradeLevel.Tier1;
            r.craftDuration        = 10f;
            r.outputWeapon         = null;
            r.outputDefense        = null;
            r.outputPotion         = pdHearing;
            r.ingredients = new RecipeIngredient[]
            {
                new RecipeIngredient { resourceType = ResourceType.Iron, amount = 1 },
                new RecipeIngredient { resourceType = ResourceType.Wood, amount = 1 }
            };
        });

        // Demir Kılıç — Iron×3 + Wood×1
        CreateAsset<RecipeData>(RECIPE_DIR, "R_Sword_T1", r =>
        {
            r.recipeName           = "Demir Kılıç";
            r.requiredStationLevel = UpgradeLevel.Tier1;
            r.craftDuration        = 15f;
            r.outputWeapon         = WeaponType.Sword;
            r.outputDefense        = null;
            r.ingredients = new RecipeIngredient[]
            {
                new RecipeIngredient { resourceType = ResourceType.Iron, amount = 3 },
                new RecipeIngredient { resourceType = ResourceType.Wood, amount = 1 }
            };
        });

        // Tahta Kalkan — Wood×4 + Iron×1
        CreateAsset<RecipeData>(RECIPE_DIR, "R_Shield_T1", r =>
        {
            r.recipeName           = "Tahta Kalkan";
            r.requiredStationLevel = UpgradeLevel.Tier1;
            r.craftDuration        = 12f;
            r.outputWeapon         = WeaponType.Shield;
            r.outputDefense        = null;
            r.ingredients = new RecipeIngredient[]
            {
                new RecipeIngredient { resourceType = ResourceType.Wood, amount = 4 },
                new RecipeIngredient { resourceType = ResourceType.Iron, amount = 1 }
            };
        });

        // ── Tier 2 tarifler ───────────────────────────────────────────────

        // Çelik Kılıç — Steel×3 + Iron×2
        CreateAsset<RecipeData>(RECIPE_DIR, "R_Sword_T2", r =>
        {
            r.recipeName           = "Çelik Kılıç";
            r.requiredStationLevel = UpgradeLevel.Tier2;
            r.craftDuration        = 20f;
            r.outputWeapon         = WeaponType.Sword;
            r.outputDefense        = null;
            r.ingredients = new RecipeIngredient[]
            {
                new RecipeIngredient { resourceType = ResourceType.Steel, amount = 3 },
                new RecipeIngredient { resourceType = ResourceType.Iron,  amount = 2 }
            };
        });

        // Demir Kalkan — Steel×2 + Iron×3
        CreateAsset<RecipeData>(RECIPE_DIR, "R_Shield_T2", r =>
        {
            r.recipeName           = "Demir Kalkan";
            r.requiredStationLevel = UpgradeLevel.Tier2;
            r.craftDuration        = 18f;
            r.outputWeapon         = WeaponType.Shield;
            r.outputDefense        = null;
            r.ingredients = new RecipeIngredient[]
            {
                new RecipeIngredient { resourceType = ResourceType.Steel, amount = 2 },
                new RecipeIngredient { resourceType = ResourceType.Iron,  amount = 3 }
            };
        });

        // ── Tier 3 tarifler ───────────────────────────────────────────────

        // Kristal Kılıç — Crystal×2 + Gold×2 + Steel×1
        CreateAsset<RecipeData>(RECIPE_DIR, "R_Sword_T3", r =>
        {
            r.recipeName           = "Kristal Kılıç";
            r.requiredStationLevel = UpgradeLevel.Tier3;
            r.craftDuration        = 30f;
            r.outputWeapon         = WeaponType.Sword;
            r.outputDefense        = null;
            r.ingredients = new RecipeIngredient[]
            {
                new RecipeIngredient { resourceType = ResourceType.Crystal, amount = 2 },
                new RecipeIngredient { resourceType = ResourceType.Gold,    amount = 2 },
                new RecipeIngredient { resourceType = ResourceType.Steel,   amount = 1 }
            };
        });

        // Çelik Kalkan — Crystal×1 + Gold×1 + Steel×3
        CreateAsset<RecipeData>(RECIPE_DIR, "R_Shield_T3", r =>
        {
            r.recipeName           = "Çelik Kalkan";
            r.requiredStationLevel = UpgradeLevel.Tier3;
            r.craftDuration        = 28f;
            r.outputWeapon         = WeaponType.Shield;
            r.outputDefense        = null;
            r.ingredients = new RecipeIngredient[]
            {
                new RecipeIngredient { resourceType = ResourceType.Crystal, amount = 1 },
                new RecipeIngredient { resourceType = ResourceType.Gold,    amount = 1 },
                new RecipeIngredient { resourceType = ResourceType.Steel,   amount = 3 }
            };
        });
    }

    // ─────────────────────────────────────────────────────────────────────
    // YÜKSELTMELEr
    // UpgradeLevel enum: Tier1 → Tier2 → Tier3
    // CraftingStation, Wheelbarrow, Tower yükseltilebilir
    // ─────────────────────────────────────────────────────────────────────
    private static void GenerateUpgrades()
    {
        // ── Craft İstasyonu Yükseltmeleri ─────────────────────────────────
        CreateAsset<UpgradeData>(UPGRADE_DIR, "UD_CraftStation_T1_T2", u =>
        {
            u.upgradeName = "Craft İstasyonu T2";
            u.fromLevel   = UpgradeLevel.Tier1;
            u.toLevel     = UpgradeLevel.Tier2;
            u.upgradeTime = 20f;
            u.description = "Çelik ekipman üretiminin kilidini açar.";
            u.cost = new RecipeIngredient[]
            {
                new RecipeIngredient { resourceType = ResourceType.Stone, amount = 5 },
                new RecipeIngredient { resourceType = ResourceType.Iron,  amount = 3 }
            };
        });

        CreateAsset<UpgradeData>(UPGRADE_DIR, "UD_CraftStation_T2_T3", u =>
        {
            u.upgradeName = "Craft İstasyonu T3";
            u.fromLevel   = UpgradeLevel.Tier2;
            u.toLevel     = UpgradeLevel.Tier3;
            u.upgradeTime = 40f;
            u.description = "Kristal ekipman üretiminin kilidini açar.";
            u.cost = new RecipeIngredient[]
            {
                new RecipeIngredient { resourceType = ResourceType.Steel,   amount = 4 },
                new RecipeIngredient { resourceType = ResourceType.Gold,    amount = 2 }
            };
        });

        // ── El Arabası Yükseltmeleri ──────────────────────────────────────
        CreateAsset<UpgradeData>(UPGRADE_DIR, "UD_Wheelbarrow_T1_T2", u =>
        {
            u.upgradeName = "El Arabası T2";
            u.fromLevel   = UpgradeLevel.Tier1;
            u.toLevel     = UpgradeLevel.Tier2;
            u.upgradeTime = 15f;
            u.description = "Kapasite 5'e, hız çarpanı 0.85'e yükselir.";
            u.cost = new RecipeIngredient[]
            {
                new RecipeIngredient { resourceType = ResourceType.Wood,  amount = 4 },
                new RecipeIngredient { resourceType = ResourceType.Iron,  amount = 2 }
            };
        });

        CreateAsset<UpgradeData>(UPGRADE_DIR, "UD_Wheelbarrow_T2_T3", u =>
        {
            u.upgradeName = "El Arabası T3";
            u.fromLevel   = UpgradeLevel.Tier2;
            u.toLevel     = UpgradeLevel.Tier3;
            u.upgradeTime = 30f;
            u.description = "Kapasite 8'e, hız çarpanı 1.0'a yükselir.";
            u.cost = new RecipeIngredient[]
            {
                new RecipeIngredient { resourceType = ResourceType.Steel, amount = 3 },
                new RecipeIngredient { resourceType = ResourceType.Iron,  amount = 4 }
            };
        });

        // ── Top Kulesi Yükseltmeleri ──────────────────────────────────────
        CreateAsset<UpgradeData>(UPGRADE_DIR, "UD_CannonTower_T1_T2", u =>
        {
            u.upgradeName = "Top Kulesi T2";
            u.fromLevel   = UpgradeLevel.Tier1;
            u.toLevel     = UpgradeLevel.Tier2;
            u.upgradeTime = 25f;
            u.description = "Hasar +25, Menzil +5.";
            u.cost = new RecipeIngredient[]
            {
                new RecipeIngredient { resourceType = ResourceType.Stone, amount = 6 },
                new RecipeIngredient { resourceType = ResourceType.Iron,  amount = 4 }
            };
        });

        CreateAsset<UpgradeData>(UPGRADE_DIR, "UD_CannonTower_T2_T3", u =>
        {
            u.upgradeName = "Top Kulesi T3";
            u.fromLevel   = UpgradeLevel.Tier2;
            u.toLevel     = UpgradeLevel.Tier3;
            u.upgradeTime = 45f;
            u.description = "Hasar +50, AoE yarıçapı +2.";
            u.cost = new RecipeIngredient[]
            {
                new RecipeIngredient { resourceType = ResourceType.Steel,   amount = 5 },
                new RecipeIngredient { resourceType = ResourceType.Gold,    amount = 3 }
            };
        });

        // ── Ok Kulesi Yükseltmeleri ───────────────────────────────────────
        CreateAsset<UpgradeData>(UPGRADE_DIR, "UD_ArcherTower_T1_T2", u =>
        {
            u.upgradeName = "Ok Kulesi T2";
            u.fromLevel   = UpgradeLevel.Tier1;
            u.toLevel     = UpgradeLevel.Tier2;
            u.upgradeTime = 20f;
            u.description = "Atış hızı +0.5, Menzil +10.";
            u.cost = new RecipeIngredient[]
            {
                new RecipeIngredient { resourceType = ResourceType.Wood,  amount = 5 },
                new RecipeIngredient { resourceType = ResourceType.Iron,  amount = 3 }
            };
        });

        CreateAsset<UpgradeData>(UPGRADE_DIR, "UD_ArcherTower_T2_T3", u =>
        {
            u.upgradeName = "Ok Kulesi T3";
            u.fromLevel   = UpgradeLevel.Tier2;
            u.toLevel     = UpgradeLevel.Tier3;
            u.upgradeTime = 40f;
            u.description = "Hasar ×1.5, zincirleme ok efekti.";
            u.cost = new RecipeIngredient[]
            {
                new RecipeIngredient { resourceType = ResourceType.Steel,   amount = 4 },
                new RecipeIngredient { resourceType = ResourceType.Crystal, amount = 1 }
            };
        });
    }

    // ─────────────────────────────────────────────────────────────────────
    // İKSİRLER
    // PotionType enum: Strength, Hearing
    // GameConstants: STRENGTH_POTION_DURATION=30, HEARING_POTION_DURATION=45
    //                STRENGTH_MULTIPLIER=1.5, HEARING_RANGE_MULTIPLIER=2.0
    // ─────────────────────────────────────────────────────────────────────
    private static PotionData[] GeneratePotions()
    {
        var strength = CreateAsset<PotionData>(POTION_DIR, "PD_Strength", p =>
        {
            p.potionType      = PotionType.Strength;
            p.displayName     = "Güç İksiri";
            p.duration        = 30f;   // STRENGTH_POTION_DURATION
            p.effectValue     = 1.5f;  // STRENGTH_MULTIPLIER
            p.screenTintColor = new Color(1f, 0.35f, 0.1f, 0.18f); // turuncumsu
        });

        var hearing = CreateAsset<PotionData>(POTION_DIR, "PD_Hearing", p =>
        {
            p.potionType      = PotionType.Hearing;
            p.displayName     = "İşitme İksiri";
            p.duration        = 45f;   // HEARING_POTION_DURATION
            p.effectValue     = 2.0f;  // HEARING_RANGE_MULTIPLIER
            p.screenTintColor = new Color(0.1f, 0.7f, 1f, 0.12f);  // mavimsi
        });

        return new PotionData[] { strength, hearing };
    }

    // ─────────────────────────────────────────────────────────────────────
    // KAYNAKLAR
    // ResourceType: Wood, Stone, Iron, Steel, Gold, Crystal
    // ResourceTier: Basic (3), Advanced (3)
    // CarryWeight: Light, Heavy
    // ─────────────────────────────────────────────────────────────────────
    private static ResourceData[] GenerateResources()
    {
        var wood = CreateAsset<ResourceData>(RESOURCE_DIR, "RD_Wood", r =>
        {
            r.resourceType = ResourceType.Wood;
            r.tier         = ResourceTier.Basic;
            r.displayName  = "Odun";
            r.weight       = CarryWeight.Light;
        });

        var stone = CreateAsset<ResourceData>(RESOURCE_DIR, "RD_Stone", r =>
        {
            r.resourceType = ResourceType.Stone;
            r.tier         = ResourceTier.Basic;
            r.displayName  = "Taş";
            r.weight       = CarryWeight.Heavy;
        });

        var iron = CreateAsset<ResourceData>(RESOURCE_DIR, "RD_Iron", r =>
        {
            r.resourceType = ResourceType.Iron;
            r.tier         = ResourceTier.Basic;
            r.displayName  = "Demir";
            r.weight       = CarryWeight.Heavy;
        });

        var steel = CreateAsset<ResourceData>(RESOURCE_DIR, "RD_Steel", r =>
        {
            r.resourceType = ResourceType.Steel;
            r.tier         = ResourceTier.Advanced;
            r.displayName  = "Çelik";
            r.weight       = CarryWeight.Heavy;
        });

        var gold = CreateAsset<ResourceData>(RESOURCE_DIR, "RD_Gold", r =>
        {
            r.resourceType = ResourceType.Gold;
            r.tier         = ResourceTier.Advanced;
            r.displayName  = "Altın";
            r.weight       = CarryWeight.Light;
        });

        var crystal = CreateAsset<ResourceData>(RESOURCE_DIR, "RD_Crystal", r =>
        {
            r.resourceType = ResourceType.Crystal;
            r.tier         = ResourceTier.Advanced;
            r.displayName  = "Kristal";
            r.weight       = CarryWeight.Light;
        });

        return new ResourceData[] { wood, stone, iron, steel, gold, crystal };
    }

    // ─────────────────────────────────────────────────────────────────────
    // EL ARABASI
    // WheelbarrowController: WHEELBARROW_BASE_CAPACITY=3, WHEELBARROW_BASE_SPEED=0.7
    // ─────────────────────────────────────────────────────────────────────
    private static void GenerateWheelbarrows()
    {
        CreateAsset<WheelbarrowData>(WHEELBARROW_DIR, "WBD_T1", w =>
        {
            w.level           = UpgradeLevel.Tier1;
            w.capacity        = 3;     // WHEELBARROW_BASE_CAPACITY
            w.speedMultiplier = 0.7f;  // WHEELBARROW_BASE_SPEED
            w.displayName     = "Basit El Arabası";
        });

        CreateAsset<WheelbarrowData>(WHEELBARROW_DIR, "WBD_T2", w =>
        {
            w.level           = UpgradeLevel.Tier2;
            w.capacity        = 5;
            w.speedMultiplier = 0.85f;
            w.displayName     = "Güçlendirilmiş El Arabası";
        });

        CreateAsset<WheelbarrowData>(WHEELBARROW_DIR, "WBD_T3", w =>
        {
            w.level           = UpgradeLevel.Tier3;
            w.capacity        = 8;
            w.speedMultiplier = 1.0f;  // tam normal hız
            w.displayName     = "Çelik El Arabası";
        });
    }

    // ─────────────────────────────────────────────────────────────────────
    // YARDIMCI: Tek bir ScriptableObject asset'i güvenli şekilde oluşturur.
    // Dosya zaten varsa mevcut asset'i döndürür (üzerine yazmaz).
    // ─────────────────────────────────────────────────────────────────────
    private static T CreateAsset<T>(string dir, string name, System.Action<T> configure)
        where T : ScriptableObject
    {
        string path = $"{dir}/{name}.asset";

        T existing = AssetDatabase.LoadAssetAtPath<T>(path);
        if (existing != null)
        {
            Debug.Log($"[GameDataGenerator] Zaten mevcut, atlandı: {path}");
            return existing;
        }

        T asset = ScriptableObject.CreateInstance<T>();
        configure(asset);
        AssetDatabase.CreateAsset(asset, path);
        Debug.Log($"[GameDataGenerator] Oluşturuldu: {path}");
        return asset;
    }
}
