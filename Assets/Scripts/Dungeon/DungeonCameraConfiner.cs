using MonsterKitchen.Core;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace MonsterKitchen.Dungeon
{
    /// <summary>
    /// Floor 타일맵 전체 영역을 기반으로 CinemachineConfiner2D 경계를 설정한다.
    ///
    /// ▶ Start() 에서 자동 빌드.
    /// ▶ Refresh() 를 호출하면 타일맵이 바뀐 뒤 즉시 재빌드 가능.
    ///   → Room 전환 시 DungeonSceneController 에서 호출한다.
    ///
    /// ▶ extraPadding
    ///   카메라 뷰포트 여유분. 기본 2 unit.
    ///   카메라 반-폭(약 4.4 unit)보다 작아서 벽 끝까지 카메라가 따라갈 수 없다면
    ///   Inspector 에서 값을 늘린다.
    /// </summary>
    [RequireComponent(typeof(PolygonCollider2D))]
    public class DungeonCameraConfiner : MonoBehaviour
    {
        [SerializeField] Tilemap               m_FloorTilemap;
        [SerializeField] CinemachineConfiner2D m_Confiner;

        [Tooltip("타일맵 경계 바깥으로 추가할 여유 거리 (unit).\n" +
                 "카메라가 왼쪽·오른쪽 벽 끝까지 따라가지 못하면 이 값을 늘린다.")]
        [SerializeField] float m_ExtraPadding = 2f;

        // ================================================================
        //  Mono
        // ================================================================

        void Start() => BuildBounds();

        // ================================================================
        //  공개 API
        // ================================================================

        /// <summary>
        /// 타일맵 변경 후 경계를 다시 계산한다.
        /// Room 전환 시 새 타일맵 활성화 직후 호출할 것.
        /// </summary>
        public void Refresh() => BuildBounds();

        // ================================================================
        //  내부 빌드
        // ================================================================

        void BuildBounds()
        {
            if (m_FloorTilemap == null || m_Confiner == null) return;

            m_FloorTilemap.CompressBounds();
            BoundsInt cells = m_FloorTilemap.cellBounds;

            // 타일맵 셀 좌표 → 월드 좌표
            Vector3 min = m_FloorTilemap.CellToWorld(new Vector3Int(cells.xMin, cells.yMin, 0));
            Vector3 max = m_FloorTilemap.CellToWorld(new Vector3Int(cells.xMax, cells.yMax, 0));

            // 타일 반-셀(0.5) + extraPadding 여유 추가
            float pad = m_FloorTilemap.cellSize.x * 0.5f + m_ExtraPadding;
            min -= new Vector3(pad, pad, 0f);
            max += new Vector3(pad, pad, 0f);

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

            m_Confiner.InvalidateBoundingShapeCache();

            DebugUtil.Log($"[DungeonCameraConfiner] 경계 빌드 완료: " +
                      $"({min.x:F1},{min.y:F1}) ~ ({max.x:F1},{max.y:F1})  pad={pad:F1}");
        }
    }
}
