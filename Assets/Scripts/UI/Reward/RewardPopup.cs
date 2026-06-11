// ====================================================================
//  RewardPopup — 보상 표시 팝업
//
//  ▶ 진입점 : RewardPopup.Show(title, rewards, onClose)
//  ▶ 확장   : RewardType 추가 → BuildEntryElement switch 케이스 추가
//  ▶ 씬 배치: UIManager.m_PanelDefs 에 (PanelId="RewardPopup", Prefab=프리팹) 등록
//             Inspector: m_Layer=Popup, m_IsPopup=true
// ====================================================================

using System;
using System.Collections.Generic;
using MonsterKitchen.Core;
using MonsterKitchen.Data;
using UnityEngine;
using UnityEngine.UIElements;

namespace MonsterKitchen.UI
{
    /// <summary>
    /// 요리 완성·던전 클리어·퀘스트 등 다양한 보상을 표시하는 팝업.
    ///
    /// 사용 예:
    /// <code>
    ///   RewardPopup.Show("요리 완성!", new[]
    ///   {
    ///       RewardEntry.Food(foodId, 1),
    ///       RewardEntry.Gold(150),
    ///   });
    /// </code>
    /// </summary>
    public class RewardPopup : UIPanel
    {
        // ── 페이로드 ──────────────────────────────────────────────────
        string                     m_Title;
        IReadOnlyList<RewardEntry> m_Rewards;
        Action                     m_OnClose;

        // ── UXML 요소 참조 ────────────────────────────────────────────
        Label         m_TitleLabel;
        VisualElement m_RewardList;
        Label         m_ConfirmBtn;

        // ── UIPanel 훅 ────────────────────────────────────────────────

        protected override void OnFirstOpen()
        {
            m_TitleLabel = Root?.Q<Label>("reward-title");
            m_RewardList = Root?.Q<VisualElement>("reward-list");
            m_ConfirmBtn = Root?.Q<Label>("reward-confirm-btn");

            m_ConfirmBtn?.RegisterCallback<ClickEvent>(_ =>
                UIManager.Instance?.Close(PanelId));

            Root?.Q<VisualElement>("reward-overlay")
                ?.RegisterCallback<ClickEvent>(evt =>
                {
                    if (evt.target is VisualElement ve && ve.name == "reward-overlay")
                        UIManager.Instance?.Close(PanelId);
                });
        }

        // ── 정적 진입점 ───────────────────────────────────────────────

        /// <summary>보상 팝업을 연다.</summary>
        /// <param name="title">팝업 제목 (예: "요리 완성!", "던전 클리어")</param>
        /// <param name="rewards">표시할 보상 목록</param>
        /// <param name="onClose">팝업이 닫힐 때 호출될 콜백 (선택)</param>
        public static void Show(string title, IReadOnlyList<RewardEntry> rewards, Action onClose = null)
        {
            var ui = UIManager.Instance;
            if (ui == null) return;

            var popup = ui.GetPanel<RewardPopup>("RewardPopup");
            if (popup == null)
            {
                DebugUtil.LogWarning("[RewardPopup] RewardPopup 패널을 찾을 수 없습니다. UIManager.m_PanelDefs 에 등록됐는지 확인하세요.");
                return;
            }

            popup.SetPayload(title, rewards, onClose);
            ui.Open("RewardPopup");
        }

        // ── 페이로드 설정 ─────────────────────────────────────────────

        void SetPayload(string title, IReadOnlyList<RewardEntry> rewards, Action onClose)
        {
            m_Title   = title;
            m_Rewards = rewards;
            m_OnClose = onClose;
        }

        // ── UIPanel override ──────────────────────────────────────────

        public override void OnOpen()
        {
            base.OnOpen(); // OnFirstOpen 포함
            Rebuild();
            PlayOpenAnimation();
        }

        public override void OnClose()
        {
            base.OnClose();
            m_OnClose?.Invoke();
            m_OnClose = null;
        }

        // ── UI 구성 ──────────────────────────────────────────────────

