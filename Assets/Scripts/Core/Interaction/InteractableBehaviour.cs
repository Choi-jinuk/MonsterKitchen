using UnityEngine;

namespace MonsterKitchen.Core
{
    // ====================================================================
    //  InteractableBehaviour — Trigger 기반 상호작용 공통 베이스
    //
    //  ▶ 플레이어 Trigger 진입 → InteractionHub.Register(this)
    //    이탈/비활성화        → Unregister
    //
    //  ▶ 서브클래스
    //    Interact()       : E키 실행 동작 (필수)
    //    CanInteract      : 조건부 상호작용 (기본 true)
    //    OnPlayerEnter()  : 진입 시 힌트 표시 등 (선택)
    //    OnPlayerExit()   : 이탈 시 UI 닫기 등 (선택)
    // ====================================================================
    public abstract class InteractableBehaviour : MonoBehaviour, IInteractable
    {
        public virtual bool    CanInteract      => true;
        public virtual Vector3 InteractPosition => transform.position;

        public abstract void Interact();

        protected virtual void OnPlayerEnter() { }
        protected virtual void OnPlayerExit()  { }

        // ── Trigger ──────────────────────────────────────────────────

        void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;
            InteractionHub.Register(this);
            OnPlayerEnter();
        }

        void OnTriggerExit2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;
            InteractionHub.Unregister(this);
            OnPlayerExit();
        }

        protected virtual void OnDisable() => InteractionHub.Unregister(this);
    }
}
