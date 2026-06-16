using MonsterKitchen.Core;
using UnityEngine;

namespace MonsterKitchen.Management
{
    /// <summary>
    /// 던전 입장 포털.
    /// 플레이어가 Trigger 안에 있는 동안 E키를 누르면 DungeonScene으로 전환.
    /// InteractionPrompt 컴포넌트가 있으면 "[E]" 말풍선을 자동으로 표시한다.
    /// </summary>
    public class DungeonPortal : InteractableBehaviour
    {
        public override void Interact()
        {
            if (PhaseManager.Instance != null)
                PhaseManager.Instance.EnterDungeon();
            else
                SceneLoader.Instance?.LoadScene(CommonString.SceneDungeon);
        }
    }
}
