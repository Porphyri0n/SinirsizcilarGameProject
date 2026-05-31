using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Unity.Netcode;
using TMPro;

public class GamePrefabsBuilder : EditorWindow
{
    [MenuItem("Tools/Build Remaining Prefabs")]
    public static void BuildRemainingPrefabs()
    {
        Debug.Log("Starting Remaining Prefabs Build Process...");

        // Ensure directories exist
        CreateDirectoryIfNotExists("Assets/Prefabs/Caravans");
        CreateDirectoryIfNotExists("Assets/Prefabs/Bandits");
        CreateDirectoryIfNotExists("Assets/Prefabs/Ships");
        CreateDirectoryIfNotExists("Assets/Prefabs/Resources");

        // Ensure Catalogs exist and are updated
        GetOrCreateRecipeCatalog();
        GetOrCreateUpgradeCatalog();

        // 1. Build Caravan Prefab
        BuildCaravanPrefab();

        // 2. Build Bandit Prefabs
        BuildBanditPrefab("Bandit_Raider", "Assets/Data/Bandits/BD_Raider.asset", "Assets/ModularHeroBundlePolyart/ModularAnimalKnightsPolyart/Prefab/Standard/RatStandard.prefab", 1.0f);
        BuildBanditPrefab("Bandit_Brute", "Assets/Data/Bandits/BD_Brute.asset", "Assets/ModularHeroBundlePolyart/ModularAnimalKnightsPolyart/Prefab/Standard/LionStandard.prefab", 1.25f);

        // 3. Build Ship Prefabs
        BuildShipPrefab("Ship_Sloop", "Assets/Data/Ships/SD_Sloop.asset", new Vector3(1f, 1f, 1f));
        BuildShipPrefab("Ship_Brigantine", "Assets/Data/Ships/SD_Brigantine.asset", new Vector3(1.4f, 1.2f, 1.4f));
        BuildShipPrefab("Ship_Galleon", "Assets/Data/Ships/SD_Galleon.asset", new Vector3(1.8f, 1.4f, 1.8f));
        BuildBossShipPrefab();

        // 4. Build CraftingStation Prefab
        BuildCraftingStationPrefab();

        // 5. Build Wheelbarrow Prefab
        BuildWheelbarrowPrefab();

        // 6. Build Loot Potion Prefab
        BuildLootPotionPrefab();

        Debug.Log("Remaining Prefabs Build Process Completed Successfully!");
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static void CreateDirectoryIfNotExists(string path)
    {
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }
    }

    private static void BuildCaravanPrefab()
    {
        string path = "Assets/Prefabs/Caravans/Caravan.prefab";
        Debug.Log($"Building Caravan Prefab: {path}");

        GameObject root = new GameObject("Caravan");
        root.tag = "Caravan";

        // Add Netcode component
        root.AddComponent<NetworkObject>();

        // Add components
        CaravanController controller = root.AddComponent<CaravanController>();
        CaravanMovement movement = root.AddComponent<CaravanMovement>();
        CaravanNetSync netSync = root.AddComponent<CaravanNetSync>();

        // Add BoxCollider (non-trigger) for physical damage hits
        BoxCollider boxCol = root.AddComponent<BoxCollider>();
        boxCol.center = new Vector3(0, 0.6f, 0);
        boxCol.size = new Vector3(1.6f, 1.3f, 2.6f);

        // Build Hierarchy
        // 1. CaravanMesh
        GameObject cartSrc = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Raygeas/Suntail Village/Assets/Prefabs/Environment/Cart_1.prefab");
        if (cartSrc != null)
        {
            GameObject cartObj = PrefabUtility.InstantiatePrefab(cartSrc) as GameObject;
            cartObj.name = "CaravanMesh";
            cartObj.transform.SetParent(root.transform, false);
            cartObj.transform.localPosition = Vector3.zero;
            cartObj.transform.localRotation = Quaternion.identity;
        }

        // 2. CargoContainer
        GameObject containerObj = new GameObject("CargoContainer");
        containerObj.transform.SetParent(root.transform, false);
        containerObj.transform.localPosition = new Vector3(0, 0.8f, -0.5f);

        // Configure Controller
        CaravanData data = AssetDatabase.LoadAssetAtPath<CaravanData>("Assets/Data/Caravans/CD_BasicCaravan.asset");
        SerializedObject soController = new SerializedObject(controller);
        soController.FindProperty("data").objectReferenceValue = data;
        soController.FindProperty("movement").objectReferenceValue = movement;
        soController.ApplyModifiedProperties();

        // Configure Movement
        SerializedObject soMovement = new SerializedObject(movement);
        soMovement.FindProperty("data").objectReferenceValue = data;
        soMovement.FindProperty("arrivalDistance").floatValue = 1.5f;
        soMovement.FindProperty("turnSpeed").floatValue = 4f;
        soMovement.ApplyModifiedProperties();

        // Configure NetSync
        SerializedObject soNetSync = new SerializedObject(netSync);
        soNetSync.FindProperty("controller").objectReferenceValue = controller;
        soNetSync.FindProperty("movement").objectReferenceValue = movement;
        soNetSync.ApplyModifiedProperties();

        // Save
        PrefabUtility.SaveAsPrefabAsset(root, path);
        DestroyImmediate(root);
    }

