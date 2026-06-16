using MonsterKitchen.Core;
using MonsterKitchen.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace MonsterKitchen.Dungeon
{
    // ====================================================================
    //  DungeonExit — 던전 출구. 항상 활성. E키 상호작용 (InteractionHub).
    //
    //  ▶ E키 (Interact):
    //    - 가방 비어있거나 꽉 참 → 즉시 귀환
    //    - 가방 여유 있음 → 확인 팝업 표시
    //  ▶ 귀환: DungeonBag.FlushToInventory() → SceneLoader.LoadScene("ManagementScene")
    // ====================================================================

    public class DungeonExit : InteractableBehaviour
    {
        bool          m_PopupShown;
        VisualElement m_PopupRoot;

        // ── IInteractable ────────────────────────────────────────────

        public override bool CanInteract => !m_PopupShown;

        public override void Interact()
        {
            var bag = DungeonBag.Current;

            // 가방 없거나, 비어있거나, 꽉 찼으면 즉시 귀환
            if (bag == null || bag.CurrentWeight == 0 || bag.IsFull)
            {
                DoReturn();
                return;
            }

            // 가방 여유 있음 → 확인 팝업
            ShowConfirmPopup(bag.CurrentWeight, bag.MaxWeight);
        }

        protected override void OnPlayerExit() => HidePopup();

        // ── 귀환 ─────────────────────────────────────────────────────

        void DoReturn()
        {
            HidePopup();
            DungeonBag.Current?.FlushToInventory();
            DebugUtil.Log("[DungeonExit] 귀환 → ManagementScene");
            SceneLoader.Instance?.LoadScene(CommonString.SceneManagement);
        }

        // ── 확인 팝업 (코드 생성) ─────────────────────────────────────

        void ShowConfirmPopup(int current, int max)
        {
            m_PopupShown = true;

            var hud = GameHUD.Instance;
            if (hud == null) { DoReturn(); return; }

            var hudRoot = hud.GetComponent<UnityEngine.UIElements.UIDocument>()
                            ?.rootVisualElement;
            if (hudRoot == null) { DoReturn(); return; }

            m_PopupRoot                             = new VisualElement();
            m_PopupRoot.style.position              = Position.Absolute;
            m_PopupRoot.style.top                   = 0;
            m_PopupRoot.style.left                  = 0;
            m_PopupRoot.style.right                 = 0;
            m_PopupRoot.style.bottom                = 0;
            m_PopupRoot.style.backgroundColor       = new StyleColor(new Color(0f, 0f, 0f, 0.55f));
            m_PopupRoot.style.alignItems            = Align.Center;
            m_PopupRoot.style.justifyContent        = Justify.Center;

            var box = new VisualElement();
            box.style.backgroundColor               = new StyleColor(new Color(0.12f, 0.12f, 0.16f, 0.97f));
            box.style.borderTopLeftRadius           = 12;
            box.style.borderTopRightRadius          = 12;
            box.style.borderBottomLeftRadius        = 12;
            box.style.borderBottomRightRadius       = 12;
            box.style.paddingTop                    = 24;
            box.style.paddingBottom                 = 24;
            box.style.paddingLeft                   = 32;
            box.style.paddingRight                  = 32;
            box.style.minWidth                      = 320;
            box.style.alignItems                    = Align.Center;

            var msg = new Label($"가방에 여유가 있습니다. ({current}/{max})\n귀환하시겠습니까?");
            msg.style.fontSize                      = 18;
            msg.style.color                         = new StyleColor(Color.white);
            msg.style.whiteSpace                    = WhiteSpace.Normal;
            msg.style.unityTextAlign                = TextAnchor.MiddleCenter;
            msg.style.marginBottom                  = 20;
            box.Add(msg);

            var btnRow = new VisualElement();
            btnRow.style.flexDirection              = FlexDirection.Row;
            btnRow.style.justifyContent             = Justify.Center;

            var btnReturn = new Button(DoReturn) { text = "귀  환" };
            btnReturn.style.width                   = 100;
            btnReturn.style.height                  = 36;
            btnReturn.style.marginRight             = 12;
            btnReturn.style.fontSize                = 16;
            btnReturn.style.backgroundColor         = new StyleColor(new Color(0.25f, 0.55f, 0.85f));
            btnReturn.style.color                   = new StyleColor(Color.white);
            btnReturn.style.borderTopLeftRadius     = 6;
            btnReturn.style.borderTopRightRadius    = 6;
            btnReturn.style.borderBottomLeftRadius  = 6;
            btnReturn.style.borderBottomRightRadius = 6;

            var btnCancel = new Button(HidePopup) { text = "취  소" };
            btnCancel.style.width                   = 100;
            btnCancel.style.height                  = 36;
            btnCancel.style.fontSize                = 16;
            btnCancel.style.backgroundColor         = new StyleColor(new Color(0.35f, 0.35f, 0.35f));
            btnCancel.style.color                   = new StyleColor(Color.white);
            btnCancel.style.borderTopLeftRadius     = 6;
            btnCancel.style.borderTopRightRadius    = 6;
            btnCancel.style.borderBottomLeftRadius  = 6;
            btnCancel.style.borderBottomRightRadius = 6;

            btnRow.Add(btnReturn);
            btnRow.Add(btnCancel);
            box.Add(btnRow);
            m_PopupRoot.Add(box);
            hudRoot.Add(m_PopupRoot);
        }

        void HidePopup()
        {
            m_PopupShown = false;
            m_PopupRoot?.RemoveFromHierarchy();
            m_PopupRoot = null;
        }
    }
}
