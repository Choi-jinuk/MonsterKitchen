using UnityEngine;

namespace MonsterKitchen.Core
{
    // ====================================================================
    //  IInteractable — E키 상호작용 후보 계약
    //
    //  ▶ InteractionHub 에 Register/Unregister 로 등록한다.
    //    E키 입력 시 Hub 가 CanInteract == true 인 후보 중
    //    플레이어와 가장 가까운 하나만 Interact() 를 실행한다.
    // ====================================================================
    public interface IInteractable
    {
        /// <summary>현재 상호작용 가능 여부. false 면 후보에서 제외.</summary>
        bool CanInteract { get; }

        /// <summary>플레이어와의 거리 비교 기준 위치.</summary>
        Vector3 InteractPosition { get; }

        /// <summary>E키 실행 동작.</summary>
        void Interact();
    }
}
