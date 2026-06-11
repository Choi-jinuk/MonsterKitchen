// ====================================================================
//  PooledParticle — ObjectPool<PooledParticle> 용 파티클 래퍼
//
//  ▶ Play(returnCallback) 호출 → ParticleSystem 재생
//  ▶ 재생 완료 후 returnCallback(this) 로 풀에 자동 반환
//  ▶ OnDisable 시 코루틴 중단 (풀 반환 중복 방지)
// ====================================================================

using System;
using System.Collections;
using UnityEngine;

namespace MonsterKitchen.Core
{
    [RequireComponent(typeof(ParticleSystem))]
    public class PooledParticle : MonoBehaviour
    {
        ParticleSystem         m_PS;
        Action<PooledParticle> m_ReturnCallback;

        void Awake()
        {
            m_PS = GetComponent<ParticleSystem>();
        }

        /// <summary>파티클을 재생하고 완료 시 returnCallback 으로 풀에 반환한다.</summary>
        public void Play(Action<PooledParticle> returnCallback)
        {
            m_ReturnCallback = returnCallback;
            if (m_PS != null)
                m_PS.Play();
            StartCoroutine(WaitAndReturn());
        }

        // ── internal ──────────────────────────────────────────────────

        IEnumerator WaitAndReturn()
        {
            yield return null;   // 최소 1프레임 대기 — IsAlive 즉시 false 방지
            while (m_PS != null && m_PS.IsAlive(withChildren: true))
                yield return null;

            m_ReturnCallback?.Invoke(this);
            m_ReturnCallback = null;
        }

        void OnDisable()
        {
            StopAllCoroutines();
            m_ReturnCallback = null;
        }
    }
}
