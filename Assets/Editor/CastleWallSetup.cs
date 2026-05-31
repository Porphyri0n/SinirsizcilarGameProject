using UnityEngine;
using UnityEditor;

/// <summary>
/// Editor aracı: GameScene'de Castle (3B) ve Wall (3C) objelerini
/// mevcut asset'leri kullanarak sahneye yerleştirir.
/// Kullanım: Unity menüsünden Tools → Sinirsizcilar → Setup Castle & Walls
/// </summary>
public class CastleWallSetup : EditorWindow
{
    [MenuItem("Tools/Sinirsizcilar/Setup Castle && Walls")]
    public static void SetupCastleAndWalls()
    {
        SetupCastleArea();
        SetupCastle();
        SetupWalls();

        EditorUtility.DisplayDialog("Tamamlandı!",
            "🏰 Castle ve 🧱 4 Wall başarıyla sahneye eklendi.\n\n" +
            "Castle → tag: Castle, CastleHealth component\n" +
            "Wall_North/East/West/South → tag: Defense, Wall component",
            "Tamam");
    }

    // ══════════════════════════════════════════════════════════════
    //  CASTLE AREA (Parent container)
    // ══════════════════════════════════════════════════════════════
    static GameObject SetupCastleArea()
    {
        var castleArea = GameObject.Find("CASTLE AREA");
        if (castleArea == null)
        {
            castleArea = new GameObject("CASTLE AREA");
            Undo.RegisterCreatedObjectUndo(castleArea, "Create CASTLE AREA");
            Debug.Log("[CastleWallSetup] Created CASTLE AREA parent.");
        }
        return castleArea;
    }

    // ══════════════════════════════════════════════════════════════
    //  3B. CASTLE (Ana Kale)
    // ══════════════════════════════════════════════════════════════
    static void SetupCastle()
    {
        var castleArea = GameObject.Find("CASTLE AREA");

        // Varsa sil
        var existing = GameObject.Find("Castle");
        if (existing != null)
        {
            Undo.DestroyObjectImmediate(existing);
            Debug.Log("[CastleWallSetup] Existing Castle removed.");
        }

        // ── Castle root ────────────────────────────────────────
        var castle = new GameObject("Castle");
        castle.tag = "Castle";
        castle.transform.SetParent(castleArea != null ? castleArea.transform : null);
        castle.transform.localPosition = Vector3.zero;
        Undo.RegisterCreatedObjectUndo(castle, "Create Castle");

        // ── CastleMesh — TSI_Stone_Platform_02A (ana yapı) ─────
        string platformPath = "Assets/ToonScapes/Spring Isles/Prefabs/Building Props/Stone Kit/TSI_Stone_Platform_02A.prefab";
        var platformPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(platformPath);

        if (platformPrefab != null)
        {
            var mesh = (GameObject)PrefabUtility.InstantiatePrefab(platformPrefab);
            mesh.name = "CastleMesh_Base";
            mesh.transform.SetParent(castle.transform);
            mesh.transform.localPosition = Vector3.zero;
            mesh.transform.localScale = new Vector3(3f, 3f, 3f);
            Undo.RegisterCreatedObjectUndo(mesh, "Create CastleMesh");
            Debug.Log("[CastleWallSetup] ✔ TSI_Stone_Platform_02A placed as CastleMesh_Base.");
        }
        else
        {
            // Fallback: Cube placeholder
            var mesh = GameObject.CreatePrimitive(PrimitiveType.Cube);
            mesh.name = "CastleMesh_Base";
            mesh.transform.SetParent(castle.transform);
            mesh.transform.localPosition = new Vector3(0f, 2.5f, 0f);
            mesh.transform.localScale = new Vector3(8f, 5f, 8f);
            Undo.RegisterCreatedObjectUndo(mesh, "Create CastleMesh Placeholder");
            Debug.LogWarning("[CastleWallSetup] TSI_Stone_Platform_02A not found, using Cube placeholder.");
        }

        // ── 4 Köşe Kule — TSI_Stone_Block_04A ──────────────────
        string blockPath = "Assets/ToonScapes/Spring Isles/Prefabs/Building Props/Stone Kit/TSI_Stone_Block_04A.prefab";
        var blockPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(blockPath);

        if (blockPrefab != null)
        {
            Vector3[] towerPos = {
                new Vector3(-5f, 0f, -5f),
                new Vector3(5f, 0f, -5f),
                new Vector3(-5f, 0f, 5f),
                new Vector3(5f, 0f, 5f)
            };
            string[] towerNames = { "CastleTower_SW", "CastleTower_SE", "CastleTower_NW", "CastleTower_NE" };

            for (int i = 0; i < towerPos.Length; i++)
            {
                var tower = (GameObject)PrefabUtility.InstantiatePrefab(blockPrefab);
                tower.name = towerNames[i];
                tower.transform.SetParent(castle.transform);
                tower.transform.localPosition = towerPos[i];
                tower.transform.localScale = new Vector3(2f, 4f, 2f);
                Undo.RegisterCreatedObjectUndo(tower, "Create " + towerNames[i]);
            }
            Debug.Log("[CastleWallSetup] ✔ 4 corner towers placed.");
        }

        // ── Kale Kapısı — TSI_Stone_Arch_01A ────────────────────
        string archPath = "Assets/ToonScapes/Spring Isles/Prefabs/Building Props/Stone Kit/TSI_Stone_Arch_01A.prefab";
        var archPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(archPath);

        if (archPrefab != null)
        {
            var gate = (GameObject)PrefabUtility.InstantiatePrefab(archPrefab);
            gate.name = "CastleGate";
            gate.transform.SetParent(castle.transform);
            gate.transform.localPosition = new Vector3(0f, 0f, -6f);
            gate.transform.localScale = new Vector3(1.5f, 1.5f, 1.5f);
            Undo.RegisterCreatedObjectUndo(gate, "Create CastleGate");
            Debug.Log("[CastleWallSetup] ✔ Stone Arch placed as CastleGate.");
        }

        // ── CastleHealth Component ──────────────────────────────
        castle.AddComponent<CastleHealth>();
        Debug.Log("[CastleWallSetup] ✔ CastleHealth component added (maxHealth: 1000).");

        // ── Box Collider ────────────────────────────────────────
        var col = castle.AddComponent<BoxCollider>();
        col.center = new Vector3(0f, 3f, 0f);
        col.size = new Vector3(12f, 6f, 12f);

        Debug.Log("[CastleWallSetup] ═══ 🏰 Castle setup complete! ═══");
    }