    private static void BuildBanditPrefab(string name, string dataPath, string meshPath, float scale)
    {
        string path = $"Assets/Prefabs/Bandits/{name}.prefab";
        Debug.Log($"Building Bandit Prefab ({name}): {path}");

        GameObject root = new GameObject(name);
        root.tag = "Bandit";

        // Add Netcode component
        root.AddComponent<NetworkObject>();

        // Add components
        BanditAI ai = root.AddComponent<BanditAI>();
        BanditHealth health = root.AddComponent<BanditHealth>();
        BanditNetSync netSync = root.AddComponent<BanditNetSync>();

        // Rigidbody + CapsuleCollider (isKinematic: true)
        Rigidbody rb = root.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        CapsuleCollider col = root.AddComponent<CapsuleCollider>();
        col.center = new Vector3(0, 0.9f, 0);
        col.radius = 0.4f;
        col.height = 1.8f;

        // Build Hierarchy
        GameObject meshSrc = AssetDatabase.LoadAssetAtPath<GameObject>(meshPath);
        if (meshSrc != null)
        {
            GameObject meshObj = PrefabUtility.InstantiatePrefab(meshSrc) as GameObject;
            meshObj.name = "BanditMesh";
            meshObj.transform.SetParent(root.transform, false);
            meshObj.transform.localPosition = Vector3.zero;
            meshObj.transform.localRotation = Quaternion.identity;
            meshObj.transform.localScale = new Vector3(scale, scale, scale);
        }

        // Configure health & data
        BanditData data = AssetDatabase.LoadAssetAtPath<BanditData>(dataPath);
        
        SerializedObject soAI = new SerializedObject(ai);
        soAI.FindProperty("data").objectReferenceValue = data;
        soAI.FindProperty("health").objectReferenceValue = health;
        soAI.FindProperty("attackRange").floatValue = 1.8f;
        soAI.FindProperty("playerAggroRange").floatValue = 8f;
        soAI.ApplyModifiedProperties();

        SerializedObject soHealth = new SerializedObject(health);
        soHealth.FindProperty("banditData").objectReferenceValue = data;
        soHealth.ApplyModifiedProperties();

        // Save
        PrefabUtility.SaveAsPrefabAsset(root, path);
        DestroyImmediate(root);
    }

