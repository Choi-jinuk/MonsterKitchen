using MonsterKitchen.Core;
using UnityEngine;

namespace MonsterKitchen.Restaurant
{
    /// <summary>
    /// RestaurantScene 진입 시 DayManager.StartDay()를 자동 호출한다.
    /// </summary>
    public class RestaurantOpener : MonoBehaviour
    {
        void Start()
        {
            if (DayManager.Instance != null)
                DayManager.Instance.StartDay();
            else
                DebugUtil.LogWarning("[RestaurantOpener] DayManager 인스턴스 없음.");
        }
    }
}
