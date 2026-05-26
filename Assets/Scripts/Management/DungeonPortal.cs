using MonsterKitchen.Core;
using UnityEngine;

namespace MonsterKitchen.Management
{
    /// <summary>
    /// 던전 입장 포털.
    /// 플레이어가 Trigger 안에 있는 동안 E키를 누르면 DungeonScene으로 전환.
    /// InteractionPrompt 컴포넌트가 있으면 "[E]" 말풍선을 자동으로 표시한다.
    /// </summary>
    public class DungeonPortal : MonoBehaviour
    {
        // ── Trigger 감지 ──────────────────────────────────────────────────

        void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;
            if (InputManager.Instance != null)
                InputManager.Instance.OnInteract += TryEnter;
        }

        void OnTriggerExit2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;
            if (InputManager.Instance != null)
                 InputManager.Instance.OnInteract -= TryEnter;
        }

        void OnDisable()
        {
            // 씬 전환 등으로 비활성화될 때 구독 해제
            if (InputManager.Instance != null)
                InputManager.Instance.OnInteract -= TryEnter;
        }

        // ── 실행 ─────────────────────────────────────────────────────────

        void TryEnter()
        {
            if (PhaseManager.Instance != null)
                PhaseManager.Instance.EnterDungeon();
            else
                SceneLoader.Instance?.LoadScene("DungeonScene");
        }
    }
}
