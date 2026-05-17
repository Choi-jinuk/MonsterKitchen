using UnityEngine;

namespace MonsterKitchen.Data
{
    // ====================================================================
    //  GatheringToolData — 채집 도구 정의 ScriptableObject
    //
    //  플레이어가 채집 전용 슬롯에 장착하는 도구.
    //  기본 채집은 도구 없이도 가능하지만, 도구를 장착하면
    //  호환 노드 타입에 대해 채집량·속도 보너스가 적용된다.
    //
    //  ▶ 내구도
    //    채집을 한 번 완료할 때마다 currentDurability 가 1 감소한다 (PlayerStats 관리).
    //    currentDurability == 0 이면 도구 파괴 → 슬롯 비워짐, 보너스 없어짐.
    //    maxDurability = 0 이면 무한 내구도 (파괴 불가).
    //
    //  ▶ 호환 노드
    //    compatibleNodeTypes 에 포함된 ResourceNodeType 의 노드에만 보너스 적용.
    //    포함되지 않은 노드에서도 기본 채집은 가능하지만 보너스 없음.
    //
    //  메뉴: Create → MonsterKitchen → Data → GatheringToolData
    //  파일명 규칙: GTL_001, GTL_002 …
    // ====================================================================

    [CreateAssetMenu(menuName = "MonsterKitchen/Data/GatheringToolData", fileName = "GTL_")]
    public class GatheringToolData : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("고유 ID. 예: GTL_001")]
        public string toolId;

        [Tooltip("표시 이름. 예: 낡은 도끼")]
        public string toolName;

        [Tooltip("도구 종류. 호환 노드 유형 기본값을 결정한다.")]
        public GatheringToolType toolType;

        [Header("Durability")]
        [Tooltip("최대 내구도. 0이면 파괴되지 않는 영구 도구.")]
        [Min(0)]
        public int maxDurability = 20;

        [Header("Bonus (호환 노드에만 적용)")]
        [Tooltip("호환 노드에서 채집 시 드롭 수량 배율. 1.5 = 50% 증가.")]
        [Min(1f)]
        public float gatherMultiplier = 1.5f;

        [Tooltip("채집 속도 배율. 값이 클수록 빠르게 채집된다 (채집에 필요한 HP를 더 빠르게 제거).")]
        [Min(1f)]
        public float speedMultiplier = 1.0f;

        [Header("Compatible Node Types")]
        [Tooltip("이 도구가 보너스를 적용하는 채집 노드 종류 목록.")]
        public ResourceNodeType[] compatibleNodeTypes;
    }
}
