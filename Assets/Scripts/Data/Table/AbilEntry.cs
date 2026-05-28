using System;
using UnityEngine;

namespace MonsterKitchen.Data
{
    // ====================================================================
    //  AbilEntry — 장비가 플레이어 스탯에 더하는 수치 한 항목
    //
    //  WeaponData.abils 리스트의 원소로 사용한다.
    //  예) [(Attack, +15), (Defense, +3)]
    //      → 장착 시 공격력 +15, 방어력 +3 이 플레이어 기본 스탯에 합산된다.
    //
    //  향후 방어구·악세서리 등 모든 장비가 동일 구조를 사용한다.
    // ====================================================================

    [Serializable]
    public struct AbilEntry
    {
        [Tooltip("더할 능력치 종류")]
        public AbilType AbilType;

        [Tooltip("더할 수치. 공격력·방어력·MaxHp 등은 정수, 속도·확률 등은 소수.")]
        public float Value;

        public AbilEntry(AbilType type, float val)
        {
            AbilType = type;
            Value    = val;
        }
    }
}
