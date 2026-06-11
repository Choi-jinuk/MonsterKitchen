using MonsterKitchen.Core;
using System.Collections;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace MonsterKitchen.Navigation
{
    // ====================================================================
    //  NavGrid — Tilemap 기반 이동 가능 영역 맵 (씬별 싱글톤)
    //
    //  ▶ m_FloorTilemap 의 cellBounds 를 그리드 범위로 자동 계산.
    //    → 방이 추가되거나 복도가 이어져도 Bake() 한 번으로 전체 커버.
    //  ▶ walkable = floor 타일 존재 AND wall 타일 없음.
    //  ▶ Grid 컴포넌트의 cellSize(1×1) 와 1:1 대응 — 셀 하나 = 타일 하나.
    //  ▶ NavGrid 가 없는 씬(주방·식당 등)에서는
    //    Instance == null 이 되어 이동 스크립트가 직선 이동으로 폴백한다.
    // ====================================================================

    [AddComponentMenu("Navigation/NavGrid")]
    public class NavGrid : MonoBehaviour
    {
        [Header("Tilemap References")]
        [Tooltip("바닥 타일맵 — 이 타일맵의 cellBounds 가 그리드 범위가 된다.")]
        [SerializeField] Tilemap m_FloorTilemap;
        [Tooltip("벽 타일맵 — 타일이 있는 셀은 이동 불가 처리된다.")]
        [SerializeField] Tilemap m_WallTilemap;

        // ── 싱글톤 ─────────────────────────────────────────────────────
        public static NavGrid Instance { get; private set; }

        // ── 그리드 데이터 ───────────────────────────────────────────────
        bool[,]     m_Walkable;
        Vector3Int  m_Origin;   // 타일맵 cellBounds 의 min (셀 좌표)

        public int Width  { get; private set; }
        public int Height { get; private set; }

        // ================================================================
        //  Mono
        // ================================================================

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            // Bake 는 Start 에서 1프레임 뒤에 실행.
            // 씬 활성화 직후 Awake 가 몰리는 스파이크에서 분리한다.
        }

        IEnumerator Start()
        {
            yield return null;   // 씬의 모든 Awake 완료 후 다음 프레임
            Bake();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ================================================================
        //  베이크
        // ================================================================

        /// <summary>
        /// 타일맵 정보를 읽어 각 셀의 이동 가능 여부를 계산한다.
        /// 방 구조 변경 후(런타임 생성 등) 수동 호출 가능.
        /// </summary>
        public void Bake()
        {
            if (m_FloorTilemap == null)
            {
                DebugUtil.LogError("[NavGrid] m_FloorTilemap 이 설정되지 않았습니다.");
                return;
            }

            m_FloorTilemap.CompressBounds();
            if (m_WallTilemap != null) m_WallTilemap.CompressBounds();

            BoundsInt floorBounds = m_FloorTilemap.cellBounds;

            // 벽 타일맵이 있으면 두 bounds 를 합산
            BoundsInt bounds = floorBounds;
            if (m_WallTilemap != null)
            {
                BoundsInt wallBounds = m_WallTilemap.cellBounds;
                Vector3Int bMin = Vector3Int.Min(floorBounds.min, wallBounds.min);
                Vector3Int bMax = Vector3Int.Max(floorBounds.max, wallBounds.max);
                bounds = new BoundsInt(bMin, bMax - bMin);
            }

            m_Origin = bounds.min;
            Width    = bounds.size.x;
            Height   = bounds.size.y;
            m_Walkable = new bool[Width, Height];

            for (int x = 0; x < Width; x++)
            for (int y = 0; y < Height; y++)
            {
                var cellPos = new Vector3Int(m_Origin.x + x, m_Origin.y + y, 0);

                bool hasFloor = m_FloorTilemap.HasTile(cellPos);
                bool hasWall  = m_WallTilemap != null && m_WallTilemap.HasTile(cellPos);

                m_Walkable[x, y] = hasFloor && !hasWall;
            }

            DebugUtil.Log($"[NavGrid] 베이크 완료  {Width}×{Height}  origin={m_Origin}");
        }

        // ================================================================
        //  좌표 변환
        // ================================================================

        /// <summary>월드 좌표 → 그리드 인덱스. 범위 외이면 false 반환.</summary>
        public bool WorldToGrid(Vector2 worldPos, out int x, out int y)
        {
            if (m_FloorTilemap == null) { x = y = 0; return false; }

            Vector3Int cell = m_FloorTilemap.WorldToCell(worldPos);
            x = cell.x - m_Origin.x;
            y = cell.y - m_Origin.y;
            return x >= 0 && x < Width && y >= 0 && y < Height;
        }

        /// <summary>그리드 인덱스 → 셀 중심 월드 좌표.</summary>
        public Vector2 GridToWorld(int x, int y)
        {
            if (m_FloorTilemap == null) return Vector2.zero;
            var cellPos = new Vector3Int(m_Origin.x + x, m_Origin.y + y, 0);
            return m_FloorTilemap.GetCellCenterWorld(cellPos);
        }

        // ================================================================
        //  조회
        // ================================================================

        /// <summary>월드 좌표가 이동 가능한 영역인지 반환한다.</summary>
        public bool IsWalkable(Vector2 worldPos)
        {
            if (m_Walkable == null) return true;
            if (!WorldToGrid(worldPos, out int x, out int y)) return false;
            return m_Walkable[x, y];
        }

        /// <summary>그리드 인덱스가 이동 가능한지 반환한다.</summary>
        public bool IsWalkableGrid(int x, int y)
        {
            if (m_Walkable == null) return true;
            if (x < 0 || y < 0 || x >= Width || y >= Height) return false;
            return m_Walkable[x, y];
        }

        /// <summary>
        /// 월드 좌표가 이동 가능한 영역인지 반환한다.
        /// 타일맵 기반 IsWalkable AND NavPolyObstacle 장애물 없음 을 동시에 검사한다.
        /// </summary>
        public bool PointIsValid(Vector2 worldPos)
        {
            if (!IsWalkable(worldPos)) return false;
            return !NavObstacleLayer.IsBlocked(worldPos);
        }

        /// <summary>
        /// 비-walkable 위치에서 BFS 로 가장 가까운 walkable 셀(타일맵 기준)의 월드 좌표를 반환한다.
        /// NavObstacleLayer 는 검사하지 않는다 — 동적 장애물 포함 검증은 GetNearestValid 사용.
        /// </summary>
        public Vector2 GetNearestWalkable(Vector2 worldPos, int searchRadius = 5)
        {
            if (m_FloorTilemap == null) return worldPos;

            Vector3Int cell = m_FloorTilemap.WorldToCell(worldPos);
            int cx = Mathf.Clamp(cell.x - m_Origin.x, 0, Width  - 1);
            int cy = Mathf.Clamp(cell.y - m_Origin.y, 0, Height - 1);

            if (IsWalkableGrid(cx, cy)) return GridToWorld(cx, cy);

            for (int r = 1; r <= searchRadius; r++)
            {
                for (int dx = -r; dx <= r; dx++)
                for (int dy = -r; dy <= r; dy++)
                {
                    if (Mathf.Abs(dx) != r && Mathf.Abs(dy) != r) continue;
                    int nx = cx + dx, ny = cy + dy;
                    if (IsWalkableGrid(nx, ny))
                        return GridToWorld(nx, ny);
                }
            }

            return worldPos;
        }

        /// <summary>
        /// 비유효 위치에서 BFS 로 가장 가까운 유효 위치(PointIsValid 기준)를 반환한다.
        /// IsWalkable(타일맵) AND NavObstacleLayer 장애물 없음 을 동시에 검사한다.
        /// </summary>
        public Vector2 GetNearestValid(Vector2 worldPos, int searchRadius = 5)
        {
            if (m_FloorTilemap == null) return worldPos;

            Vector3Int cell = m_FloorTilemap.WorldToCell(worldPos);
            int cx = Mathf.Clamp(cell.x - m_Origin.x, 0, Width  - 1);
            int cy = Mathf.Clamp(cell.y - m_Origin.y, 0, Height - 1);

            Vector2 center = GridToWorld(cx, cy);
            if (PointIsValid(center)) return center;

            for (int r = 1; r <= searchRadius; r++)
            {
                for (int dx = -r; dx <= r; dx++)
                for (int dy = -r; dy <= r; dy++)
                {
                    if (Mathf.Abs(dx) != r && Mathf.Abs(dy) != r) continue;
                    int nx = cx + dx, ny = cy + dy;
                    if (nx < 0 || ny < 0 || nx >= Width || ny >= Height) continue;
                    Vector2 candidate = GridToWorld(nx, ny);
                    if (PointIsValid(candidate))
                        return candidate;
                }
            }

            return worldPos;
        }

        // ================================================================
        //  Gizmo
        // ================================================================

#if UNITY_EDITOR
        [Header("Gizmo")]
        [Tooltip("씬 뷰에서 베이킹된 이동 가능 영역을 항상 표시한다.")]
        [SerializeField] bool m_ShowGizmos = true;
        [Tooltip("이동 불가 셀도 표시한다 (빨간색).")]
        [SerializeField] bool m_ShowBlocked;

        void OnDrawGizmos()
        {
            if (!m_ShowGizmos || m_Walkable == null || m_FloorTilemap == null) return;

            var walkableColor = new Color(0.1f, 0.9f, 0.2f, 0.18f);
            var blockedColor  = new Color(0.9f, 0.15f, 0.1f, 0.22f);
            var walkableWire  = new Color(0.1f, 0.9f, 0.2f, 0.35f);
            var blockedWire   = new Color(0.9f, 0.15f, 0.1f, 0.45f);
            var cellSize      = new Vector3(0.92f, 0.92f, 0.02f);

            for (int x = 0; x < Width; x++)
            for (int y = 0; y < Height; y++)
            {
                bool walkable = m_Walkable[x, y];
                if (!walkable && !m_ShowBlocked) continue;

                Vector3 center = GridToWorld(x, y);

                Gizmos.color = walkable ? walkableColor : blockedColor;
                Gizmos.DrawCube(center, cellSize);

                Gizmos.color = walkable ? walkableWire : blockedWire;
                Gizmos.DrawWireCube(center, cellSize);
            }

            if (Width > 0 && Height > 0)
            {
                Vector3 min    = GridToWorld(0, 0);
                Vector3 max    = GridToWorld(Width - 1, Height - 1);
                Vector3 bounds = new Vector3(max.x - min.x + 1f, max.y - min.y + 1f, 0.02f);
                Gizmos.color = new Color(1f, 1f, 0f, 0.6f);
                Gizmos.DrawWireCube((min + max) * 0.5f, bounds);
            }
        }
#endif
    }
}
