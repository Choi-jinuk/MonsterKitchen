using MonsterKitchen.Core;
using UnityEngine;

namespace MonsterKitchen.Management
{
    /// <summary>
    /// 영업 시작 트리거.
    /// 플레이어가 Trigger 안에 있는 동안 Interact 키를 누르면 PhaseManager.StartEvening() 호출.
    /// </summary>
    public class EveningStarter : MonoBehaviour
    {
        void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;
            if (InputManager.Instance != null)
                InputManager.Instance.OnInteract += TryStart;
        }

        void OnTriggerExit2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;
            if (InputManager.Instance != null)
                InputManager.Instance.OnInteract -= TryStart;
        }

        void OnDisable()
        {
            if (InputManager.Instance != null)
                InputManager.Instance.OnInteract -= TryStart;
        }

        void TryStart()
        {
            if (PhaseManager.Instance != null)
                PhaseManager.Instance.StartEvening();
            else
                SceneLoader.Instance?.LoadScene("KitchenScene");
        }
    }
}
