using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace MonsterKitchen.EditorTools
{
    // ====================================================================
    //  DungeonMapBuilder — 던전 확장 맵 절차 페인트 (개발용 플레이스홀더 타일)
    //
    //  ▶ 사용: DungeonScene 을 열고 메뉴 MonsterKitchen → Build Dungeon Map.
    //  ▶ Floor/Wall 타일맵 전체 클리어 후 레이아웃 rect 로 재페인트.
    //  ▶ 아트 타일 준비 시 이 스크립트의 타일 에셋만 교체하면 된다.
    //  ▶ 에디터 1회 실행 툴 — FindFirstObjectByType 은 런타임 금지 규칙의 예외.
    // ====================================================================
    public static class DungeonMapBuilder
    {
        // 스펙 §3 레이아웃 (셀 좌표)
        static readonly RectInt[] FloorAreas =
        {
            new RectInt(-8,  -23, 16, 13),   // 허브
            new RectInt(-13, -17,  5,  5),   // 숲 통로
            new RectInt(-29, -17, 16, 16),   // 숲 갈래
            new RectInt(  8, -17,  5,  5),   // 암석 통로
            new RectInt( 13, -17, 16, 16),   // 암석 갈래
            new RectInt( -2, -11,  4,  4),   // 심층 통로
            new RectInt(-12,  -8, 24, 16),   // 심층 바깥
            new RectInt( -2,   8,  4,  3),   // 게이트 통로
            new RectInt(-12,  11, 24, 12),   // 심층 안쪽
        };

        // 게이트 봉인 영역 — 바닥 페인트 후 벽 타일로 덮는다 (DungeonGate 가 파괴 시 제거)
        static readonly RectInt GateSeal = new RectInt(-2, 8, 4, 3);

        const string TileDir = "Assets/Data/Tiles";

        [MenuItem("MonsterKitchen/Build Dungeon Map")]
        public static void Build()
        {
            var grid = Object.FindAnyObjectByType<Grid>();
            if (grid == null) { Debug.LogError("[DungeonMapBuilder] Grid 없음 — DungeonScene 을 여세요."); return; }

            Tilemap floor = null, wall = null;
            foreach (var tm in grid.GetComponentsInChildren<Tilemap>(true))
            {
                string n = tm.name.ToLowerInvariant();
                if (n.Contains("wall")) wall = tm;
                else floor ??= tm;
            }
            if (floor == null || wall == null)
            {
                Debug.LogError($"[DungeonMapBuilder] Floor/Wall 타일맵 탐색 실패 (floor:{(floor != null ? floor.name : "없음")}, wall:{(wall != null ? wall.name : "없음")})");
                return;
            }

            var floorTile = GetOrCreateTile("DevFloor", new Color(0.22f, 0.22f, 0.26f));
            var wallTile  = GetOrCreateTile("DevWall",  new Color(0.45f, 0.32f, 0.22f));

            floor.ClearAllTiles();
            wall.ClearAllTiles();

            // 1) 바닥
            var floorCells = new HashSet<Vector3Int>();
            foreach (var r in FloorAreas)
                for (int x = r.xMin; x < r.xMax; x++)
                    for (int y = r.yMin; y < r.yMax; y++)
                        floorCells.Add(new Vector3Int(x, y, 0));
            foreach (var c in floorCells) floor.SetTile(c, floorTile);

            // 2) 벽 — 바닥에 인접(8방향)한 비바닥 셀 1겹
            var dirs = new[]
            {
                new Vector3Int( 1, 0, 0), new Vector3Int(-1, 0, 0),
                new Vector3Int( 0, 1, 0), new Vector3Int( 0,-1, 0),
                new Vector3Int( 1, 1, 0), new Vector3Int(-1, 1, 0),
                new Vector3Int( 1,-1, 0), new Vector3Int(-1,-1, 0),
            };
            var wallCells = new HashSet<Vector3Int>();
            foreach (var c in floorCells)
                foreach (var d in dirs)
                {
                    var n = c + d;
                    if (!floorCells.Contains(n)) wallCells.Add(n);
                }
            foreach (var c in wallCells) wall.SetTile(c, wallTile);

            // 3) 게이트 봉인 — 통로 위 벽 타일
            for (int x = GateSeal.xMin; x < GateSeal.xMax; x++)
                for (int y = GateSeal.yMin; y < GateSeal.yMax; y++)
                    wall.SetTile(new Vector3Int(x, y, 0), wallTile);

            // 4) Wall 타일맵 물리 — TilemapCollider2D 보장
            if (wall.GetComponent<TilemapCollider2D>() == null)
                wall.gameObject.AddComponent<TilemapCollider2D>();

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(grid.gameObject.scene);
            Debug.Log($"[DungeonMapBuilder] 완료 — 바닥 {floorCells.Count}셀, 벽 {wallCells.Count}셀 (Floor: {floor.name}, Wall: {wall.name})");
        }

        static TileBase GetOrCreateTile(string name, Color color)
        {
            string tilePath = $"{TileDir}/{name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Tile>(tilePath);
            if (existing != null) return existing;

            if (!AssetDatabase.IsValidFolder(TileDir))
                AssetDatabase.CreateFolder("Assets/Data", "Tiles");

            // 1×1 단색 스프라이트 PNG 생성 (32px, PPU 32 → 1유닛)
            string pngPath = $"{TileDir}/{name}.png";
            var tex = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            var px  = new Color[32 * 32];
            for (int i = 0; i < px.Length; i++) px[i] = color;
            tex.SetPixels(px);
            tex.Apply();
            System.IO.File.WriteAllBytes(pngPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(pngPath);

            var importer = (TextureImporter)AssetImporter.GetAtPath(pngPath);
            importer.textureType         = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = 32;
            importer.filterMode          = FilterMode.Point;
            importer.SaveAndReimport();

            var tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite       = AssetDatabase.LoadAssetAtPath<Sprite>(pngPath);
            tile.colliderType = Tile.ColliderType.Grid;
            AssetDatabase.CreateAsset(tile, tilePath);
            AssetDatabase.SaveAssets();
            return tile;
        }
    }
}