    private static void BuildShipPrefab(string name, string dataPath, Vector3 scale)
    {
        string path = $"Assets/Prefabs/Ships/{name}.prefab";
        Debug.Log($"Building Ship Prefab ({name}): {path}");

        GameObject root = new GameObject(name);
        root.tag = "Enemy";

        // Add Netcode
        root.AddComponent<NetworkObject>();

        // Add components
        ShipHealth health = root.AddComponent<ShipHealth>();
        ShipMovement movement = root.AddComponent<ShipMovement>();
        ShipAttack attack = root.AddComponent<ShipAttack>();
        ShipNetSync netSync = root.AddComponent<ShipNetSync>();
        ShipSinkAnimation sinkAnim = root.AddComponent<ShipSinkAnimation>();

        // BoxCollider
        BoxCollider boxCol = root.AddComponent<BoxCollider>();
        boxCol.center = new Vector3(0, 0.8f * scale.y, 0);
        boxCol.size = new Vector3(2.5f * scale.x, 1.6f * scale.y, 4.5f * scale.z);

        // Visuals
        GameObject boatSrc = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Raygeas/Suntail Village/Assets/Prefabs/Environment/Boat.prefab");
        if (boatSrc != null)
        {
            GameObject boatObj = PrefabUtility.InstantiatePrefab(boatSrc) as GameObject;
            boatObj.name = "ShipMesh";
            boatObj.transform.SetParent(root.transform, false);
            boatObj.transform.localPosition = Vector3.zero;
            boatObj.transform.localRotation = Quaternion.identity;
            boatObj.transform.localScale = scale;
        }

        // Configure data
        ShipData data = AssetDatabase.LoadAssetAtPath<ShipData>(dataPath);

        SerializedObject soHealth = new SerializedObject(health);
        soHealth.FindProperty("shipData").objectReferenceValue = data;
        soHealth.ApplyModifiedProperties();

        SerializedObject soMovement = new SerializedObject(movement);
        soMovement.FindProperty("shipData").objectReferenceValue = data;
        soMovement.FindProperty("arrivalDistance").floatValue = 1.5f;
        soMovement.FindProperty("turnSpeed").floatValue = 4f;
        soMovement.ApplyModifiedProperties();

        SerializedObject soAttack = new SerializedObject(attack);
        soAttack.FindProperty("shipData").objectReferenceValue = data;
        soAttack.FindProperty("movement").objectReferenceValue = movement;
        soAttack.ApplyModifiedProperties();

        SerializedObject soNetSync = new SerializedObject(netSync);
        soNetSync.FindProperty("shipHealth").objectReferenceValue = health;
        soNetSync.ApplyModifiedProperties();

        SerializedObject soSink = new SerializedObject(sinkAnim);
        soSink.FindProperty("shipHealth").objectReferenceValue = health;
        soSink.FindProperty("sinkDuration").floatValue = 3f;
        soSink.FindProperty("sinkDepth").floatValue = 4f;
        soSink.FindProperty("maxTiltAngle").floatValue = 25f;
        soSink.ApplyModifiedProperties();

        // Save
        PrefabUtility.SaveAsPrefabAsset(root, path);
        DestroyImmediate(root);
    }

