using System;
using UnityEngine;

namespace MonsterKitchen.Data
{
    // ====================================================================
    //  ResourceNodeData — 채집 노드 정의 (순수 CSV 직렬화 클래스)
    //
    //  ▶ 3-레이어 구조에서의 위치
    //    TableData(ResourceNodeData) — 정적 노드 정의 (이 클래스)  ← 현재
    //    ResourceNode (MonoBehaviour) — 씬 배치 + 런타임 상태
    //
    //  ▶ 시각 틴트(NodeTint)
    //    Color 는 CSV 직렬화 불가 → ResourceNode 컴포넌트의 [SerializeField]로 이동.
    //
    //  ID 범위: RNO_001 = 7001 ~
    // ====================================================================

    [Serializable]
    public class ResourceNodeData
    {
        [Header("Identity")]
        [Tooltip("고유 uint ID. DataTable 키. 예: 7001")]
        public uint Id;

        [Tooltip("로컬라이제이션 키. 예: RNO_001_NAME")]
        public string NameKey;

        [Tooltip("표시 이름 (StringData 미등록 폴백). 예: 숲 나무")]
        public string DisplayName;

        [Tooltip("채집 노드 종류. 채집 도구 호환 여부 판단에 사용된다.")]
        public ResourceNodeType NodeType;

        [Header("Drop")]
        [Tooltip("채집 완료 시 드롭할 재료 ID (IngredientData.Id).")]
        public uint DropIngredientId;

        [Tooltip("최소 드롭 수량.")]
        [Min(1)]
        public int DropMin = 1;

        [Tooltip("최대 드롭 수량. DropMin 이상이어야 한다.")]
        [Min(1)]
        public int DropMax = 2;

        [Header("Node Stats")]
        [Tooltip("채집에 필요한 총 내구도(HP). 이 값만큼 데미지가 누적되면 채집 완료.")]
        [Min(1)]
        public int MaxHp = 30;

        [Header("Respawn")]
        [Tooltip("채집 완료 후 재생성까지 걸리는 시간(초). 0이면 리스폰 없음.")]
        [Min(0f)]
        public float RespawnSeconds = 120f;

        [Header("Visual")]
        [Tooltip("씬에서 노드를 표시할 스프라이트 주소 (AssetManifest 키).")]
        public string NodeSpriteAddress;
    }

    [Serializable]
    public class ResourceNodeTable : DataTable<ResourceNodeData> { }
}
