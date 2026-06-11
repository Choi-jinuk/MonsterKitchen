using MonsterKitchen.Core.Collections;
using UnityEngine;

namespace MonsterKitchen.Navigation
{
    // ====================================================================
    //  NavPathfinder — A* 경로 탐색 (정적 유틸리티)
    //
    //  ▶ NavGrid.Instance 를 사용하므로 씬에 NavGrid 가 있어야 동작한다.
    //  ▶ 8방향 이동, 대각선 코너컷팅 방지.
    //  ▶ LOS 최적화: 출발~도착 직통이면 2점 경로 즉시 반환.
    //  ▶ 경로 스무딩: 불필요한 중간 경유지 제거.
    //  ▶ FindPath 반환값: 월드 좌표 Vector2[] (null = 경로 없음/맵 없음)
    //
    //  최적화 포인트
    //    · Node[,] 정적 캐시 — FindPath 호출마다 배열 할당 없음.
    //    · 세대 ID(SearchGen) — 노드 상태 초기화를 O(w×h) → O(1)/노드 로 절감.
    //    · Node 객체 재사용 — 초기 방문 후 GC 없음.
    // ====================================================================

    public static class NavPathfinder
    {
        // ── 8방향 오프셋 ─────────────────────────────────────────────────
        static readonly Vector2Int[] s_Dirs = {
            new Vector2Int( 0,  1), new Vector2Int( 0, -1),
            new Vector2Int(-1,  0), new Vector2Int( 1,  0),
            new Vector2Int(-1,  1), new Vector2Int( 1,  1),
            new Vector2Int(-1, -1), new Vector2Int( 1, -1),
        };

        const float Sqrt2 = 1.41421356f;

        // ── 노드 풀 (정적 캐시) ─────────────────────────────────────────
        static Node[,] s_Pool;
        static int     s_SearchGen;  // 검색마다 증가 — 노드 상태를 O(1)로 무효화

        // ── A* 노드 ─────────────────────────────────────────────────────
        sealed class Node
        {
            public int   x, y;
            public float g;
            public float h;
            public Node  parent;
            public bool  InOpen;
            public int   SearchGen;   // s_SearchGen 과 다르면 미방문
            public float F => g + h;
        }

        // ================================================================
        //  공개 API
        // ================================================================

        /// <summary>
        /// start → end 경로를 A* 로 탐색해 월드 좌표 배열로 반환한다.
        /// NavGrid 가 씬에 없거나 경로를 찾지 못하면 null 을 반환한다.
        /// </summary>
        public static Vector2[] FindPath(Vector2 start, Vector2 end)
        {
            var grid = NavGrid.Instance;
            if (grid == null) return null;

            if (!grid.WorldToGrid(start, out int sx, out int sy)) return null;
            if (!grid.WorldToGrid(end,   out int ex, out int ey)) return null;

            if (sx == ex && sy == ey) return new[] { start, end };

            if (HasLOSGrid(grid, sx, sy, ex, ey)) return new[] { start, end };

            var path = RunAStar(grid, sx, sy, ex, ey);
            return path == null ? null : SmoothPath(grid, path);
        }

        // ================================================================
        //  A* 내부
        // ================================================================