    private static void BuildBossShipPrefab()
    {
        string path = "Assets/Prefabs/Ships/BossShip.prefab";
        Debug.Log($"Building BossShip Prefab: {path}");

        GameObject root = new GameObject("BossShip");
        root.tag = "Enemy";

        // Add Netcode
        root.AddComponent<NetworkObject>();

        Vector3 scale = new Vector3(2.8f, 2.0f, 2.8f);

        // Add components
        BossShip bossComponent = root.AddComponent<BossShip>();
        ShipHealth health = root.AddComponent<ShipHealth>();
        ShipMovement movement = root.AddComponent<ShipMovement>();
        ShipAttack attack = root.AddComponent<ShipAttack>();
        ShipNetSync netSync = root.AddComponent<ShipNetSync>();
        ShipSinkAnimation sinkAnim = root.AddComponent<ShipSinkAnimation>();
        ShipDamageEffect dmgEffect = root.AddComponent<ShipDamageEffect>();

        // BoxCollider
        BoxCollider boxCol = root.AddComponent<BoxCollider>();
        boxCol.center = new Vector3(0, 0.8f * scale.y, 0);
        boxCol.size = new Vector3(2.5f * scale.x, 1.6f * scale.y, 4.5f * scale.z);

        // Visuals
        GameObject boatSrc = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Raygeas/Suntail Village/Assets/Prefabs/Environment/Boat.prefab");
        if (boatSrc != null)
        {
            GameObject boatObj = PrefabUtility.InstantiatePrefab(boatSrc) as GameObject;
            boatObj.name = "ShipMesh";
            boatObj.transform.SetParent(root.transform, false);
            boatObj.transform.localPosition = Vector3.zero;
            boatObj.transform.localRotation = Quaternion.identity;
            boatObj.transform.localScale = scale;
        }

        // Configure data
        ShipData data = AssetDatabase.LoadAssetAtPath<ShipData>("Assets/Data/Ships/SD_BossShip.asset");

        // Base Ship data setup
        SerializedObject soBoss = new SerializedObject(bossComponent);
        soBoss.FindProperty("shipData").objectReferenceValue = data;
        soBoss.FindProperty("healthMultiplier").floatValue = 3f;
        soBoss.FindProperty("bonusLootCount").intValue = 4;
        soBoss.FindProperty("bonusLootRadius").floatValue = 4f;
        soBoss.FindProperty("attackBurstCount").intValue = 3;
        soBoss.FindProperty("burstInterval").floatValue = 0.4f;
        soBoss.FindProperty("burstDamage").floatValue = 15f;
        soBoss.FindProperty("zigzagAmplitude").floatValue = 2.5f;
        soBoss.FindProperty("zigzagFrequency").floatValue = 0.4f;
        soBoss.ApplyModifiedProperties();

        SerializedObject soHealth = new SerializedObject(health);
        soHealth.FindProperty("shipData").objectReferenceValue = data;
        soHealth.ApplyModifiedProperties();

        SerializedObject soMovement = new SerializedObject(movement);
        soMovement.FindProperty("shipData").objectReferenceValue = data;
        soMovement.FindProperty("arrivalDistance").floatValue = 1.5f;
        soMovement.FindProperty("turnSpeed").floatValue = 4f;
        soMovement.ApplyModifiedProperties();

        SerializedObject soAttack = new SerializedObject(attack);
        soAttack.FindProperty("shipData").objectReferenceValue = data;
        soAttack.FindProperty("movement").objectReferenceValue = movement;
        soAttack.ApplyModifiedProperties();

        SerializedObject soNetSync = new SerializedObject(netSync);
        soNetSync.FindProperty("shipHealth").objectReferenceValue = health;
        soNetSync.ApplyModifiedProperties();

        // Sink anim (subscribes to BossShip base as it inherits from ShipBase)
        SerializedObject soSink = new SerializedObject(sinkAnim);
        soSink.FindProperty("shipBase").objectReferenceValue = bossComponent;
        soSink.FindProperty("shipHealth").objectReferenceValue = health;
        soSink.FindProperty("sinkDuration").floatValue = 3f;
        soSink.FindProperty("sinkDepth").floatValue = 4f;
        soSink.FindProperty("maxTiltAngle").floatValue = 25f;
        soSink.ApplyModifiedProperties();

        // Damage Effect (Requires ShipBase -> BossShip)
        SerializedObject soDmg = new SerializedObject(dmgEffect);
        soDmg.FindProperty("ship").objectReferenceValue = bossComponent;
        soDmg.ApplyModifiedProperties();

        // Save
        PrefabUtility.SaveAsPrefabAsset(root, path);
        DestroyImmediate(root);
    }

