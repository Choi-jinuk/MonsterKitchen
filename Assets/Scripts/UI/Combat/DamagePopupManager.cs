using MonsterKitchen.Core;
using UnityEngine;

namespace MonsterKitchen.UI
{
    /// <summary>
    /// 데미지 팝업 풀을 관리하는 싱글톤 매니저.
    /// ManagementScene의 UIManager 오브젝트 등에 배치, DontDestroyOnLoad.
    ///
    /// 사용법 (코드에서 직접 호출):
    ///   DamagePopupManager.Instance.ShowPopup(worldPos, amount, isCrit);
    ///
    /// 또는 Health.OnDamaged 이벤트에 자동 구독하려면
    /// 엔티티에 DamagePopupTrigger 컴포넌트를 추가한다.
    /// </summary>
    public class DamagePopupManager : MonoBehaviour
    {
        public static DamagePopupManager Instance { get; private set; }

        const int InitialPoolSize = 10;
        const int MaxPoolSize     = 30;

        ObjectPool<DamagePopup> m_Pool;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void Start()
        {
            var prefab = AssetLoadManager.Instance?.Load<DamagePopup>(AssetKeys.PREFAB_DAMAGE_POPUP);
            if (prefab == null)
            {
                DebugUtil.LogError("[DamagePopupManager] DamagePopup 프리팹을 AssetManifest에서 찾을 수 없습니다. 키: " + AssetKeys.PREFAB_DAMAGE_POPUP);
                return;
            }
            m_Pool = new ObjectPool<DamagePopup>(prefab, transform, InitialPoolSize, MaxPoolSize);
        }

        /// <summary>지정 위치에 데미지 팝업을 생성한다.</summary>
        /// <param name="worldPos">팝업 생성 월드 좌표</param>
        /// <param name="amount">표시할 데미지 수치</param>
        /// <param name="isCrit">크리티컬 여부 (주황색, 큰 글씨)</param>
        public void ShowPopup(Vector3 worldPos, int amount, bool isCrit = false)
        {
            if (m_Pool == null)
            {
                DebugUtil.LogWarning("[DamagePopupManager] 풀이 초기화되지 않았습니다.");
                return;
            }

            // 살짝 랜덤 오프셋으로 겹침 방지
            Vector3 offset = new Vector3(RandomUtil.Range(-0.2f, 0.2f), 0.3f, 0f);
            var popup = m_Pool.Get(worldPos + offset);
            popup.Show(amount, isCrit, this);
        }

        /// <summary>DamagePopup 이 애니메이션 완료 후 호출한다.</summary>
        public void ReturnToPool(DamagePopup popup)
        {
            m_Pool.Return(popup);
        }
    }
}
