using System;
using UnityEngine;

namespace MonsterKitchen.Combat
{
    /// <summary>
    /// 체력 컴포넌트 — 플레이어/몬스터 공용.
    /// </summary>
    public class Health : MonoBehaviour
    {
        [SerializeField] int m_DefaultHp = 100;

        public int   MaxHp        { get; private set; }
        public int   CurrentHp   { get; private set; }
        public bool  IsDead      => CurrentHp <= 0;
        /// <summary>true 동안 TakeDamage 를 무시한다 (대시 무적 등).</summary>
        public bool  IsInvincible { get; set; }

        public event Action<int, int>     OnHpChanged;   // (current, max)
        public event Action<AttributeType> OnDeath;       // 막타 속성 전달
        public event Action<int, AttributeType> OnDamaged; // (amount, attackAttr)

        void Awake()
        {
            MaxHp     = m_DefaultHp;
            CurrentHp = m_DefaultHp;
        }

        public void SetMaxHp(int value)
        {
            MaxHp     = value;
            CurrentHp = Mathf.Min(CurrentHp, MaxHp);
            OnHpChanged?.Invoke(CurrentHp, MaxHp);
        }

        /// <summary>데미지 적용. attackAttribute = 막타 속성.</summary>
        public void TakeDamage(int amount, AttributeType attackAttribute = AttributeType.None)
        {
            if (IsDead || IsInvincible) return;

            int actual = Mathf.Max(1, amount);
            CurrentHp  = Mathf.Max(0, CurrentHp - actual);

            OnDamaged?.Invoke(actual, attackAttribute);
            OnHpChanged?.Invoke(CurrentHp, MaxHp);

            if (CurrentHp <= 0)
                OnDeath?.Invoke(attackAttribute);
        }

        public void Heal(int amount)
        {
            if (IsDead) return;
            CurrentHp = Mathf.Min(MaxHp, CurrentHp + amount);
            OnHpChanged?.Invoke(CurrentHp, MaxHp);
        }
    }
}
