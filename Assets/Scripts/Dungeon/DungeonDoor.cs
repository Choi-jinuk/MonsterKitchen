using System;
using UnityEngine;

namespace MonsterKitchen.Dungeon
{
    // ====================================================================
    //  DungeonDoor — 방과 방 사이 통로를 막는 문
    //
    //  ▶ 사용법
    //    1. 문 역할의 GO 에 BoxCollider2D(isTrigger=false) 와 이 컴포넌트를 추가.
    //    2. linkedRoom 슬롯에 앞 방의 DungeonRoom 을 연결.
    //    3. linkedRoom 이 클리어되면:
    //       - blocker 를 isTrigger=true 로 변환 (물리 차단 해제, 통과 감지 활성)
    //       - visualRenderer 를 숨긴다.
    //    4. 플레이어가 이 콜라이더를 통과하면 OnPlayerPassedThrough 가 발생.
    //       DungeonSceneController 가 이 이벤트를 구독해 다음 방 전환을 처리한다.
    //
    //  ▶ blocker 슬롯 (선택)
    //    비워두면 이 GO 의 첫 번째 Collider2D 를 사용.
    //
    //  ▶ 주의
    //    blocker 가 isTrigger 로 바뀌므로, Enemy/Player 레이어 간
    //    IgnoreLayerCollision 이 설정되어 있어도 OnTriggerEnter2D 는 정상 동작한다.
    // ====================================================================

    public class DungeonDoor : MonoBehaviour
    {
        [Tooltip("이 문을 여는 조건인 앞 방.")]
        [SerializeField] DungeonRoom m_LinkedRoom;

        [Tooltip("막을 콜라이더. 비우면 이 GO 의 첫 번째 Collider2D 를 사용.")]
        [SerializeField] Collider2D m_Blocker;

        [Tooltip("(선택) 숨길 스프라이트 렌더러.")]
        [SerializeField] SpriteRenderer m_VisualRenderer;

        // ================================================================
        //  이벤트 — DungeonSceneController 가 구독
        // ================================================================

        /// <summary>
        /// 방이 클리어된 뒤 플레이어가 이 문을 처음 통과할 때 발생한다.
        /// 한 번 발생 후 자동으로 null 로 초기화된다.
        /// </summary>
        public event Action OnPlayerPassedThrough;

        bool m_RoomCleared;
        bool m_Passed;

        // ================================================================
        //  Mono
        // ================================================================

        void Start()
        {
            if (m_Blocker == null)
                m_Blocker = GetComponent<Collider2D>();

            if (m_LinkedRoom != null)
                m_LinkedRoom.OnRoomCleared += Open;
        }

        void OnDestroy()
        {
            if (m_LinkedRoom != null)
                m_LinkedRoom.OnRoomCleared -= Open;
        }

        // ================================================================
        //  공개 API
        // ================================================================

        /// <summary>연결 방 클리어 시 호출 — 물리 차단 해제, 통과 감지 시작.</summary>
        public void Open()
        {
            m_RoomCleared = true;
            Debug.Log($"[DungeonDoor] '{name}' 열림 — 플레이어 통과 대기");

            // blocker 를 trigger 로 전환: 물리 충돌은 해제되고 통과 감지만 남는다.
            if (m_Blocker != null)
                m_Blocker.isTrigger = true;

            if (m_VisualRenderer != null)
                m_VisualRenderer.enabled = false;
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (m_Passed || !m_RoomCleared) return;
            if (!other.CompareTag("Player")) return;

            m_Passed = true;
            Debug.Log($"[DungeonDoor] '{name}' — 플레이어 통과 감지");

            OnPlayerPassedThrough?.Invoke();
            OnPlayerPassedThrough = null;  // 단발 이벤트
        }
    }
}
