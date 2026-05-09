using MonsterKitchen.Core;
using UnityEngine;

namespace MonsterKitchen.Restaurant
{
    /// <summary>
    /// RestaurantScene 진입 시 DayManager에 씬 로컬 레퍼런스를 주입한다.
    ///
    /// - customerPrefab : AssetLoadManager 경유 로드 (Inspector 참조 제거)
    /// - guestSpawnPoint, tables : 씬 배치 오브젝트이므로 SerializeField 유지
    /// </summary>
    public class RestaurantSetup : MonoBehaviour
    {
        [SerializeField] Transform         guestSpawnPoint;
        [SerializeField] RestaurantTable[] tables;

        void Awake()
        {
            if (DayManager.Instance == null)
            {
                Debug.LogWarning("[RestaurantSetup] DayManager 인스턴스 없음 — ManagementScene부터 시작하세요.");
                return;
            }

            var customerPrefab = AssetLoadManager.Instance?.Load<CustomerAI>(AssetKeys.PrefabCustomer);
            if (customerPrefab == null)
                Debug.LogError("[RestaurantSetup] Customer 프리팹을 AssetManifest에서 찾을 수 없습니다. 키: " + AssetKeys.PrefabCustomer);

            DayManager.Instance.SetRestaurantConfig(guestSpawnPoint, customerPrefab, tables);
            Debug.Log("[RestaurantSetup] DayManager 레퍼런스 주입 완료.");
        }
    }
}
