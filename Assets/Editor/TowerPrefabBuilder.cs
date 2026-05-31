using System.IO;
using UnityEditor;
using UnityEngine;
using Unity.Netcode;

public class TowerPrefabBuilder : EditorWindow
{
    [MenuItem("Tools/Build Tower Prefabs")]
    public static void BuildPrefabs()
    {
        Debug.Log("Starting Tower Prefabs Build Process...");

        // Ensure directories exist
        CreateDirectoryIfNotExists("Assets/Prefabs/Weapons");
        CreateDirectoryIfNotExists("Assets/Prefabs/Defenses");

        // 1. Build Arrow Projectile Prefab
        GameObject arrowPrefab = BuildArrowPrefab();
        
        // 2. Build Cannonball Projectile Prefab
        GameObject cannonballPrefab = BuildCannonballPrefab();

        // 3. Build ArcherTower Prefab
        BuildArcherTowerPrefab(arrowPrefab);

        // 4. Build CannonTower Prefab
        BuildCannonTowerPrefab(cannonballPrefab);

        Debug.Log("Tower Prefabs Build Process Completed Successfully!");
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

    private static GameObject BuildArrowPrefab()
    {
        string path = "Assets/Prefabs/Weapons/Arrow.prefab";
        Debug.Log($"Building Arrow Prefab: {path}");

        GameObject root = new GameObject("Arrow");
        root.layer = LayerMask.NameToLayer("Default");

        // Add Rigidbody
        Rigidbody rb = root.AddComponent<Rigidbody>();
        rb.useGravity = false;
        rb.isKinematic = false;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.linearDamping = 0f;
        rb.angularDamping = 0f;

        // Add CapsuleCollider aligned with the arrow shape
        CapsuleCollider col = root.AddComponent<CapsuleCollider>();
        col.isTrigger = true;
        col.direction = 2; // Z-Axis alignment
        col.radius = 0.05f;
        col.height = 0.8f;
        col.center = new Vector3(0, 0, 0);

        // Add Projectile script
        Projectile proj = root.AddComponent<Projectile>();
        // Using Reflection or SerializedObject to set private fields if needed, 
        // but lifetime and destroyOnHit are set to defaults of 6f and true.
        // We'll set them explicitly using SerializedObject to be safe.
        SerializedObject so = new SerializedObject(proj);
        so.FindProperty("lifetime").floatValue = 6f;
        so.FindProperty("destroyOnHit").boolValue = true;
        so.ApplyModifiedProperties();

        // Add Visual Mesh
        GameObject arrowMeshSrc = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ModularHeroBundlePolyart/ModularRPGHeroesPolyArt/Prefabs/Weapons/Arrow01.prefab");
        if (arrowMeshSrc != null)
        {
            GameObject visual = PrefabUtility.InstantiatePrefab(arrowMeshSrc) as GameObject;
            visual.name = "Visual";
            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one;
        }
        else
        {
            Debug.LogWarning("Arrow01 prefab not found, making a simple cylinder visual instead.");
            GameObject cyl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cyl.name = "Visual";
            DestroyImmediate(cyl.GetComponent<Collider>());
            cyl.transform.SetParent(root.transform, false);
            cyl.transform.localPosition = Vector3.zero;
            cyl.transform.localRotation = Quaternion.Euler(90f, 0, 0);
            cyl.transform.localScale = new Vector3(0.04f, 0.4f, 0.04f);
        }

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        DestroyImmediate(root);
        return prefab;
    }

    private static GameObject BuildCannonballPrefab()
    {
        string path = "Assets/Prefabs/Weapons/Cannonball.prefab";
        Debug.Log($"Building Cannonball Prefab: {path}");

        // Create Sphere primitive so it comes with MeshFilter, MeshRenderer, and SphereCollider
        GameObject root = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        root.name = "Cannonball";
        root.layer = LayerMask.NameToLayer("Default");

        // Scale down to a reasonable size
        root.transform.localScale = new Vector3(0.6f, 0.6f, 0.6f);

        // Configure Collider
        SphereCollider col = root.GetComponent<SphereCollider>();
        col.isTrigger = true;

        // Configure Rigidbody
        Rigidbody rb = root.AddComponent<Rigidbody>();
        rb.useGravity = true;
        rb.isKinematic = false;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        // Configure Projectile
        Projectile proj = root.AddComponent<Projectile>();
        SerializedObject so = new SerializedObject(proj);
        so.FindProperty("lifetime").floatValue = 6f;
        so.FindProperty("destroyOnHit").boolValue = true;
        so.ApplyModifiedProperties();

        // Apply dark metallic material if available
        Material metalMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Raygeas/Suntail Village/Assets/Materials/Building Modules/Metal.mat");
        if (metalMat != null)
        {
            root.GetComponent<MeshRenderer>().sharedMaterial = metalMat;
        }

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        DestroyImmediate(root);
        return prefab;
    }

    private static void BuildArcherTowerPrefab(GameObject arrowPrefab)
    {
        string path = "Assets/Prefabs/Defenses/ArcherTower.prefab";
        Debug.Log($"Building ArcherTower Prefab: {path}");

        GameObject root = new GameObject("ArcherTower");
        root.tag = "Defense";

        // Add Netcode component
        root.AddComponent<NetworkObject>();

        // Add Tower components
        ArcherTower controller = root.AddComponent<ArcherTower>();
        TowerAiming aiming = root.AddComponent<TowerAiming>();
        TowerCameraRig cameraRig = root.AddComponent<TowerCameraRig>();
        TowerNetSync netSync = root.AddComponent<TowerNetSync>();
        TowerUpgrade upgrade = root.AddComponent<TowerUpgrade>();

        // Add BoxCollider for trigger interaction
        BoxCollider boxCol = root.AddComponent<BoxCollider>();
        boxCol.isTrigger = true;
        boxCol.center = new Vector3(0, 1.2f, 0);
        boxCol.size = new Vector3(2.5f, 2.5f, 2.5f);

        // Build Hierarchy
        // 1. TowerBase
        GameObject stoneBasePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Raygeas/Suntail Village/Assets/Prefabs/Building Modules/Stone Modules/Stone_Base.prefab");
        if (stoneBasePrefab != null)
        {
            GameObject baseObj = PrefabUtility.InstantiatePrefab(stoneBasePrefab) as GameObject;
            baseObj.name = "TowerBase";
            baseObj.transform.SetParent(root.transform, false);
            baseObj.transform.localPosition = Vector3.zero;
            baseObj.transform.localRotation = Quaternion.identity;
        }

        // 2. AimPivot
        GameObject aimPivotObj = new GameObject("AimPivot");
        aimPivotObj.transform.SetParent(root.transform, false);
        aimPivotObj.transform.localPosition = new Vector3(0, 2.8f, 0);

        // 2a. BowMesh inside AimPivot
        GameObject bowPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ModularHeroBundlePolyart/ModularRPGHeroesPolyArt/Prefabs/Weapons/Bow01.prefab");
        if (bowPrefab != null)
        {
            GameObject bowObj = PrefabUtility.InstantiatePrefab(bowPrefab) as GameObject;
            bowObj.name = "BowMesh";
            bowObj.transform.SetParent(aimPivotObj.transform, false);
            bowObj.transform.localPosition = Vector3.zero;
            bowObj.transform.localRotation = Quaternion.identity;
        }

        // 2b. Muzzle inside AimPivot
        GameObject muzzleObj = new GameObject("Muzzle");
        muzzleObj.transform.SetParent(aimPivotObj.transform, false);
        muzzleObj.transform.localPosition = new Vector3(0, 0, 0.8f);

        // 3. FPV Camera
        GameObject camObj = new GameObject("TowerCamera");
        camObj.transform.SetParent(root.transform, false);
        camObj.transform.localPosition = new Vector3(0, 3.5f, -1.2f);
        camObj.transform.localRotation = Quaternion.Euler(15f, 0, 0);
        Camera cam = camObj.AddComponent<Camera>();
        cam.enabled = false;

        // 4. ExitPoint
        GameObject exitObj = new GameObject("ExitPoint");
        exitObj.transform.SetParent(root.transform, false);
        exitObj.transform.localPosition = new Vector3(0, 0.5f, -2.5f);

        // Load DefenseData
        DefenseData defData = AssetDatabase.LoadAssetAtPath<DefenseData>("Assets/Data/Defenses/DD_ArcherTower.asset");

        // Load UpgradeData
        UpgradeData u1 = AssetDatabase.LoadAssetAtPath<UpgradeData>("Assets/Data/Upgrades/UD_ArcherTower_T1_T2.asset");
        UpgradeData u2 = AssetDatabase.LoadAssetAtPath<UpgradeData>("Assets/Data/Upgrades/UD_ArcherTower_T2_T3.asset");

        // Configure Controller Serialized Fields
        SerializedObject soController = new SerializedObject(controller);
        soController.FindProperty("data").objectReferenceValue = defData;
        soController.FindProperty("aimPivot").objectReferenceValue = aimPivotObj.transform;
        soController.FindProperty("cameraRig").objectReferenceValue = cameraRig;
        soController.FindProperty("exitPoint").objectReferenceValue = exitObj.transform;
        soController.FindProperty("projectilePrefab").objectReferenceValue = arrowPrefab;
        soController.FindProperty("muzzle").objectReferenceValue = muzzleObj.transform;
        soController.FindProperty("projectileSpeed").floatValue = 35f;
        soController.ApplyModifiedProperties();

        // Configure Aiming Serialized Fields
        SerializedObject soAiming = new SerializedObject(aiming);
        soAiming.FindProperty("tower").objectReferenceValue = controller;
        soAiming.FindProperty("towerCamera").objectReferenceValue = cam;
        soAiming.FindProperty("aimPivot").objectReferenceValue = aimPivotObj.transform;
        soAiming.FindProperty("maxAimDistance").floatValue = 50f;
        soAiming.ApplyModifiedProperties();

        // Configure CameraRig Serialized Fields
        SerializedObject soCameraRig = new SerializedObject(cameraRig);
        soCameraRig.FindProperty("towerCamera").objectReferenceValue = cam;
        soCameraRig.ApplyModifiedProperties();

        // Configure NetSync Serialized Fields
        SerializedObject soNetSync = new SerializedObject(netSync);
        soNetSync.FindProperty("controller").objectReferenceValue = controller;
        soNetSync.FindProperty("upgrade").objectReferenceValue = upgrade;
        soNetSync.ApplyModifiedProperties();

        // Configure Upgrade Serialized Fields
        SerializedObject soUpgrade = new SerializedObject(upgrade);
        SerializedProperty upgradesProp = soUpgrade.FindProperty("upgrades");
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
        soUpgrade.ApplyModifiedProperties();

        // Save Prefab
        PrefabUtility.SaveAsPrefabAsset(root, path);
        DestroyImmediate(root);
    }

    private static void BuildCannonTowerPrefab(GameObject cannonballPrefab)
    {
        string path = "Assets/Prefabs/Defenses/CannonTower.prefab";
        Debug.Log($"Building CannonTower Prefab: {path}");

        GameObject root = new GameObject("CannonTower");
        root.tag = "Defense";

        // Add Netcode component
        root.AddComponent<NetworkObject>();

        // Add Tower components
        CannonTower controller = root.AddComponent<CannonTower>();
        TowerAiming aiming = root.AddComponent<TowerAiming>();
        TowerCameraRig cameraRig = root.AddComponent<TowerCameraRig>();
        TowerNetSync netSync = root.AddComponent<TowerNetSync>();
        TowerUpgrade upgrade = root.AddComponent<TowerUpgrade>();

        // Add BoxCollider for trigger interaction
        BoxCollider boxCol = root.AddComponent<BoxCollider>();
        boxCol.isTrigger = true;
        boxCol.center = new Vector3(0, 1.2f, 0);
        boxCol.size = new Vector3(2.5f, 2.5f, 2.5f);

        // Build Hierarchy
        // 1. TowerBase
        GameObject stoneBasePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Raygeas/Suntail Village/Assets/Prefabs/Building Modules/Stone Modules/Stone_Base.prefab");
        if (stoneBasePrefab != null)
        {
            GameObject baseObj = PrefabUtility.InstantiatePrefab(stoneBasePrefab) as GameObject;
            baseObj.name = "TowerBase";
            baseObj.transform.SetParent(root.transform, false);
            baseObj.transform.localPosition = Vector3.zero;
            baseObj.transform.localRotation = Quaternion.identity;
        }

        // 2. AimPivot
        GameObject aimPivotObj = new GameObject("AimPivot");
        aimPivotObj.transform.SetParent(root.transform, false);
        aimPivotObj.transform.localPosition = new Vector3(0, 2.8f, 0);

        // 2a. CannonMesh inside AimPivot (represented by a stylized barrel)
        GameObject barrelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/JC_StylizedDungeons/Prefabs/Props/SM_Props_Barrel_01.prefab");
        if (barrelPrefab != null)
        {
            GameObject barrelObj = PrefabUtility.InstantiatePrefab(barrelPrefab) as GameObject;
            barrelObj.name = "CannonMesh";
            barrelObj.transform.SetParent(aimPivotObj.transform, false);
            barrelObj.transform.localPosition = Vector3.zero;
            barrelObj.transform.localRotation = Quaternion.Euler(90f, 0, 0); // barrel pointing forward
        }

        // 2b. Muzzle inside AimPivot
        GameObject muzzleObj = new GameObject("Muzzle");
        muzzleObj.transform.SetParent(aimPivotObj.transform, false);
        muzzleObj.transform.localPosition = new Vector3(0, 0, 1.2f);

        // 3. FPV Camera
        GameObject camObj = new GameObject("TowerCamera");
        camObj.transform.SetParent(root.transform, false);
        camObj.transform.localPosition = new Vector3(0, 3.5f, -1.2f);
        camObj.transform.localRotation = Quaternion.Euler(20f, 0, 0);
        Camera cam = camObj.AddComponent<Camera>();
        cam.enabled = false;

        // 4. ExitPoint
        GameObject exitObj = new GameObject("ExitPoint");
        exitObj.transform.SetParent(root.transform, false);
        exitObj.transform.localPosition = new Vector3(0, 0.5f, -2.5f);

        // Load DefenseData
        DefenseData defData = AssetDatabase.LoadAssetAtPath<DefenseData>("Assets/Data/Defenses/DD_CannonTower.asset");

        // Load UpgradeData
        UpgradeData u1 = AssetDatabase.LoadAssetAtPath<UpgradeData>("Assets/Data/Upgrades/UD_CannonTower_T1_T2.asset");
        UpgradeData u2 = AssetDatabase.LoadAssetAtPath<UpgradeData>("Assets/Data/Upgrades/UD_CannonTower_T2_T3.asset");

        // Configure Controller Serialized Fields
        SerializedObject soController = new SerializedObject(controller);
        soController.FindProperty("data").objectReferenceValue = defData;
        soController.FindProperty("aimPivot").objectReferenceValue = aimPivotObj.transform;
        soController.FindProperty("cameraRig").objectReferenceValue = cameraRig;
        soController.FindProperty("exitPoint").objectReferenceValue = exitObj.transform;
        soController.FindProperty("projectilePrefab").objectReferenceValue = cannonballPrefab;
        soController.FindProperty("muzzle").objectReferenceValue = muzzleObj.transform;
        soController.FindProperty("projectileSpeed").floatValue = 25f;
        soController.ApplyModifiedProperties();

        // Configure Aiming Serialized Fields
        SerializedObject soAiming = new SerializedObject(aiming);
        soAiming.FindProperty("tower").objectReferenceValue = controller;
        soAiming.FindProperty("towerCamera").objectReferenceValue = cam;
        soAiming.FindProperty("aimPivot").objectReferenceValue = aimPivotObj.transform;
        soAiming.FindProperty("maxAimDistance").floatValue = 50f;
        soAiming.ApplyModifiedProperties();

        // Configure CameraRig Serialized Fields
        SerializedObject soCameraRig = new SerializedObject(cameraRig);
        soCameraRig.FindProperty("towerCamera").objectReferenceValue = cam;
        soCameraRig.ApplyModifiedProperties();

        // Configure NetSync Serialized Fields
        SerializedObject soNetSync = new SerializedObject(netSync);
        soNetSync.FindProperty("controller").objectReferenceValue = controller;
        soNetSync.FindProperty("upgrade").objectReferenceValue = upgrade;
        soNetSync.ApplyModifiedProperties();

        // Configure Upgrade Serialized Fields
        SerializedObject soUpgrade = new SerializedObject(upgrade);
        SerializedProperty upgradesProp = soUpgrade.FindProperty("upgrades");
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
        soUpgrade.ApplyModifiedProperties();

        // Save Prefab
        PrefabUtility.SaveAsPrefabAsset(root, path);
        DestroyImmediate(root);
    }
}
