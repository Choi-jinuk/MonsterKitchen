using MonsterKitchen.Core;
using UnityEngine;

namespace MonsterKitchen.Cooking
{
    /// <summary>
    /// 주방에서 식당으로 이동하는 트리거.
    /// Player 충돌 시 RestaurantScene 로드.
    /// </summary>
    public class RestaurantEntry : MonoBehaviour
    {
        [SerializeField] string targetScene = "RestaurantScene";

        void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;
            SceneLoader.Instance?.LoadScene(targetScene);
        }
    }
}
