#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 생성된 스프라이트를 씬/프리팹의 SpriteRenderer에 일괄 할당한다.
/// 메뉴: MonsterKitchen → Assign All Sprites
/// </summary>
public static class SpriteAssigner
{
    // ── 스프라이트 경로 ──────────────────────────────────────────────
    const string PLAYER   = "Assets/Sprites/Char/Player.png";
    const string SLIME    = "Assets/Sprites/Char/Slime.png";
    const string CUSTOMER = "Assets/Sprites/Char/Customer.png";
    const string PORTAL   = "Assets/Sprites/Env/Portal.png";
    const string DUNGEON_EXIT  = "Assets/Sprites/Env/DungeonExit.png";
    const string COOK_STATION  = "Assets/Sprites/Env/CookStation.png";
    const string EVENING_SIGN  = "Assets/Sprites/Env/EveningSign.png";
    const string FARM_PLOT     = "Assets/Sprites/Env/FarmPlot.png";
    const string TOOL_STATION  = "Assets/Sprites/Env/ToolStation.png";
    const string SHOP_STATION  = "Assets/Sprites/Env/ShopStation.png";
    const string TABLE         = "Assets/Sprites/Env/Table.png";
    const string ITEM_DROP     = "Assets/Sprites/Env/ItemDrop.png";

    // ── 씬 경로 ─────────────────────────────────────────────────────
    const string SCENE_MANAGEMENT = "Assets/Scenes/ManagementScene.unity";
    const string SCENE_DUNGEON    = "Assets/Scenes/DungeonScene.unity";
    const string SCENE_KITCHEN    = "Assets/Scenes/KitchenScene.unity";
    const string SCENE_RESTAURANT = "Assets/Scenes/RestaurantScene.unity";

    // ── 씬별 (GameObjectName → SpritePath) 매핑 ─────────────────────
    static readonly Dictionary<string, (string scene, string go, string sprite)[]> SceneMaps
        = new()
    {
        {
            SCENE_MANAGEMENT, new[]
            {
                (SCENE_MANAGEMENT, "Player",               PLAYER),
                (SCENE_MANAGEMENT, "FarmPlot_01",          FARM_PLOT),
                (SCENE_MANAGEMENT, "FarmPlot_02",          FARM_PLOT),
                (SCENE_MANAGEMENT, "DungeonPortal",        PORTAL),
                (SCENE_MANAGEMENT, "EveningStarter",       EVENING_SIGN),
                (SCENE_MANAGEMENT, "ToolUpgrade_Damage",   TOOL_STATION),
                (SCENE_MANAGEMENT, "ToolUpgrade_Range",    TOOL_STATION),
                (SCENE_MANAGEMENT, "ToolUpgrade_Cooldown", TOOL_STATION),
                (SCENE_MANAGEMENT, "ShopUpgrade_Tip",      SHOP_STATION),
                (SCENE_MANAGEMENT, "ShopUpgrade_Seats",    SHOP_STATION),
            }
        },
        {
            SCENE_DUNGEON, new[]
            {
                (SCENE_DUNGEON, "Player",      PLAYER),
                (SCENE_DUNGEON, "DungeonExit", DUNGEON_EXIT),
            }
        },
        {
            SCENE_KITCHEN, new[]
            {
                (SCENE_KITCHEN, "Player",         PLAYER),
                (SCENE_KITCHEN, "CookingStation", COOK_STATION),
            }
        },
        {
            SCENE_RESTAURANT, new[]
            {
                (SCENE_RESTAURANT, "Player",         PLAYER),
                (SCENE_RESTAURANT, "Table_01",       TABLE),
                (SCENE_RESTAURANT, "CookingStation", COOK_STATION),
            }
        },
    };