    // ══════════════════════════════════════════════════════════════
    //  3C. WALL (Kale Suru) × 4
    // ══════════════════════════════════════════════════════════════
    static void SetupWalls()
    {
        var castleArea = GameObject.Find("CASTLE AREA");

        // Walls parent
        var wallsParent = GameObject.Find("Walls");
        if (wallsParent != null)
        {
            Undo.DestroyObjectImmediate(wallsParent);
        }
        wallsParent = new GameObject("Walls");
        wallsParent.transform.SetParent(castleArea != null ? castleArea.transform : null);
        wallsParent.transform.localPosition = Vector3.zero;
        Undo.RegisterCreatedObjectUndo(wallsParent, "Create Walls parent");

        // ── Wall mesh prefab'larını yükle (JC_StylizedDungeons) ─
        // Stage 0 (sağlam): SM_Walls_01 — düz duvar
        // Stage 1 (çatlak): SM_Walls_04 — delikli/hasarlı duvar
        // Stage 2 (yıkık): SM_Walls_Part_01 — parçalanmış
        string stage0Path = "Assets/JC_StylizedDungeons/Prefabs/Walls/SM_Walls_01.prefab";
        string stage1Path = "Assets/JC_StylizedDungeons/Prefabs/Walls/SM_Walls_04.prefab";
        string stage2Path = "Assets/JC_StylizedDungeons/Prefabs/Walls/SM_Walls_Part_01.prefab";

        var stage0Prefab = AssetDatabase.LoadAssetAtPath<GameObject>(stage0Path);
        var stage1Prefab = AssetDatabase.LoadAssetAtPath<GameObject>(stage1Path);
        var stage2Prefab = AssetDatabase.LoadAssetAtPath<GameObject>(stage2Path);

        bool hasMeshes = (stage0Prefab != null);
        if (!hasMeshes)
        {
            Debug.LogWarning("[CastleWallSetup] JC_StylizedDungeons wall prefabs not found. Using cube placeholders.");
        }

        // ── 4 yöne wall yerleştir ───────────────────────────────
        // Kale merkezi (0,0,0) etrafında surlar
        WallConfig[] walls = {
            new WallConfig("Wall_North", new Vector3(0f, 0f, 15f),  0f,   new Vector3(20f, 1f, 1f)),
            new WallConfig("Wall_South", new Vector3(0f, 0f, -15f), 180f, new Vector3(20f, 1f, 1f)),
            new WallConfig("Wall_East",  new Vector3(15f, 0f, 0f),  90f,  new Vector3(20f, 1f, 1f)),
            new WallConfig("Wall_West",  new Vector3(-15f, 0f, 0f), -90f, new Vector3(20f, 1f, 1f))
        };

        foreach (var wc in walls)
        {
            CreateWall(wc, wallsParent.transform, stage0Prefab, stage1Prefab, stage2Prefab, hasMeshes);
        }

        Debug.Log("[CastleWallSetup] ═══ 🧱 4 Walls setup complete! ═══");
    }

