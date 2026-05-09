using MonsterKitchen.Core;
using UnityEngine;

namespace MonsterKitchen.Dungeon
{
    /// <summary>
    /// 던전 출구 트리거. 방 클리어 후 활성화된다.
    /// Player가 충돌하면 PhaseManager.ReturnFromDungeon()으로 ManagementScene으로 복귀.
    /// </summary>
    public class DungeonExit : MonoBehaviour
    {
        void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;
            if (PhaseManager.Instance != null)
                PhaseManager.Instance.ReturnFromDungeon();
            else
                SceneLoader.Instance?.LoadScene("ManagementScene");
        }
    }
}