    [MenuItem("MonsterKitchen/Assign All Sprites")]
    public static void AssignAll()
    {
        // 미리 현재 씬 저장
        if (EditorSceneManager.GetActiveScene().isDirty)
            EditorSceneManager.SaveOpenScenes();

        string originalScene = EditorSceneManager.GetActiveScene().path;
        int total = 0;

        try
        {
            EditorUtility.DisplayProgressBar("Assigning Sprites", "스프라이트 임포트 모드 수정 중...", 0f);

            // Step 1: 모든 Char/Env 스프라이트를 Single 모드로 고정
            FixImportModes();

            // Step 2: 프리팹 처리
            EditorUtility.DisplayProgressBar("Assigning Sprites", "프리팹 처리 중...", 0.2f);
            total += AssignToPrefab("Assets/Prefabs/Enemies/Slime.prefab",       SLIME);
            total += AssignToPrefab("Assets/Prefabs/Restaurant/Customer.prefab", CUSTOMER);
            total += AssignToPrefab("Assets/Prefabs/Items/ItemDrop.prefab",      ITEM_DROP);

            // Step 3: 씬 처리
            int sceneIndex = 0;
            foreach (var kv in SceneMaps)
            {
                float prog = 0.4f + 0.6f * (sceneIndex / (float)SceneMaps.Count);
                EditorUtility.DisplayProgressBar("Assigning Sprites",
                    $"씬 처리 중: {System.IO.Path.GetFileNameWithoutExtension(kv.Key)}...", prog);

                total += ProcessScene(kv.Key, kv.Value);
                sceneIndex++;
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();

            // 원래 씬으로 복귀
            if (!string.IsNullOrEmpty(originalScene))
                EditorSceneManager.OpenScene(originalScene, OpenSceneMode.Single);
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[SpriteAssigner] 완료 — 총 {total}개 SpriteRenderer 할당.");
        EditorUtility.DisplayDialog("Assign All Sprites",
            $"완료!\n총 {total}개 SpriteRenderer에 스프라이트 할당.", "OK");
    }

    // ────────────────────────────────────────────────────────────────
    //  Step 1: 임포트 모드 수정 (Multiple → Single)
    // ────────────────────────────────────────────────────────────────
    static readonly string[] SingleSpriteAssets =
    {
        PLAYER, SLIME, CUSTOMER,
        PORTAL, DUNGEON_EXIT, COOK_STATION, EVENING_SIGN,
        FARM_PLOT, TOOL_STATION, SHOP_STATION, TABLE, ITEM_DROP,
    };

    static void FixImportModes()
    {
        foreach (var path in SingleSpriteAssets)
        {
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp == null) continue;
            if (imp.spriteImportMode != SpriteImportMode.Single)
            {
                imp.spriteImportMode = SpriteImportMode.Single;
                imp.SaveAndReimport();
                Debug.Log($"[SpriteAssigner] Import mode fixed: {path}");
            }
        }
    }

    // ────────────────────────────────────────────────────────────────
    //  Step 2: 프리팹
    // ────────────────────────────────────────────────────────────────
    static int AssignToPrefab(string prefabPath, string spritePath)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
        {
            Debug.LogWarning($"[SpriteAssigner] 프리팹 없음: {prefabPath}");
            return 0;
        }

        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
        if (sprite == null)
        {
            Debug.LogWarning($"[SpriteAssigner] 스프라이트 없음: {spritePath}");
            return 0;
        }

        // 프리팹 루트 또는 자식에서 SpriteRenderer 검색
        var sr = prefab.GetComponent<SpriteRenderer>()
                 ?? prefab.GetComponentInChildren<SpriteRenderer>(true);
        if (sr == null)
        {
            // SpriteRenderer 없으면 자동 추가
            using var scope = new PrefabUtility.EditPrefabContentsScope(prefabPath);
            var root  = scope.prefabContentsRoot;
            sr        = root.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            Debug.Log($"[SpriteAssigner] SpriteRenderer 추가 후 할당: {prefabPath}");
            return 1;
        }

        sr.sprite = sprite;
        EditorUtility.SetDirty(prefab);
        PrefabUtility.SavePrefabAsset(prefab);
        Debug.Log($"[SpriteAssigner] {System.IO.Path.GetFileName(prefabPath)} → {System.IO.Path.GetFileName(spritePath)}");
        return 1;
    }

    // ────────────────────────────────────────────────────────────────
    //  Step 3: 씬
    // ────────────────────────────────────────────────────────────────
    static int ProcessScene(string scenePath, (string scene, string go, string sprite)[] assignments)
    {
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        int count = 0;

        foreach (var (_, goName, spritePath) in assignments)
        {
            var go = FindInScene(scene, goName);
            if (go == null)
            {
                Debug.LogWarning($"[SpriteAssigner] GO 없음: {goName} ({System.IO.Path.GetFileNameWithoutExtension(scenePath)})");
                continue;
            }

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            if (sprite == null)
            {
                Debug.LogWarning($"[SpriteAssigner] 스프라이트 없음: {spritePath}");
                continue;
            }

            // SpriteRenderer 없으면 자동 추가
            var sr = go.GetComponent<SpriteRenderer>();
            if (sr == null)
                sr = go.AddComponent<SpriteRenderer>();

            sr.sprite = sprite;
            EditorUtility.SetDirty(go);
            Debug.Log($"[SpriteAssigner] [{System.IO.Path.GetFileNameWithoutExtension(scenePath)}] {goName} → {System.IO.Path.GetFileName(spritePath)}");
            count++;
        }

        if (count > 0)
            EditorSceneManager.SaveScene(scene);

        return count;
    }

    // ────────────────────────────────────────────────────────────────
    //  Util: 씬에서 이름으로 GameObject 탐색 (루트 + 자식 포함)
    // ────────────────────────────────────────────────────────────────
    static GameObject FindInScene(Scene scene, string name)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.name == name) return root;
            var found = FindInChildren(root.transform, name);
            if (found != null) return found;
        }
        return null;
    }

    static GameObject FindInChildren(Transform t, string name)
    {
        foreach (Transform child in t)
        {
            if (child.name == name) return child.gameObject;
            var found = FindInChildren(child, name);
            if (found != null) return found;
        }
        return null;
    }
}
#endif
