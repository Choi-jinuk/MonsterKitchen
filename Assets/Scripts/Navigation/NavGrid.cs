using UnityEngine;
using UnityEngine.Tilemaps;

namespace MonsterKitchen.Navigation
{
    // ====================================================================
    //  NavGrid — Tilemap 기반 이동 가능 영역 맵 (씬별 싱글톤)
    //
    //  ▶ floorTilemap 의 cellBounds 를 그리드 범위로 자동 계산.
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
        [SerializeField] Tilemap floorTilemap;
        [Tooltip("벽 타일맵 — 타일이 있는 셀은 이동 불가 처리된다.")]
        [SerializeField] Tilemap wallTilemap;

        // ── 싱글톤 ─────────────────────────────────────────────────────
        public static NavGrid Instance { get; private set; }

        // ── 그리드 데이터 ───────────────────────────────────────────────
        bool[,]     _walkable;
        Vector3Int  _origin;   // 타일맵 cellBounds 의 min (셀 좌표)

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
            if (floorTilemap == null)
            {
                Debug.LogError("[NavGrid] floorTilemap 이 설정되지 않았습니다.");
                return;
            }

            floorTilemap.CompressBounds();
            if (wallTilemap != null) wallTilemap.CompressBounds();

            BoundsInt floorBounds = floorTilemap.cellBounds;

            // 벽 타일맵이 있으면 두 bounds 를 합산
            BoundsInt bounds = floorBounds;
            if (wallTilemap != null)
            {
                BoundsInt wallBounds = wallTilemap.cellBounds;
                Vector3Int bMin = Vector3Int.Min(floorBounds.min, wallBounds.min);
                Vector3Int bMax = Vector3Int.Max(floorBounds.max, wallBounds.max);
                bounds = new BoundsInt(bMin, bMax - bMin);
            }

            _origin = bounds.min;
            Width   = bounds.size.x;
            Height  = bounds.size.y;
            _walkable = new bool[Width, Height];

            for (int x = 0; x < Width; x++)
            for (int y = 0; y < Height; y++)
            {
                var cellPos = new Vector3Int(_origin.x + x, _origin.y + y, 0);

                bool hasFloor = floorTilemap.HasTile(cellPos);
                bool hasWall  = wallTilemap != null && wallTilemap.HasTile(cellPos);

                _walkable[x, y] = hasFloor && !hasWall;
            }

            Debug.Log($"[NavGrid] 베이크 완료  {Width}×{Height}  origin={_origin}");
        }

        // ================================================================
        //  좌표 변환
        // ================================================================

        /// <summary>월드 좌표 → 그리드 인덱스. 범위 외이면 false 반환.</summary>
        public bool WorldToGrid(Vector2 worldPos, out int x, out int y)
        {
            if (floorTilemap == null) { x = y = 0; return false; }

            Vector3Int cell = floorTilemap.WorldToCell(worldPos);
            x = cell.x - _origin.x;
            y = cell.y - _origin.y;
            return x >= 0 && x < Width && y >= 0 && y < Height;
        }

        /// <summary>그리드 인덱스 → 셀 중심 월드 좌표.</summary>
        public Vector2 GridToWorld(int x, int y)
        {
            if (floorTilemap == null) return Vector2.zero;
            var cellPos = new Vector3Int(_origin.x + x, _origin.y + y, 0);
            return floorTilemap.GetCellCenterWorld(cellPos);
        }

        // ================================================================
        //  조회
        // ================================================================

        /// <summary>월드 좌표가 이동 가능한 영역인지 반환한다.</summary>
        public bool IsWalkable(Vector2 worldPos)
        {
            if (_walkable == null) return true;
            if (!WorldToGrid(worldPos, out int x, out int y)) return false;
            return _walkable[x, y];
        }

        /// <summary>그리드 인덱스가 이동 가능한지 반환한다.</summary>
        public bool IsWalkableGrid(int x, int y)
        {
            if (_walkable == null) return true;
            if (x < 0 || y < 0 || x >= Width || y >= Height) return false;
            return _walkable[x, y];
        }

        // ================================================================
        //  Gizmo
        // ================================================================

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            if (_walkable == null || floorTilemap == null) return;

            for (int x = 0; x < Width; x++)
            for (int y = 0; y < Height; y++)
            {
                if (!_walkable[x, y]) continue; // walkable 셀만 표시
                Gizmos.color = new Color(0f, 1f, 0f, 0.12f);
                Gizmos.DrawCube(GridToWorld(x, y), Vector3.one * 0.85f);
            }
        }
#endif
    }
}
