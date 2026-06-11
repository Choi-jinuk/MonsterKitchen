using System.Collections;
using MonsterKitchen.Core;
using UnityEngine;

namespace MonsterKitchen.Restaurant
{
    /// <summary>
    /// 식당 테이블 1개. 착석 여부 + 청결 상태를 관리한다.
    /// 손님 퇴장 시 IsDirty=true. 플레이어 E키로 1.5초 청소 가능.
    /// </summary>
    public class RestaurantTable : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] Transform  m_SeatPoint;
        [SerializeField] GameObject m_DirtyOverlay;

        [Header("Settings")]
        [SerializeField] float m_CleanDuration = 1.5f;

        public bool       IsOccupied { get; private set; }
        public bool       IsDirty    { get; private set; }
        public CustomerAI Occupant   { get; private set; }

        public Transform SeatPoint => m_SeatPoint != null ? m_SeatPoint : transform;

        bool m_IsCleaning;

        // ── 착석 / 퇴장 ───────────────────────────────────────────────

        public void Occupy(CustomerAI customer)
        {
            IsOccupied = true;
            Occupant   = customer;
        }

        public void Vacate()
        {
            IsOccupied = false;
            Occupant   = null;
        }

        // ── 청결 상태 ─────────────────────────────────────────────────

        public void SetDirty(bool dirty)
        {
            IsDirty = dirty;
            if (m_DirtyOverlay != null)
                m_DirtyOverlay.SetActive(dirty);
        }

        // ── 플레이어 근접 청소 상호작용 ────────────────────────────────

        void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;
            if (InputManager.Instance != null)
                InputManager.Instance.OnInteract += TryClean;
        }

        void OnTriggerExit2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;
            if (InputManager.Instance != null)
                InputManager.Instance.OnInteract -= TryClean;
        }

        void OnDisable()
        {
            if (InputManager.Instance != null)
                InputManager.Instance.OnInteract -= TryClean;
        }

        void TryClean()
        {
            if (!IsDirty || IsOccupied || m_IsCleaning) return;
            StartCoroutine(CleanRoutine());
        }

        IEnumerator CleanRoutine()
        {
            m_IsCleaning = true;
            DebugUtil.Log("[Table] 청소 중...");
            yield return new WaitForSeconds(m_CleanDuration);
            SetDirty(false);
            m_IsCleaning = false;
            DebugUtil.Log("[Table] 청소 완료.");
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            if (m_SeatPoint == null)
                DebugUtil.LogWarning("[RestaurantTable] SeatPoint 미연결.", this);
            if (m_DirtyOverlay == null)
                DebugUtil.LogWarning("[RestaurantTable] DirtyOverlay 미연결.", this);
        }
#endif
    }
}