    private static void BuildCraftingStationPrefab()
    {
        string path = "Assets/Prefabs/CraftingStation.prefab";
        Debug.Log($"Building CraftingStation Prefab: {path}");

        GameObject root = new GameObject("CraftingStation");
        root.tag = "Interactable";

        // Add Netcode
        root.AddComponent<NetworkObject>();

        // Add components
        CraftingStation station = root.AddComponent<CraftingStation>();
        CraftQueueManager queue = root.AddComponent<CraftQueueManager>();
        UpgradeManager upgrade = root.AddComponent<UpgradeManager>();
        CraftNetSync netSync = root.AddComponent<CraftNetSync>();

        // Add Trigger BoxCollider
        BoxCollider boxCol = root.AddComponent<BoxCollider>();
        boxCol.isTrigger = true;
        boxCol.center = new Vector3(0, 0.8f, 0);
        boxCol.size = new Vector3(2.5f, 1.8f, 2f);

        // Visuals (Workbench = Table + Anvil)
        GameObject stationMesh = new GameObject("StationMesh");
        stationMesh.transform.SetParent(root.transform, false);

        GameObject tableSrc = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Raygeas/Suntail Village/Assets/Prefabs/Environment/Table_1.prefab");
        if (tableSrc != null)
        {
            GameObject tableObj = PrefabUtility.InstantiatePrefab(tableSrc) as GameObject;
            tableObj.name = "Table";
            tableObj.transform.SetParent(stationMesh.transform, false);
            tableObj.transform.localPosition = Vector3.zero;
            tableObj.transform.localRotation = Quaternion.identity;
        }

        GameObject anvilSrc = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Raygeas/Suntail Village/Assets/Prefabs/Environment/Anvil.prefab");
        if (anvilSrc != null)
        {
            GameObject anvilObj = PrefabUtility.InstantiatePrefab(anvilSrc) as GameObject;
            anvilObj.name = "Anvil";
            anvilObj.transform.SetParent(stationMesh.transform, false);
            anvilObj.transform.localPosition = new Vector3(0, 0.8f, 0); // on top of table
            anvilObj.transform.localRotation = Quaternion.identity;
        }

        // Configure CraftingStation array fields
        // 1. Recipes
        string[] recipePaths = {
            "Assets/Data/Recipes/R_Potion_Strength.asset",
            "Assets/Data/Recipes/R_Potion_Hearing.asset",
            "Assets/Data/Recipes/R_Sword_T1.asset",
            "Assets/Data/Recipes/R_Shield_T1.asset"
        };
        List<RecipeData> recipesList = new List<RecipeData>();
        foreach (var rPath in recipePaths)
        {
            RecipeData r = AssetDatabase.LoadAssetAtPath<RecipeData>(rPath);
            if (r != null) recipesList.Add(r);
        }

        // 2. Upgrades
        UpgradeData u1 = AssetDatabase.LoadAssetAtPath<UpgradeData>("Assets/Data/Upgrades/UD_CraftStation_T1_T2.asset");
        UpgradeData u2 = AssetDatabase.LoadAssetAtPath<UpgradeData>("Assets/Data/Upgrades/UD_CraftStation_T2_T3.asset");

        SerializedObject soStation = new SerializedObject(station);
        SerializedProperty recipesProp = soStation.FindProperty("recipes");
        recipesProp.ClearArray();
        for (int i = 0; i < recipesList.Count; i++)
        {
            recipesProp.InsertArrayElementAtIndex(i);
            recipesProp.GetArrayElementAtIndex(i).objectReferenceValue = recipesList[i];
        }

        SerializedProperty upgradesProp = soStation.FindProperty("upgrades");
        upgradesProp.ClearArray();
        if (u1 != null)
        {
            upgradesProp.InsertArrayElementAtIndex(0);
            upgradesProp.GetArrayElementAtIndex(0).objectReferenceValue = u1;
        }
        if (u2 != null)
        {
            upgradesProp.InsertArrayElementAtIndex(upgradesProp.arraySize);
            upgradesProp.GetArrayElementAtIndex(upgradesProp.arraySize - 1).objectReferenceValue = u2;
        }
        soStation.FindProperty("queue").objectReferenceValue = queue;
        soStation.ApplyModifiedProperties();

        // Configure NetSync
        SerializedObject soNet = new SerializedObject(netSync);
        soNet.FindProperty("catalog").objectReferenceValue = GetOrCreateRecipeCatalog();
        soNet.ApplyModifiedProperties();

        // Save
        PrefabUtility.SaveAsPrefabAsset(root, path);
        DestroyImmediate(root);
    }

