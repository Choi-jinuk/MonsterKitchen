// D-01+D-02 Dungeon Tilemap Setup
using UnityEngine;
// rev2
using UnityEditor;
using UnityEngine.Tilemaps;

/// <summary>
/// DungeonScene에 2방 타일맵 던전(D-01, D-02)을 세팅하는 일회성 Editor 스크립트.
/// 메뉴: MonsterKitchen → Setup Dungeon Tilemap
///
/// 레이아웃:
///   Room1 (20x12, 중앙) --- 복도(4x3) --- Room2 (20x12, 오른쪽)
///   x: -10~9             x: 10~13         x: 14~33
///   y: -6~5              y: -1~1          y: -6~5
/// </summary>
public static class DungeonTilemapSetup
{
    [MenuItem("MonsterKitchen/Setup Dungeon Tilemap")]
    public static void Run()
    {
        DisableOldWalls();

        var floorTile = CreateColorTile("FloorTile", new Color(0.25f, 0.45f, 0.20f));
        var wallTile  = CreateColorTile("WallTile",  new Color(0.30f, 0.22f, 0.15f));

        var grid          = CreateOrFindGrid("DungeonGrid");
        var groundTilemap = CreateOrFindTilemap(grid, "Ground", 0);
        var wallTilemap   = CreateOrFindTilemap(grid, "Walls",  1);

        PaintDungeon(groundTilemap, wallTilemap, floorTile, wallTile);
        SetupWallCollider(wallTilemap.gameObject);

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        Debug.Log("[DungeonTilemapSetup] 완료! 2방 던전 타일맵 + Collider 세팅됨.");
    }

    // -----------------------------------------------------------------------
    private static void DisableOldWalls()
    {
        foreach (var n in new[] { "Wall_Top", "Wall_Bottom", "Wall_Left", "Wall_Right" })
        {
            var go = GameObject.Find(n);
            if (go != null) { go.SetActive(false); Debug.Log($"[DungeonTilemapSetup] 기존 벽 비활성화: {n}"); }
        }
    }

    // -----------------------------------------------------------------------
    // 솔리드 컬러 Tile 에셋 생성
    // 스프라이트를 Tile 에셋의 서브에셋으로 등록해 씬 리로드 후에도 유지되도록 한다.
    // -----------------------------------------------------------------------
    private static Tile CreateColorTile(string tileName, Color color)
    {
        const string dir = "Assets/Tilemaps/Tiles";
        if (!AssetDatabase.IsValidFolder("Assets/Tilemaps"))
            AssetDatabase.CreateFolder("Assets", "Tilemaps");
        if (!AssetDatabase.IsValidFolder(dir))
            AssetDatabase.CreateFolder("Assets/Tilemaps", "Tiles");

        string tilePath = $"{dir}/{tileName}.asset";
        string texPath  = $"{dir}/{tileName}_Tex.asset";

        // 이미 스프라이트가 유효한 타일이 있으면 재사용
        var existingTile = AssetDatabase.LoadAssetAtPath<Tile>(tilePath);
        if (existingTile != null && existingTile.sprite != null)
            return existingTile;

        // 1. 텍스처 생성 & 에셋 저장
        var tex = new Texture2D(16, 16, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        var pixels = new Color[256];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = color;
        tex.SetPixels(pixels);
        tex.Apply();

        var existingTex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
        if (existingTex != null)
        {
            EditorUtility.CopySerialized(tex, existingTex);
            AssetDatabase.SaveAssets();
        }
        else
        {
            AssetDatabase.CreateAsset(tex, texPath);
            AssetDatabase.SaveAssets();
        }

        // 저장된 텍스처를 다시 로드 (안정적인 레퍼런스)
        var savedTex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);

        // 2. 스프라이트 생성
        var sprite = Sprite.Create(savedTex,
            new Rect(0, 0, 16, 16),
            new Vector2(0.5f, 0.5f),
            16f);
        sprite.name = tileName + "_Sprite";

        // 3. Tile 에셋 생성 또는 기존 갱신
        Tile tile;
        if (existingTile != null)
        {
            tile = existingTile;
            // 기존 서브에셋 스프라이트 제거
            foreach (var sub in AssetDatabase.LoadAllAssetRepresentationsAtPath(tilePath))
                if (sub is Sprite) Object.DestroyImmediate(sub, true);
        }
        else
        {
            tile = ScriptableObject.CreateInstance<Tile>();
            AssetDatabase.CreateAsset(tile, tilePath);
        }

        // 스프라이트를 Tile 에셋의 서브에셋으로 등록 → 직렬화됨
        AssetDatabase.AddObjectToAsset(sprite, tilePath);
        tile.sprite = sprite;
        tile.color  = Color.white;

        EditorUtility.SetDirty(tile);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(tilePath);

        Debug.Log($"[DungeonTilemapSetup] 타일 에셋 생성/갱신: {tilePath}");
        return tile;
    }