    static void CreateWall(WallConfig config, Transform parent,
        GameObject stage0Prefab, GameObject stage1Prefab, GameObject stage2Prefab,
        bool hasMeshes)
    {
        // ── Wall root ───────────────────────────────────────────
        var wallObj = new GameObject(config.name);
        wallObj.tag = "Defense";
        wallObj.transform.SetParent(parent);
        wallObj.transform.localPosition = config.position;
        wallObj.transform.localRotation = Quaternion.Euler(0f, config.yRotation, 0f);
        Undo.RegisterCreatedObjectUndo(wallObj, "Create " + config.name);

        // ── Damage Stage meshes ─────────────────────────────────
        GameObject s0, s1, s2;

        if (hasMeshes && stage0Prefab != null)
        {
            // Stage 0 — Sağlam
            s0 = (GameObject)PrefabUtility.InstantiatePrefab(stage0Prefab);
            s0.name = "Wall_Stage0";
            s0.transform.SetParent(wallObj.transform);
            s0.transform.localPosition = Vector3.zero;
            s0.transform.localScale = config.meshScale;
            s0.SetActive(true);
            Undo.RegisterCreatedObjectUndo(s0, "Create Wall_Stage0");
        }
        else
        {
            s0 = CreatePlaceholderWall("Wall_Stage0", wallObj.transform, Color.gray, config);
            s0.SetActive(true);
        }

        if (hasMeshes && stage1Prefab != null)
        {
            // Stage 1 — Çatlak
            s1 = (GameObject)PrefabUtility.InstantiatePrefab(stage1Prefab);
            s1.name = "Wall_Stage1";
            s1.transform.SetParent(wallObj.transform);
            s1.transform.localPosition = Vector3.zero;
            s1.transform.localScale = config.meshScale;
            s1.SetActive(false);
            Undo.RegisterCreatedObjectUndo(s1, "Create Wall_Stage1");
        }
        else
        {
            s1 = CreatePlaceholderWall("Wall_Stage1", wallObj.transform, Color.yellow, config);
            s1.SetActive(false);
        }

        if (hasMeshes && stage2Prefab != null)
        {
            // Stage 2 — Yıkık
            s2 = (GameObject)PrefabUtility.InstantiatePrefab(stage2Prefab);
            s2.name = "Wall_Stage2";
            s2.transform.SetParent(wallObj.transform);
            s2.transform.localPosition = Vector3.zero;
            s2.transform.localScale = config.meshScale;
            s2.SetActive(false);
            Undo.RegisterCreatedObjectUndo(s2, "Create Wall_Stage2");
        }
        else
        {
            s2 = CreatePlaceholderWall("Wall_Stage2", wallObj.transform, Color.red, config);
            s2.SetActive(false);
        }

        // ── DestroyedEffect (partikül placeholder) ──────────────
        var fx = new GameObject("DestroyedEffect");
        fx.transform.SetParent(wallObj.transform);
        fx.transform.localPosition = Vector3.zero;
        var ps = fx.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.startColor = new Color(0.6f, 0.5f, 0.3f, 1f); // toz rengi
        main.startSize = 0.3f;
        main.startLifetime = 1.5f;
        main.maxParticles = 30;
        main.playOnAwake = false;
        var emission = ps.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 20) });
        fx.SetActive(false);
        Undo.RegisterCreatedObjectUndo(fx, "Create DestroyedEffect");

        // ── BoxCollider ─────────────────────────────────────────
        var col = wallObj.AddComponent<BoxCollider>();
        col.center = new Vector3(0f, 2f, 0f);
        col.size = new Vector3(20f, 4f, 1.5f);

        // ── Wall Component ──────────────────────────────────────
        var wallScript = wallObj.AddComponent<Wall>();

        // Wall script'ine SerializedObject ile değer ata
        var so = new SerializedObject(wallScript);

        // maxHealth = 500
        var maxHp = so.FindProperty("maxHealth");
        if (maxHp != null) maxHp.floatValue = 500f;

        // damageStages array
        var stages = so.FindProperty("damageStages");
        if (stages != null)
        {
            stages.arraySize = 3;
            stages.GetArrayElementAtIndex(0).objectReferenceValue = s0;
            stages.GetArrayElementAtIndex(1).objectReferenceValue = s1;
            stages.GetArrayElementAtIndex(2).objectReferenceValue = s2;
        }

        // stageThresholds
        var thresholds = so.FindProperty("stageThresholds");
        if (thresholds != null)
        {
            thresholds.arraySize = 2;
            thresholds.GetArrayElementAtIndex(0).floatValue = 0.5f;
            thresholds.GetArrayElementAtIndex(1).floatValue = 0.25f;
        }

        // destroyedEffect
        var desFx = so.FindProperty("destroyedEffect");
        if (desFx != null) desFx.objectReferenceValue = fx;

        so.ApplyModifiedProperties();

        Debug.Log("[CastleWallSetup] ✔ " + config.name + " created at " + config.position);
    }

    static GameObject CreatePlaceholderWall(string name, Transform parent, Color color, WallConfig config)
    {
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = name;
        cube.transform.SetParent(parent);
        cube.transform.localPosition = new Vector3(0f, 2f, 0f);
        cube.transform.localScale = new Vector3(20f, 4f, 1f);

        var renderer = cube.GetComponent<Renderer>();
        if (renderer != null)
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = color;
            renderer.sharedMaterial = mat;
        }
        Undo.RegisterCreatedObjectUndo(cube, "Create " + name);
        return cube;
    }

    struct WallConfig
    {
        public string name;
        public Vector3 position;
        public float yRotation;
        public Vector3 meshScale;

        public WallConfig(string name, Vector3 pos, float yRot, Vector3 scale)
        {
            this.name = name;
            this.position = pos;
            this.yRotation = yRot;
            this.meshScale = scale;
        }
    }
}
