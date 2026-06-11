// ====================================================================
//  VFXManager — 히트·사망·요리완성 파티클 풀 관리 싱글톤
//
//  ▶ ManagementScene 의 오브젝트에 배치 → DontDestroyOnLoad
//  ▶ AssetManifest 에 등록된 PooledParticle 프리팹을 로드해 3개의 풀 구성
//    키: AssetKeys.PREFAB_VFX_HIT / DEATH / COOK_COMPLETE
//  ▶ 프리팹 미등록 시 LogWarning 후 해당 풀 null 유지 (기능 skip)
// ====================================================================

using MonsterKitchen.Core;
using UnityEngine;

namespace MonsterKitchen.Core
{
    public class VFXManager : MonoBehaviour
    {
        public static VFXManager Instance { get; private set; }

        const int InitialPoolSize = 4;
        const int MaxPoolSize     = 16;

        ObjectPool<PooledParticle> m_HitPool;
        ObjectPool<PooledParticle> m_DeathPool;
        ObjectPool<PooledParticle> m_CookCompletePool;

        // ── 수명 ──────────────────────────────────────────────────────

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void Start()
        {
            m_HitPool         = BuildPool(AssetKeys.PREFAB_VFX_HIT,          "VFX_Hit");
            m_DeathPool       = BuildPool(AssetKeys.PREFAB_VFX_DEATH,         "VFX_Death");
            m_CookCompletePool = BuildPool(AssetKeys.PREFAB_VFX_COOK_COMPLETE, "VFX_CookComplete");
        }

        // ── 공개 API ──────────────────────────────────────────────────

        /// <summary>히트 이펙트를 worldPos 에 재생한다.</summary>
        public void PlayHit(Vector3 worldPos)         => Spawn(m_HitPool,          worldPos);

        /// <summary>사망 이펙트를 worldPos 에 재생한다.</summary>
        public void PlayDeath(Vector3 worldPos)       => Spawn(m_DeathPool,        worldPos);

        /// <summary>요리 완성 이펙트를 worldPos 에 재생한다.</summary>
        public void PlayCookComplete(Vector3 worldPos) => Spawn(m_CookCompletePool, worldPos);

        // ── internal ──────────────────────────────────────────────────

        void Spawn(ObjectPool<PooledParticle> pool, Vector3 pos)
        {
            if (pool == null) return;
            var particle = pool.Get(pos);
            particle.Play(p => pool.Return(p));
        }

        ObjectPool<PooledParticle> BuildPool(string key, string containerName)
        {
            var prefab = AssetLoadManager.Instance?.Load<PooledParticle>(key);
            if (prefab == null)
            {
                DebugUtil.LogWarning($"[VFXManager] 프리팹 없음 — 키: {key}. AssetManifest 에 등록 후 사용 가능.");
                return null;
            }

            var container = new GameObject(containerName);
            container.transform.SetParent(transform);
            return new ObjectPool<PooledParticle>(prefab, container.transform, InitialPoolSize, MaxPoolSize);
        }
    }
}
