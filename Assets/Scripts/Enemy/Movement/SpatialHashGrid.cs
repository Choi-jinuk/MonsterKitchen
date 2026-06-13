using System.Collections.Generic;
using UnityEngine;

namespace MonsterKitchen.Enemy
{
    // ====================================================================
    //  SpatialHashGrid — 순수 2D 공간 해시 (이웃 위치 질의)
    //
    //  ▶ Insert 로 위치+id 등록 → Query 로 인접 9셀 반경 내 위치를 버퍼에 채움.
    //  ▶ id 는 self 제외용 (보통 GetInstanceID()).
    //  ▶ 매 프레임 Clear → Insert* → Query* 패턴. 셀 리스트 재사용으로 할당 최소화.
    // ====================================================================
    public sealed class SpatialHashGrid
    {
        struct Entry { public Vector2 Pos; public int Id; }

        readonly float m_CellSize;
        readonly Dictionary<long, List<Entry>> m_Cells = new Dictionary<long, List<Entry>>(256);
        readonly Stack<List<Entry>>            m_Pool  = new Stack<List<Entry>>();

        public SpatialHashGrid(float cellSize) => m_CellSize = Mathf.Max(0.01f, cellSize);

        static long Key(int cx, int cy) => ((long)cx << 32) ^ (uint)cy;

        int CellCoord(float v) => Mathf.FloorToInt(v / m_CellSize);

        public void Clear()
        {
            foreach (var kv in m_Cells) { kv.Value.Clear(); m_Pool.Push(kv.Value); }
            m_Cells.Clear();
        }

        public void Insert(Vector2 pos, int id)
        {
            long key = Key(CellCoord(pos.x), CellCoord(pos.y));
            if (!m_Cells.TryGetValue(key, out var list))
            {
                list = m_Pool.Count > 0 ? m_Pool.Pop() : new List<Entry>(8);
                m_Cells[key] = list;
            }
            list.Add(new Entry { Pos = pos, Id = id });
        }

        /// <summary>반경 내 이웃 위치를 buffer 에 채우고 개수 반환. excludeId 는 제외.</summary>
        public int Query(Vector2 pos, float radius, int excludeId, Vector2[] buffer)
        {
            int   cx        = CellCoord(pos.x);
            int   cy        = CellCoord(pos.y);
            float sqrRadius = radius * radius;
            int   count     = 0;

            for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            {
                if (!m_Cells.TryGetValue(Key(cx + dx, cy + dy), out var list)) continue;
                for (int i = 0; i < list.Count; i++)
                {
                    var e = list[i];
                    if (e.Id == excludeId)                       continue;
                    if ((e.Pos - pos).sqrMagnitude > sqrRadius)  continue;
                    if (count >= buffer.Length)                  return count;
                    buffer[count++] = e.Pos;
                }
            }
            return count;
        }
    }
}