        void Rebuild()
        {
            if (m_TitleLabel != null)
                m_TitleLabel.text = m_Title ?? "보상";

            if (m_RewardList == null) return;
            m_RewardList.Clear();

            if (m_Rewards == null || m_Rewards.Count == 0)
            {
                var empty = new Label("보상 없음");
                empty.AddToClassList("reward-empty-msg");
                m_RewardList.Add(empty);
                return;
            }

            foreach (var entry in m_Rewards)
                m_RewardList.Add(BuildEntryElement(entry));
        }

        void PlayOpenAnimation()
        {
            var panel = Root?.Q<VisualElement>("reward-panel");
            if (panel == null) return;

            panel.style.scale   = new StyleScale(new Scale(new UnityEngine.Vector2(0.85f, 0.85f)));
            panel.style.opacity = 0f;
            panel.schedule.Execute(() =>
            {
                panel.style.scale   = new StyleScale(new Scale(UnityEngine.Vector2.one));
                panel.style.opacity = 1f;
            }).StartingIn(16);
        }

        // ── 엔트리 렌더링 ─────────────────────────────────────────────

        /// <summary>
        /// RewardType 별 VisualElement 생성.
        /// 새 타입 추가 시 이 switch 에 케이스 추가.
        /// </summary>
        VisualElement BuildEntryElement(RewardEntry entry) => entry.Type switch
        {
            RewardType.Gold       => BuildRow(null,                       GetGoldLabel(entry),       entry.Amount, "reward-icon-gold"),
            RewardType.Ingredient => BuildRow(GetIngredientSprite(entry.Id), GetIngredientLabel(entry), entry.Amount, null),
            RewardType.Food       => BuildRow(GetFoodSprite(entry.Id),    GetFoodLabel(entry),       entry.Amount, null),
            RewardType.Exp        => BuildRow(null,                       GetExpLabel(entry),        entry.Amount, "reward-icon-exp"),
            RewardType.Item       => BuildRow(null,                       entry.LabelOverride ?? $"아이템 #{entry.Id}", entry.Amount, null),
            _                    => BuildRow(null,                       entry.LabelOverride ?? entry.Type.ToString(), entry.Amount, null),
        };

        static VisualElement BuildRow(Sprite sprite, string label, int amount, string iconClass)
        {
            var row = new VisualElement();
            row.AddToClassList("reward-row");

            var icon = new VisualElement();
            icon.AddToClassList("reward-icon");
            if (sprite != null)
                icon.style.backgroundImage = Background.FromSprite(sprite);
            else if (!string.IsNullOrEmpty(iconClass))
                icon.AddToClassList(iconClass);
            row.Add(icon);

            var nameLbl = new Label(label);
            nameLbl.AddToClassList("reward-entry-name");
            row.Add(nameLbl);

            var amountLbl = new Label(amount > 0 ? $"+{amount}" : amount.ToString());
            amountLbl.AddToClassList("reward-entry-amount");
            if (amount > 0) amountLbl.AddToClassList("reward-entry-amount-positive");
            row.Add(amountLbl);

            return row;
        }

        // ── 데이터 조회 헬퍼 ─────────────────────────────────────────

        static string GetGoldLabel(RewardEntry e)        => e.LabelOverride ?? "골드";
        static string GetExpLabel(RewardEntry e)         => e.LabelOverride ?? "경험치";

        static string GetIngredientLabel(RewardEntry e)
        {
            if (!string.IsNullOrEmpty(e.LabelOverride)) return e.LabelOverride;
            return DataRegistry.Instance?.Ingredients?.Get(e.Id)?.DisplayName ?? $"재료 #{e.Id}";
        }

        static string GetFoodLabel(RewardEntry e)
        {
            if (!string.IsNullOrEmpty(e.LabelOverride)) return e.LabelOverride;
            return DataRegistry.Instance?.Foods?.Get(e.Id)?.DisplayName ?? $"음식 #{e.Id}";
        }

        static Sprite GetIngredientSprite(uint id)
        {
            var d = DataRegistry.Instance?.Ingredients?.Get(id);
            return d != null ? AssetLoadManager.Instance?.Load<Sprite>(d.SpriteAddress) : null;
        }

        static Sprite GetFoodSprite(uint id)
        {
            var d = DataRegistry.Instance?.Foods?.Get(id);
            return d != null ? AssetLoadManager.Instance?.Load<Sprite>(d.SpriteAddress) : null;
        }
    }
}
