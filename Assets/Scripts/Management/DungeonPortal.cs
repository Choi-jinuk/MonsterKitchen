using MonsterKitchen.Core;
using UnityEngine;

namespace MonsterKitchen.Management
{
    /// <summary>
    /// 던전 입장 포털.
    /// Player 충돌 시 PhaseManager.EnterDungeon()을 호출해 DungeonScene으로 전환.
    /// </summary>
    public class DungeonPortal : MonoBehaviour
    {
        void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;

            if (PhaseManager.Instance != null)
                PhaseManager.Instance.EnterDungeon();
            else
                SceneLoader.Instance?.LoadScene("DungeonScene");
        }
    }
}