    // -----------------------------------------------------------------------
    private static Grid CreateOrFindGrid(string gridName)
    {
        var existing = GameObject.Find(gridName);
        if (existing != null) return existing.GetComponent<Grid>();

        var go = new GameObject(gridName);
        go.transform.position = Vector3.zero;
        var grid = go.AddComponent<Grid>();
        grid.cellSize = Vector3.one;
        Undo.RegisterCreatedObjectUndo(go, "Create DungeonGrid");
        return grid;
    }

    // -----------------------------------------------------------------------
    private static Tilemap CreateOrFindTilemap(Grid grid, string tmName, int order)
    {
        var child = grid.transform.Find(tmName);
        if (child != null) return child.GetComponent<Tilemap>();

        var go = new GameObject(tmName);
        go.transform.SetParent(grid.transform, false);
        go.AddComponent<Tilemap>();
        var r = go.AddComponent<TilemapRenderer>();
        r.sortingOrder = (order == 1) ? 2 : 0;
        Undo.RegisterCreatedObjectUndo(go, $"Create Tilemap {tmName}");
        return go.GetComponent<Tilemap>();
    }

    // -----------------------------------------------------------------------
    // 2방 + 복도 레이아웃 페인팅
    // -----------------------------------------------------------------------
    private static void PaintDungeon(Tilemap ground, Tilemap walls,
                                     TileBase floorTile, TileBase wallTile)
    {
        ground.ClearAllTiles();
        walls.ClearAllTiles();

        // Room1: x=-10~9, y=-6~5 / 오른쪽 벽 y=-1~1에 문 구멍
        PaintRoom(ground, walls, floorTile, wallTile,
            -10, -6, 20, 12,
            rightGapYMin: -1, rightGapYMax: 1,
            leftGapYMin: 999, leftGapYMax: 999);

        // 복도: x=10~13, y=-1~1
        for (int cx = 10; cx <= 13; cx++)
        {
            for (int cy = -1; cy <= 1; cy++)
                ground.SetTile(new Vector3Int(cx, cy, 0), floorTile);
            walls.SetTile(new Vector3Int(cx, -2, 0), wallTile);
            walls.SetTile(new Vector3Int(cx,  2, 0), wallTile);
        }

        // Room2: x=14~33, y=-6~5 / 왼쪽 벽 y=-1~1에 문 구멍
        PaintRoom(ground, walls, floorTile, wallTile,
            14, -6, 20, 12,
            rightGapYMin: 999, rightGapYMax: 999,
            leftGapYMin: -1, leftGapYMax: 1);

        Debug.Log("[DungeonTilemapSetup] 타일 배치 완료 - Room1 + 복도 + Room2");
    }

    // -----------------------------------------------------------------------
    // 단일 방 페인팅 (문 구멍 지원)
    // -----------------------------------------------------------------------
    private static void PaintRoom(Tilemap ground, Tilemap walls,
        TileBase floorTile, TileBase wallTile,
        int ox, int oy, int rw, int rh,
        int rightGapYMin, int rightGapYMax,
        int leftGapYMin,  int leftGapYMax)
    {
        for (int x = 0; x < rw; x++)
        {
            for (int y = 0; y < rh; y++)
            {
                int wx = ox + x, wy = oy + y;
                var pos = new Vector3Int(wx, wy, 0);

                bool topBot    = (y == 0 || y == rh - 1);
                bool leftEdge  = (x == 0);
                bool rightEdge = (x == rw - 1);

                bool rightOpen = rightEdge && wy >= rightGapYMin && wy <= rightGapYMax;
                bool leftOpen  = leftEdge  && wy >= leftGapYMin  && wy <= leftGapYMax;

                bool isWall = (topBot || leftEdge || rightEdge) && !rightOpen && !leftOpen;

                if (isWall)
                    walls.SetTile(pos, wallTile);
                else if (!topBot && !leftEdge && !rightEdge)
                    ground.SetTile(pos, floorTile);
                else if (rightOpen || leftOpen)
                    ground.SetTile(pos, floorTile);
            }
        }
    }

    // -----------------------------------------------------------------------
    private static void SetupWallCollider(GameObject wallGO)
    {
        foreach (var c in wallGO.GetComponents<TilemapCollider2D>())   Object.DestroyImmediate(c);
        foreach (var c in wallGO.GetComponents<CompositeCollider2D>()) Object.DestroyImmediate(c);
        foreach (var c in wallGO.GetComponents<Rigidbody2D>())         Object.DestroyImmediate(c);

        var rb = wallGO.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Static;

        var tmc = wallGO.AddComponent<TilemapCollider2D>();
        tmc.compositeOperation = Collider2D.CompositeOperation.Merge;

        var cc = wallGO.AddComponent<CompositeCollider2D>();
        cc.geometryType = CompositeCollider2D.GeometryType.Polygons;

        wallGO.layer = LayerMask.NameToLayer("Default");
        Debug.Log("[DungeonTilemapSetup] 벽 충돌체 설정 완료");
    }
}
