using MonsterKitchen.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MonsterKitchen.Management
{
    /// <summary>
    /// 영업 시작 트리거.
    /// Player가 접근해 E키를 누르면 PhaseManager.StartEvening()을 호출해 RestaurantScene으로 전환.
    /// </summary>
    public class EveningStarter : MonoBehaviour
    {
        bool _playerNearby;

        void Update()
        {
            if (!_playerNearby) return;
            if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
            {
                if (PhaseManager.Instance != null)
                    PhaseManager.Instance.StartEvening();
                else
                    SceneLoader.Instance?.LoadScene("KitchenScene");
            }
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (other.CompareTag("Player")) _playerNearby = true;
        }

        void OnTriggerExit2D(Collider2D other)
        {
            if (other.CompareTag("Player")) _playerNearby = false;
        }
    }
}