    private static void BuildWheelbarrowPrefab()
    {
        string path = "Assets/Prefabs/Wheelbarrow.prefab";
        Debug.Log($"Building Wheelbarrow Prefab: {path}");

        GameObject root = new GameObject("Wheelbarrow");
        
        // Add Rigidbody (useGravity: true, drag/angularDrag: 1f)
        Rigidbody rb = root.AddComponent<Rigidbody>();
        rb.useGravity = true;
        rb.isKinematic = false;
        rb.linearDamping = 1f;
        rb.angularDamping = 1f;

        // Add components
        WheelbarrowController controller = root.AddComponent<WheelbarrowController>();

        // Trigger BoxCollider
        BoxCollider boxCol = root.AddComponent<BoxCollider>();
        boxCol.isTrigger = true;
        boxCol.center = new Vector3(0, 0.5f, 0);
        boxCol.size = new Vector3(1.2f, 1f, 1.8f);

        // Visuals (constructed from box + wheel + handle bars)
        GameObject barrowMesh = new GameObject("WheelbarrowMesh");
        barrowMesh.transform.SetParent(root.transform, false);

        // Box/Tray
        GameObject boxSrc = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Raygeas/Suntail Village/Assets/Prefabs/Environment/Box_1.prefab");
        if (boxSrc != null)
        {
            GameObject boxObj = PrefabUtility.InstantiatePrefab(boxSrc) as GameObject;
            boxObj.name = "Tray";
            boxObj.transform.SetParent(barrowMesh.transform, false);
            boxObj.transform.localPosition = new Vector3(0, 0.4f, -0.2f);
            boxObj.transform.localRotation = Quaternion.identity;
            boxObj.transform.localScale = new Vector3(0.8f, 0.6f, 1f);
        }

        // Wheel
        GameObject wheelSrc = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ToonScapes/Spring Isles/Prefabs/Props/Wood Props/TSI_Wood_Wheel_01A.prefab");
        if (wheelSrc != null)
        {
            GameObject wheelObj = PrefabUtility.InstantiatePrefab(wheelSrc) as GameObject;
            wheelObj.name = "Wheel";
            wheelObj.transform.SetParent(barrowMesh.transform, false);
            wheelObj.transform.localPosition = new Vector3(0, 0.25f, 0.6f);
            wheelObj.transform.localRotation = Quaternion.Euler(0, 90f, 0); // align wheel plane along Z axis
            wheelObj.transform.localScale = new Vector3(0.6f, 0.6f, 0.6f);
        }

        // Load level data
        WheelbarrowData w1 = AssetDatabase.LoadAssetAtPath<WheelbarrowData>("Assets/Data/Wheelbarrows/WBD_T1.asset");
        WheelbarrowData w2 = AssetDatabase.LoadAssetAtPath<WheelbarrowData>("Assets/Data/Wheelbarrows/WBD_T2.asset");
        WheelbarrowData w3 = AssetDatabase.LoadAssetAtPath<WheelbarrowData>("Assets/Data/Wheelbarrows/WBD_T3.asset");

        // Load UpgradeData
        UpgradeData u1 = AssetDatabase.LoadAssetAtPath<UpgradeData>("Assets/Data/Upgrades/UD_Wheelbarrow_T1_T2.asset");
        UpgradeData u2 = AssetDatabase.LoadAssetAtPath<UpgradeData>("Assets/Data/Upgrades/UD_Wheelbarrow_T2_T3.asset");

        // Configure controller
        SerializedObject so = new SerializedObject(controller);
        
        SerializedProperty levelDataProp = so.FindProperty("levelData");
        levelDataProp.ClearArray();
        if (w1 != null) { levelDataProp.InsertArrayElementAtIndex(0); levelDataProp.GetArrayElementAtIndex(0).objectReferenceValue = w1; }
        if (w2 != null) { levelDataProp.InsertArrayElementAtIndex(levelDataProp.arraySize); levelDataProp.GetArrayElementAtIndex(levelDataProp.arraySize - 1).objectReferenceValue = w2; }
        if (w3 != null) { levelDataProp.InsertArrayElementAtIndex(levelDataProp.arraySize); levelDataProp.GetArrayElementAtIndex(levelDataProp.arraySize - 1).objectReferenceValue = w3; }

        SerializedProperty upgradesProp = so.FindProperty("upgrades");
        upgradesProp.ClearArray();
        if (u1 != null) { upgradesProp.InsertArrayElementAtIndex(0); upgradesProp.GetArrayElementAtIndex(0).objectReferenceValue = u1; }
        if (u2 != null) { upgradesProp.InsertArrayElementAtIndex(upgradesProp.arraySize); upgradesProp.GetArrayElementAtIndex(upgradesProp.arraySize - 1).objectReferenceValue = u2; }

        so.FindProperty("followStrength").floatValue = 8f;
        so.ApplyModifiedProperties();

        // Save
        PrefabUtility.SaveAsPrefabAsset(root, path);
        DestroyImmediate(root);
    }

