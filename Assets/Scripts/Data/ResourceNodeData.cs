using UnityEngine;

namespace MonsterKitchen.Data
{
    // ====================================================================
    //  ResourceNodeData — 채집 노드 정의 ScriptableObject
    //
    //  던전 맵에 배치되는 나무 / 돌 / 광맥 오브젝트의 데이터를 정의한다.
    //
    //  ▶ 채집 방식
    //    플레이어가 무기로 공격해서 채집한다 (전투 우선, 전투 대상 없을 때 채집).
    //    maxHp 만큼 데미지를 누적하면 채집 완료 → dropIngredient 를 dropMin~Max 수량 드롭.
    //
    //  ▶ 리스폰
    //    채집 완료 후 respawnSeconds 초 뒤에 같은 위치에 재활성화된다.
    //    씬 내 ResourceNode 컴포넌트가 직접 코루틴으로 처리한다.
    //
    //  ▶ 채집 도구 보너스
    //    GatheringToolData.compatibleNodeTypes 에 이 노드 타입이 포함된 도구를
    //    장착했을 때 gatherMultiplier / speedMultiplier 가 적용된다.
    //
    //  메뉴: Create → MonsterKitchen → Data → ResourceNodeData
    //  파일명 규칙: RNO_001, RNO_002 …
    // ====================================================================

    [CreateAssetMenu(menuName = "MonsterKitchen/Data/ResourceNodeData", fileName = "RNO_")]
    public class ResourceNodeData : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("고유 ID. 예: RNO_001")]
        public string nodeId;

        [Tooltip("표시 이름. 예: 숲 나무")]
        public string displayName;

        [Tooltip("채집 노드 종류. 채집 도구 호환 여부 판단에 사용된다.")]
        public ResourceNodeType nodeType;

        [Header("Drop")]
        [Tooltip("채집 완료 시 드롭할 재료 데이터.")]
        public IngredientData dropIngredient;

        [Tooltip("최소 드롭 수량.")]
        [Min(1)]
        public int dropMin = 1;

        [Tooltip("최대 드롭 수량. dropMin 이상이어야 한다.")]
        [Min(1)]
        public int dropMax = 2;

        [Header("Node Stats")]
        [Tooltip("채집에 필요한 총 내구도(HP). 이 값만큼 데미지가 쌓이면 채집 완료.")]
        [Min(1)]
        public int maxHp = 30;

        [Header("Respawn")]
        [Tooltip("채집 완료 후 재생성까지 걸리는 시간(초). 0이면 리스폰 없음.")]
        [Min(0f)]
        public float respawnSeconds = 120f;

        [Header("Visual")]
        [Tooltip("씬에서 노드를 표시할 스프라이트. 없으면 기본 흰 사각형에 nodeTint 색상만 적용.")]
        public Sprite nodeSprite;

        [Tooltip("스프라이트가 없을 때 SpriteRenderer 에 적용할 단색 틴트. 스프라이트가 있으면 색조 보정으로 작동.")]
        public Color nodeTint = Color.white;
    }
}
