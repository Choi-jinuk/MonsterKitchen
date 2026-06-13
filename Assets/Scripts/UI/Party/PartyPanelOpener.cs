using UnityEngine;
using UnityEngine.InputSystem;

namespace MonsterKitchen.UI
{
    // ====================================================================
    //  PartyPanelOpener — 파티 편성 패널 토글 (테스트용 키 트리거)
    //
    //  ManagementScene 에 항상 활성으로 두고, 토글 키로 PartySelectPanel
    //  GameObject 를 켜고 끈다. (New Input System Keyboard)
    // ====================================================================
    public class PartyPanelOpener : MonoBehaviour
    {
        [SerializeField] GameObject m_Panel;
        [SerializeField] Key        m_ToggleKey = Key.P;

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null || m_Panel == null) return;

            if (kb[m_ToggleKey].wasPressedThisFrame)
                m_Panel.SetActive(!m_Panel.activeSelf);
        }
    }
}