    private static void BuildLootPotionPrefab()
    {
        string path = "Assets/Prefabs/Resources/Loot_StrengthPotion.prefab";
        Debug.Log($"Building Loot_StrengthPotion Prefab: {path}");

        GameObject root = new GameObject("Loot_StrengthPotion");
        root.tag = "Loot";

        // Add SphereCollider as trigger
        SphereCollider col = root.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 1.0f;

        // Add LootPickupUI
        LootPickupUI pickupUI = root.AddComponent<LootPickupUI>();

        // Build Hierarchy
        // 1. PotionMesh
        GameObject potionSrc = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/JC_StylizedDungeons/Prefabs/Props/SM_Props_Potion_01.prefab");
        if (potionSrc != null)
        {
            GameObject potObj = PrefabUtility.InstantiatePrefab(potionSrc) as GameObject;
            potObj.name = "PotionMesh";
            potObj.transform.SetParent(root.transform, false);
            potObj.transform.localPosition = Vector3.zero;
            potObj.transform.localRotation = Quaternion.identity;
        }

        // Configure LootPickupUI
        SerializedObject so = new SerializedObject(pickupUI);
        so.FindProperty("searchRadius").floatValue = 2f;
        so.FindProperty("pickupText").stringValue = "[E] Al";
        so.FindProperty("verticalOffset").floatValue = 1.0f;
        so.ApplyModifiedProperties();

        // Save
        PrefabUtility.SaveAsPrefabAsset(root, path);
        DestroyImmediate(root);
    }

    private static RecipeCatalog GetOrCreateRecipeCatalog()
    {
        string path = "Assets/Data/Recipes/RecipeCatalog.asset";
        RecipeCatalog catalog = AssetDatabase.LoadAssetAtPath<RecipeCatalog>(path);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<RecipeCatalog>();
            AssetDatabase.CreateAsset(catalog, path);
        }

        // Find all RecipeData assets in the project
        string[] guids = AssetDatabase.FindAssets("t:RecipeData");
        List<RecipeData> recipesList = new List<RecipeData>();
        foreach (string guid in guids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            RecipeData recipe = AssetDatabase.LoadAssetAtPath<RecipeData>(assetPath);
            if (recipe != null)
            {
                recipesList.Add(recipe);
            }
        }

        SerializedObject so = new SerializedObject(catalog);
        SerializedProperty recipesProp = so.FindProperty("recipes");
        recipesProp.ClearArray();
        for (int i = 0; i < recipesList.Count; i++)
        {
            recipesProp.InsertArrayElementAtIndex(i);
            recipesProp.GetArrayElementAtIndex(i).objectReferenceValue = recipesList[i];
        }
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(catalog);
        return catalog;
    }

    private static UpgradeCatalog GetOrCreateUpgradeCatalog()
    {
        string path = "Assets/Data/Upgrades/UpgradeCatalog.asset";
        UpgradeCatalog catalog = AssetDatabase.LoadAssetAtPath<UpgradeCatalog>(path);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<UpgradeCatalog>();
            AssetDatabase.CreateAsset(catalog, path);
        }

        // Find all UpgradeData assets in the project
        string[] guids = AssetDatabase.FindAssets("t:UpgradeData");
        List<UpgradeData> upgradesList = new List<UpgradeData>();
        foreach (string guid in guids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            UpgradeData upgrade = AssetDatabase.LoadAssetAtPath<UpgradeData>(assetPath);
            if (upgrade != null)
            {
                upgradesList.Add(upgrade);
            }
        }

        SerializedObject so = new SerializedObject(catalog);
        SerializedProperty upgradesProp = so.FindProperty("upgrades");
        upgradesProp.ClearArray();
        for (int i = 0; i < upgradesList.Count; i++)
        {
            upgradesProp.InsertArrayElementAtIndex(i);
            upgradesProp.GetArrayElementAtIndex(i).objectReferenceValue = upgradesList[i];
        }
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(catalog);
        return catalog;
    }
}
