using System;
using UnityEngine;

namespace MonsterKitchen.Data
{
    // ====================================================================
    //  GatheringToolData — 채집 도구 정의
    // ====================================================================

    [Serializable]
    public class GatheringToolData
    {
        [Header("Identity")]
        [Tooltip("고유 ID (CSV id 컬럼). 예: 8001")]
        public uint Id;

        [Tooltip("표시 이름. 예: 낡은 도끼")]
        public string ToolName;

        [Tooltip("도구 종류. 호환 노드 유형 기본값을 결정한다.")]
        public GatheringToolType ToolType;

        [Header("Durability")]
        [Tooltip("최대 내구도. 0이면 파괴되지 않는 영구 도구.")]
        [Min(0)]
        public int MaxDurability = 20;

        [Header("Bonus (호환 노드에만 적용)")]
        [Tooltip("호환 노드에서 채집 시 드롭 수량 배율. 1.5 = 50% 증가.")]
        [Min(1f)]
        public float GatherMultiplier = 1.5f;

        [Tooltip("채집 속도 배율.")]
        [Min(1f)]
        public float SpeedMultiplier = 1.0f;

        [Header("Compatible Node Types — CSV: \"Tree|Rock\" 형식")]
        [Tooltip("이 도구가 보너스를 적용하는 채집 노드 종류 목록.")]
        public ResourceNodeType[] CompatibleNodeTypes;
    }
}