        static Vector2[] RunAStar(NavGrid grid, int sx, int sy, int ex, int ey)
        {
            int w = grid.Width, h = grid.Height;

            // ── 노드 풀 준비 ─────────────────────────────────────────────
            // 그리드 크기가 달라졌을 때만 재할당.
            // 세대 ID 증가로 이전 검색 결과를 O(1) 무효화.
            s_SearchGen++;
            if (s_Pool == null || s_Pool.GetLength(0) < w || s_Pool.GetLength(1) < h)
                s_Pool = new Node[w, h];

            using var open = new PooledList<Node>(64);

            var start = GetNode(w: w, x: sx, y: sy);
            start.g = 0f;
            start.h = Heuristic(sx, sy, ex, ey);
            start.InOpen = true;
            open.Add(start);

            while (open.Count > 0)
            {
                // 최소 F 노드 선택 (swap-and-pop)
                int best = 0;
                for (int i = 1; i < open.Count; i++)
                    if (open[i].F < open[best].F) best = i;

                var cur = open[best];
                open[best] = open[open.Count - 1];
                open.RemoveAt(open.Count - 1);
                cur.InOpen = false;

                if (cur.x == ex && cur.y == ey)
                    return RetracePath(grid, cur);

                foreach (var dir in s_Dirs)
                {
                    int nx = cur.x + dir.x;
                    int ny = cur.y + dir.y;

                    if (!grid.IsWalkableGrid(nx, ny)) continue;

                    bool diag = dir.x != 0 && dir.y != 0;
                    if (diag && (!grid.IsWalkableGrid(cur.x + dir.x, cur.y)
                              || !grid.IsWalkableGrid(cur.x, cur.y + dir.y)))
                        continue;

                    float newG = cur.g + (diag ? Sqrt2 : 1f);
                    var   nbr  = GetNode(w, nx, ny);
                    if (newG >= nbr.g) continue;

                    nbr.g      = newG;
                    nbr.h      = Heuristic(nx, ny, ex, ey);
                    nbr.parent = cur;

                    if (!nbr.InOpen)
                    {
                        nbr.InOpen = true;
                        open.Add(nbr);
                    }
                }
            }

            return null;
        }

        // ── 노드 풀 접근 ─────────────────────────────────────────────────
        static Node GetNode(int w, int x, int y)
        {
            var n = s_Pool[x, y];
            if (n == null)
            {
                n = new Node { x = x, y = y };
                s_Pool[x, y] = n;
            }

            // 현재 검색 세대와 다르면 상태 초기화
            if (n.SearchGen != s_SearchGen)
            {
                n.g         = float.MaxValue;
                n.h         = 0f;
                n.parent    = null;
                n.InOpen    = false;
                n.SearchGen = s_SearchGen;
            }

            return n;
        }

        static Vector2[] RetracePath(NavGrid grid, Node end)
        {
            using var pts = new PooledList<Vector2>();
            for (var n = end; n != null; n = n.parent)
                pts.Add(grid.GridToWorld(n.x, n.y));
            pts.Reverse();
            return pts.ToArray();
        }

        // ================================================================
        //  휴리스틱 — Octile Distance (8방향 최적)
        // ================================================================

        static float Heuristic(int ax, int ay, int bx, int by)
        {
            int dx = Mathf.Abs(ax - bx);
            int dy = Mathf.Abs(ay - by);
            return Mathf.Max(dx, dy) + (Sqrt2 - 1f) * Mathf.Min(dx, dy);
        }

        // ================================================================
        //  LOS — Bresenham 직선 순회
        // ================================================================

        static bool HasLOSGrid(NavGrid grid, int x0, int y0, int x1, int y1)
        {
            int dx = Mathf.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
            int dy = -Mathf.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
            int err = dx + dy;
            int cx = x0, cy = y0;

            while (true)
            {
                if (!grid.IsWalkableGrid(cx, cy)) return false;
                if (cx == x1 && cy == y1) return true;

                int e2 = 2 * err;
                if (e2 >= dy) { err += dy; cx += sx; }
                if (e2 <= dx) { err += dx; cy += sy; }
            }
        }

        static bool HasLOSWorld(NavGrid grid, Vector2 a, Vector2 b)
        {
            if (!grid.WorldToGrid(a, out int ax, out int ay)) return false;
            if (!grid.WorldToGrid(b, out int bx, out int by)) return false;
            return HasLOSGrid(grid, ax, ay, bx, by);
        }

        // ================================================================
        //  경로 스무딩 — 불필요한 중간 경유지 제거
        // ================================================================

        static Vector2[] SmoothPath(NavGrid grid, Vector2[] path)
        {
            if (path == null || path.Length <= 2) return path;

            using var result = new PooledList<Vector2>();
            result.Add(path[0]);
            int cur = 0;

            while (cur < path.Length - 1)
            {
                int furthest = cur + 1;
                for (int i = path.Length - 1; i > cur + 1; i--)
                {
                    if (HasLOSWorld(grid, path[cur], path[i]))
                    {
                        furthest = i;
                        break;
                    }
                }
                result.Add(path[furthest]);
                cur = furthest;
            }

            return result.ToArray();
        }
    }
}
