using MonsterKitchen.Core;
using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace MonsterKitchen.Core.Collections
{
    // ========================================================================
    //  PooledList<T> — ArrayPool 기반 일회성 리스트
    //
    //  Collections.Pooled(github.com/jtmueller/Collections.Pooled) 의
    //  PooledList<T> 와 동일한 개념을 Unity 내장 System.Buffers.ArrayPool 로
    //  직접 구현한다.  UPM 패키지가 없으므로 소스 수준으로 제공.
    //
    //  ▶ 해결한 우려 사항
    //    1. Dispose 누락 위험    — IDisposable 구현, 반드시 using var 로 사용
    //    2. 반환 후 참조 유지 버그 — Dispose 후 m_Array = null, ThrowIfDisposed()
    //    5. 너무 큰 리스트가 Pool에 남을 수 있음
    //                            — capacity > MaxPoolableCapacity 이면 풀 미반환
    //
    //  사용 패턴:
    //    using var list = new PooledList<T>(initialCapacity);
    //    list.Add(item);
    //    var result = list.ToArray(); // using 블록 안에서 호출
    //    // } ← using 블록 종료 시 자동 Dispose
    // ========================================================================

    public sealed class PooledList<T> : IDisposable
    {
        // 우려 5: 이 크기를 초과하는 배열은 풀에 반환하지 않는다.
        // ArrayPool 슬롯을 오래 점유하는 것보다 GC로 회수하는 편이 낫다.
        const int MAX_POOLABLE_CAPACITY = 512;

        static readonly ArrayPool<T> s_Pool = ArrayPool<T>.Shared;

        // 참조형(또는 참조 포함 값형)이면 Dispose 시 배열 원소를 clear 해야
        // 풀 재사용 시 stale reference 가 남지 않는다.
        static readonly bool s_NeedsClear =
            RuntimeHelpers.IsReferenceOrContainsReferences<T>();

        T[]  m_Array;
        int  m_Count;
        bool m_Disposed; // 우려 2: Dispose 여부 추적

        // ── 공개 API ──────────────────────────────────────────────────────

        public int Count
        {
            get { ThrowIfDisposed(); return m_Count; }
        }

        /// <summary>읽기 전용 인덱서 — Dispose 후 접근 시 ObjectDisposedException.</summary>
        public T this[int index]
        {
            get
            {
                ThrowIfDisposed();
                return m_Array[index];
            }
            set
            {
                ThrowIfDisposed();
                m_Array[index] = value;
            }
        }

        public PooledList(int initialCapacity = 4)
        {
            m_Array = s_Pool.Rent(Math.Max(initialCapacity, 4));
        }

        public void Add(T item)
        {
            ThrowIfDisposed();
            if (m_Count == m_Array.Length) Grow();
            m_Array[m_Count++] = item;
        }

        public void RemoveAt(int index)
        {
            ThrowIfDisposed();
            m_Count--;
            if (index < m_Count)
                Array.Copy(m_Array, index + 1, m_Array, index, m_Count - index);
            // 참조형이면 빠진 자리를 default 로 clear (stale reference 방지)
            if (s_NeedsClear) m_Array[m_Count] = default;
        }

        public void Reverse()
        {
            ThrowIfDisposed();
            Array.Reverse(m_Array, 0, m_Count);
        }

        /// <summary>현재 원소를 새 배열로 복사해 반환한다. using 블록 안에서 호출해야 한다.</summary>
        public T[] ToArray()
        {
            ThrowIfDisposed();
            var result = new T[m_Count];
            Array.Copy(m_Array, result, m_Count);
            return result;
        }

        // ── Dispose ───────────────────────────────────────────────────────

        public void Dispose()
        {
            if (m_Disposed) return;
            m_Disposed = true;

            if (m_Array != null)
            {
                // 우려 5: 너무 큰 배열은 풀에 돌려주지 않음
                if (m_Array.Length <= MAX_POOLABLE_CAPACITY)
                    s_Pool.Return(m_Array, clearArray: s_NeedsClear);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                // 우려 5: 초과 크기 배열 GC 회수를 로그로 확인 (에디터/개발 빌드만)
                else
                    DebugUtil.Log($"[PooledList<{typeof(T).Name}>] capacity={m_Array.Length} > {MAX_POOLABLE_CAPACITY}, not returned to pool (GC).");
#endif
                // 우려 2: 반환된 배열에 대한 참조를 즉시 무효화
                m_Array = null;
            }

            m_Count = 0;
        }

        // ── 내부 ─────────────────────────────────────────────────────────

        void Grow()
        {
            int newCap = Math.Max(m_Array.Length * 2, 8);
            var newArr = s_Pool.Rent(newCap);
            Array.Copy(m_Array, newArr, m_Count);

            // 기존 배열 반환 (크기 제한 적용)
            if (m_Array.Length <= MAX_POOLABLE_CAPACITY)
                s_Pool.Return(m_Array, clearArray: s_NeedsClear);

            m_Array = newArr;
        }

        // 우려 2: Dispose 후 접근 감지
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void ThrowIfDisposed()
        {
            if (m_Disposed)
                throw new ObjectDisposedException($"PooledList<{typeof(T).Name}>");
        }
    }
}
