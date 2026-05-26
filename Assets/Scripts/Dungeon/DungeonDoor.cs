using UnityEngine;

namespace MonsterKitchen.Dungeon
{
    // ====================================================================
    //  DungeonDoor — 방과 방 사이 통로를 막는 문
    //
    //  ▶ 사용법
    //    1. 문 역할의 GO 에 BoxCollider2D 와 이 컴포넌트를 추가.
    //    2. linkedRoom 슬롯에 앞 방의 DungeonRoom 을 연결.
    //    3. blocker 슬롯에 막을 Collider2D 를 연결 (없으면 this GO 첫 번째).
    //    4. linkedRoom 이 클리어되면 blocker 를 비활성화하고 GO 를 숨긴다.
    //
    //  ▶ SpriteRenderer 연결 (선택)
    //    visualRenderer 에 연결하면 문이 열릴 때 스프라이트도 숨긴다.
    // ====================================================================

    public class DungeonDoor : MonoBehaviour
    {
        [Tooltip("이 문을 여는 조건인 앞 방.")]
        [SerializeField] DungeonRoom  linkedRoom;

        [Tooltip("막을 콜라이더. 비우면 이 GO 의 첫 번째 Collider2D 를 사용.")]
        [SerializeField] Collider2D   blocker;

        [Tooltip("(선택) 숨길 스프라이트 렌더러.")]
        [SerializeField] SpriteRenderer visualRenderer;

        // ================================================================
        //  Mono
        // ================================================================

        void Start()
        {
            if (blocker == null)
                blocker = GetComponent<Collider2D>();

            if (linkedRoom != null)
                linkedRoom.OnRoomCleared += Open;
        }

        void OnDestroy()
        {
            if (linkedRoom != null)
                linkedRoom.OnRoomCleared -= Open;
        }

        // ================================================================
        //  Public API
        // ================================================================

        public void Open()
        {
            Debug.Log($"[DungeonDoor] '{name}' 열림");
            if (blocker != null)        blocker.enabled = false;
            if (visualRenderer != null) visualRenderer.enabled = false;
        }
    }
}
