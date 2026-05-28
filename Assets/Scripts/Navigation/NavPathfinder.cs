using MonsterKitchen.Core.Collections;
using UnityEngine;

namespace MonsterKitchen.Navigation
{
    // ====================================================================
    //  NavPathfinder — A* 경로 탐색 (정적 유틸리티)
    //
    //  ▶ NavGrid.Instance 를 사용하므로 씬에 NavGrid 가 있어야 동작한다.
    //  ▶ 8방향 이동, 대각선 코너컷팅 방지.
    //  ▶ LOS(시야선) 최적화: 출발점과 도착점이 직통이면 2점 경로 반환.
    //  ▶ 경로 스무딩: 불필요한 중간 경유지를 제거해 자연스러운 이동선 생성.
    //  ▶ FindPath 반환값: 월드 좌표 Vector2[] (null = 경로 없음 또는 맵 없음)
    // ====================================================================

    public static class NavPathfinder
    {
        // 8방향 오프셋
        static readonly Vector2Int[] s_Dirs = {
            new Vector2Int( 0,  1), new Vector2Int( 0, -1),
            new Vector2Int(-1,  0), new Vector2Int( 1,  0),
            new Vector2Int(-1,  1), new Vector2Int( 1,  1),
            new Vector2Int(-1, -1), new Vector2Int( 1, -1),
        };

        const float SQRT_TWO = 1.41421356f;

        // ── A* 노드 ─────────────────────────────────────────────────────

        sealed class Node
        {
            public int x, y;
            public float g = float.MaxValue;
            public float h;
            public Node parent;
            public bool InOpen;
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

            // 출발 == 도착
            if (sx == ex && sy == ey)
                return new[] { start, end };

            // 직통 시야선이면 2점 경로로 즉시 반환
            if (HasLOSGrid(grid, sx, sy, ex, ey))
                return new[] { start, end };

            var path = RunAStar(grid, sx, sy, ex, ey);
            if (path == null) return null;

            return SmoothPath(grid, path);
        }

        // ================================================================
        //  A* 내부 구현
        // ================================================================

        static Vector2[] RunAStar(NavGrid grid, int sx, int sy, int ex, int ey)
        {
            int w = grid.Width, h = grid.Height;
            var nodes = new Node[w, h];

            // 오픈 리스트 — PooledList 로 ArrayPool 재사용.
            // using var 로 감싸 어떤 반환 경로에서도 Dispose 보장 (우려 1).
            using var open = new PooledList<Node>(64);

            var start = GetNode(nodes, sx, sy);
            start.g = 0;
            start.h = Heuristic(sx, sy, ex, ey);
            start.InOpen = true;
            open.Add(start);

            while (open.Count > 0)
            {
                // f 값이 가장 낮은 노드 선택
                int bestIdx = 0;
                float bestF = open[0].F;
                for (int i = 1; i < open.Count; i++)
                {
                    if (open[i].F < bestF) { bestF = open[i].F; bestIdx = i; }
                }

                var cur = open[bestIdx];
                open[bestIdx] = open[open.Count - 1]; // swap-and-pop
                open.RemoveAt(open.Count - 1);
                cur.InOpen = false;

                if (cur.x == ex && cur.y == ey)
                    return RetracePath(grid, cur); // open.Dispose() 는 return 후 자동 호출

                foreach (var dir in s_Dirs)
                {
                    int nx = cur.x + dir.x;
                    int ny = cur.y + dir.y;

                    if (!grid.IsWalkableGrid(nx, ny)) continue;

                    // 대각선 이동 시 양 옆이 막혀 있으면 코너컷팅 방지
                    bool diag = dir.x != 0 && dir.y != 0;
                    if (diag && (!grid.IsWalkableGrid(cur.x + dir.x, cur.y)
                              || !grid.IsWalkableGrid(cur.x, cur.y + dir.y)))
                        continue;

                    float moveCost = diag ? SQRT_TWO : 1f;
                    float newG = cur.g + moveCost;

                    var nbr = GetNode(nodes, nx, ny);
                    if (newG >= nbr.g) continue;

                    nbr.g = newG;
                    nbr.h = Heuristic(nx, ny, ex, ey);
                    nbr.parent = cur;

                    if (!nbr.InOpen)
                    {
                        nbr.InOpen = true;
                        open.Add(nbr);
                    }
                }
            }

            return null; // 경로 없음 — using var open 이 Dispose 처리
        }

        static Node GetNode(Node[,] nodes, int x, int y)
        {
            if (nodes[x, y] == null)
                nodes[x, y] = new Node { x = x, y = y };
            return nodes[x, y];
        }

        static Vector2[] RetracePath(NavGrid grid, Node end)
        {
            // using var: Dispose 보장 (우려 1).
            // ToArray() 는 using 블록 안에서 호출 — 배열 복사 후 Dispose (우려 2).
            using var pts = new PooledList<Vector2>();
            for (var n = end; n != null; n = n.parent)
                pts.Add(grid.GridToWorld(n.x, n.y));
            pts.Reverse();
            return pts.ToArray(); // 복사 완료 → using 블록 종료 시 내부 배열 반환
        }

        // ================================================================
        //  Heuristic — Octile Distance (8방향 최적 휴리스틱)
        // ================================================================

        static float Heuristic(int ax, int ay, int bx, int by)
        {
            int dx = Mathf.Abs(ax - bx);
            int dy = Mathf.Abs(ay - by);
            return Mathf.Max(dx, dy) + (SQRT_TWO - 1f) * Mathf.Min(dx, dy);
        }

        // ================================================================
        //  LOS (Bresenham 직선 순회)
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
                // 현재 점에서 가장 멀리 직통으로 볼 수 있는 점 탐색
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
