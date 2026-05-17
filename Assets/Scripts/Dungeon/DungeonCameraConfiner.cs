using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace MonsterKitchen.Dungeon
{
    /// <summary>
    /// DungeonScene 시작 시 Floor 타일맵 전체 영역(방 + 통로 포함)을 기반으로
    /// CinemachineConfiner2D 의 경계를 자동 설정한다.
    ///
    /// 사용법:
    ///   1. 빈 GameObject(DungeonBounds)에 이 컴포넌트를 추가.
    ///   2. floorTilemap 에 던전 바닥 타일맵을 연결.
    ///   3. confiner 에 CM_Dungeon 의 CinemachineConfiner2D 컴포넌트를 연결.
    ///   → Start() 에서 타일맵 cellBounds 를 읽어 PolygonCollider2D 를 자동 생성.
    /// </summary>
    [RequireComponent(typeof(PolygonCollider2D))]
    public class DungeonCameraConfiner : MonoBehaviour
    {
        [SerializeField] Tilemap              floorTilemap;
        [SerializeField] CinemachineConfiner2D confiner;

        void Start()
        {
            if (floorTilemap == null || confiner == null) return;

            floorTilemap.CompressBounds();
            BoundsInt cells = floorTilemap.cellBounds;

            // 타일맵 셀 좌표 → 월드 좌표 변환
            Vector3 min = floorTilemap.CellToWorld(new Vector3Int(cells.xMin, cells.yMin, 0));
            Vector3 max = floorTilemap.CellToWorld(new Vector3Int(cells.xMax, cells.yMax, 0));

            // 약간 여유(half-cell)를 두어 맵 가장자리 타일도 포함
            float pad = floorTilemap.cellSize.x * 0.5f;
            min -= new Vector3(pad, pad, 0);
            max += new Vector3(pad, pad, 0);

            var poly = GetComponent<PolygonCollider2D>();
            poly.isTrigger = true;
            poly.pathCount = 1;
            poly.SetPath(0, new Vector2[]
            {
                new Vector2(min.x, min.y),
                new Vector2(max.x, min.y),
                new Vector2(max.x, max.y),
                new Vector2(min.x, max.y),
            });

            confiner.InvalidateBoundingShapeCache();
        }
    }
}
