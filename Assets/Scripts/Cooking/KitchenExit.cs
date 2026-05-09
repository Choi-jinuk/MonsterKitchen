using MonsterKitchen.Core;
using UnityEngine;

namespace MonsterKitchen.Cooking
{
    /// <summary>
    /// 주방 출구 트리거. Player 충돌 시 DungeonScene으로 복귀.
    /// </summary>
    public class KitchenExit : MonoBehaviour
    {
        [SerializeField] string targetScene = "ManagementScene";

        void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;
            SceneLoader.Instance?.LoadScene(targetScene);
        }
    }
}
