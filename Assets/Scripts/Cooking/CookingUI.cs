using System.Collections.Generic;
using MonsterKitchen.Core;
using MonsterKitchen.Data;
using MonsterKitchen.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace MonsterKitchen.Cooking
{
    /// <summary>
    /// 조리 UI 패널. UIPanel(isPopup=true, layer=Panel) 상속.
    ///
    /// - Space 키    : CookingStation → OpenFor(station)
    /// - 인벤토리 클릭 : 재료 슬롯에 추가 (최대 3칸)
    /// - 슬롯 클릭   : 재료 제거
    /// - 요리 버튼   : 슬롯 매칭 레시피로 station.CookRecipe() 호출 → 닫기
    /// - ✕ / 플레이어 이탈 : UIManager.Close("CookingUI")
    /// </summary>
    public class CookingUI : UIPanel
    {
        const int MAX_SLOTS = 3;

        // ── 슬롯 상태 ────────────────────────────────────────────────
        readonly List<uint>         m_SlotIds  = new();   // 현재 슬롯 재료 ID
        CookingStation              m_Station;
        RecipeData                  m_Matched;

        // ── UXML 요소 참조 ───────────────────────────────────────────
        VisualElement[]   m_SlotElements;     // cook-slot-0~2
        VisualElement[]   m_SlotIcons;        // cook-slot-icon-0~2
        Label[]           m_SlotNames;        // cook-slot-name-0~2
        VisualElement     m_InvGrid;
        Label             m_RecipeLabel;
        Label             m_CookBtn;

        // ── Unity 생명주기 ────────────────────────────────────────────

        protected override void Awake()
        {
            base.Awake();
        }

        protected override void Start()
        {
            base.Start(); // UIManager.Register

            // 슬롯 요소 캐싱
            m_SlotElements = new VisualElement[MAX_SLOTS];
            m_SlotIcons    = new VisualElement[MAX_SLOTS];
            m_SlotNames    = new Label[MAX_SLOTS];
            for (int i = 0; i < MAX_SLOTS; i++)
            {
                int idx = i; // 클로저 캡처
                m_SlotElements[i] = Root?.Q<VisualElement>($"cook-slot-{i}");
                m_SlotIcons[i]    = Root?.Q<VisualElement>($"cook-slot-icon-{i}");
                m_SlotNames[i]    = Root?.Q<Label>($"cook-slot-name-{i}");

                // 슬롯 클릭 → 재료 제거
                m_SlotElements[i]?.RegisterCallback<ClickEvent>(_ => RemoveSlot(idx));
            }

            m_InvGrid     = Root?.Q<VisualElement>("cook-inv-grid");
            m_RecipeLabel = Root?.Q<Label>("cook-recipe-label");
            m_CookBtn     = Root?.Q<Label>("cook-btn");

            // ✕ 버튼
            Root?.Q<Label>("cook-close-btn")
                ?.RegisterCallback<ClickEvent>(_ => UIManager.Instance?.Close(PanelId));

            // 오버레이 클릭 닫기
            Root?.Q<VisualElement>("cook-overlay")
                ?.RegisterCallback<ClickEvent>(evt =>
                {
                    if (evt.target is VisualElement ve && ve.name == "cook-overlay")
                        UIManager.Instance?.Close(PanelId);
                });

            // 요리 버튼 클릭
            m_CookBtn?.RegisterCallback<ClickEvent>(_ => TryCook());

            // 인벤토리 변경 → 열려 있을 때만 갱신
            if (PlayerDataManager.Instance?.Inventory != null)
                PlayerDataManager.Instance.Inventory.OnIngredientChanged += (_, _) => { if (IsOpen) BuildInventoryGrid(); };
        }

        // ── 외부 진입점 ──────────────────────────────────────────────

        /// <summary>CookingStation에서 호출. 스테이션 참조를 저장하고 패널을 연다.</summary>
        public void OpenFor(CookingStation station)
        {
            m_Station = station;
            UIManager.Instance?.Open(PanelId);
        }

        // ── UIPanel override ─────────────────────────────────────────

        public override void OnOpen()
        {
            m_SlotIds.Clear();
            base.OnOpen();
            RefreshAll();
        }

        public override void OnClose()
        {
            m_Station = null;
            base.OnClose();
        }

        // ── UI 갱신 ──────────────────────────────────────────────────

        void RefreshAll()
        {
            BuildInventoryGrid();
            RefreshSlotDisplay();
            RefreshRecipeMatch();
        }

        void BuildInventoryGrid()
        {
            if (m_InvGrid == null) return;
            m_InvGrid.Clear();

            var all = PlayerDataManager.Instance?.Inventory.AllIngredients;
            if (all == null || all.Count == 0)
            {
                m_InvGrid.Add(EmptyMsg("재료가 없습니다."));
                return;
            }

            var registry = DataRegistry.Instance;
            var loader   = AssetLoadManager.Instance;
            foreach (var kv in all)
            {
                if (kv.Value <= 0) continue;
                var d      = registry?.GetIngredient(kv.Key);
                var sprite = loader?.Load<Sprite>(d?.SpriteAddress);
                var wrap   = BuildInvSlot(sprite, kv.Value.ToString(), d?.DisplayName ?? kv.Key.ToString(), kv.Key);
                m_InvGrid.Add(wrap);
            }
        }

        VisualElement BuildInvSlot(Sprite sprite, string count, string name, uint ingredientId)
        {
            var wrap = new VisualElement();
            wrap.style.alignItems = Align.Center;

            var slot = new VisualElement();
            slot.AddToClassList("cook-inv-slot");

            var icon = new VisualElement();
            icon.AddToClassList("cook-inv-slot-icon");
            if (sprite != null) icon.style.backgroundImage = Background.FromSprite(sprite);
            slot.Add(icon);

            var countLbl = new Label(count);
            countLbl.AddToClassList("cook-inv-slot-count");
            slot.Add(countLbl);

            // 클릭 → 슬롯에 추가
            slot.RegisterCallback<ClickEvent>(_ => AddIngredientToSlot(ingredientId));

            var nameLbl = new Label(name);
            nameLbl.AddToClassList("cook-inv-slot-name");

            wrap.Add(slot);
            wrap.Add(nameLbl);
            return wrap;
        }

        void RefreshSlotDisplay()
        {
            for (int i = 0; i < MAX_SLOTS; i++)
            {
                bool filled = i < m_SlotIds.Count;
                var se = m_SlotElements[i];
                var si = m_SlotIcons[i];
                var sn = m_SlotNames[i];

                if (se == null) continue;

                se.RemoveFromClassList("cook-slot-empty");
                se.RemoveFromClassList("cook-slot-filled");

                if (filled)
                {
                    uint id    = m_SlotIds[i];
                    var d      = DataRegistry.Instance?.GetIngredient(id);
                    var sprite = AssetLoadManager.Instance?.Load<Sprite>(d?.SpriteAddress);

                    if (si != null)
                        si.style.backgroundImage = sprite != null
                            ? Background.FromSprite(sprite)
                            : StyleKeyword.None;

                    if (sn != null) sn.text = d?.DisplayName ?? id.ToString();
                    se.AddToClassList("cook-slot-filled");
                }
                else
                {
                    if (si != null) si.style.backgroundImage = StyleKeyword.None;
                    if (sn != null) sn.text = "비어 있음";
                    se.AddToClassList("cook-slot-empty");
                }
            }
        }

        void RefreshRecipeMatch()
        {
            if (m_RecipeLabel == null || m_CookBtn == null) return;

            m_Matched = (m_SlotIds.Count > 0)
                ? RecipeMatcher.FindSlotMatch(DataRegistry.Instance?.AllRecipes, m_SlotIds)
                : null;

            if (m_Matched != null)
            {
                m_RecipeLabel.text = $"레시피: {m_Matched.DisplayName}";
                m_RecipeLabel.RemoveFromClassList("cook-recipe-matched");
                m_RecipeLabel.AddToClassList("cook-recipe-matched");

                m_CookBtn.RemoveFromClassList("cook-btn-disabled");
                m_CookBtn.AddToClassList("cook-btn-enabled");
            }
            else
            {
                m_RecipeLabel.text = m_SlotIds.Count > 0 ? "재료 조합이 맞지 않습니다" : "재료를 선택하세요";
                m_RecipeLabel.RemoveFromClassList("cook-recipe-matched");

                m_CookBtn.RemoveFromClassList("cook-btn-enabled");
                m_CookBtn.AddToClassList("cook-btn-disabled");
            }
        }

        // ── 슬롯 조작 ────────────────────────────────────────────────

        void AddIngredientToSlot(uint id)
        {
            if (m_SlotIds.Count >= MAX_SLOTS) return;
            if (PlayerDataManager.Instance?.Inventory == null ||
                !PlayerDataManager.Instance.Inventory.HasIngredient(id, 1)) return;

            m_SlotIds.Add(id);
            RefreshSlotDisplay();
            RefreshRecipeMatch();
        }

        void RemoveSlot(int index)
        {
            if (index < 0 || index >= m_SlotIds.Count) return;
            m_SlotIds.RemoveAt(index);
            RefreshSlotDisplay();
            RefreshRecipeMatch();
        }

        // ── 요리 실행 ────────────────────────────────────────────────

        void TryCook()
        {
            if (m_Matched == null || m_Station == null) return;

            // 슬롯 재료가 인벤토리에 있는지 재확인
            if (!RecipeMatcher.CanCook(m_Matched, PlayerDataManager.Instance?.Inventory))
            {
                m_RecipeLabel.text = "재료가 부족합니다!";
                return;
            }

            m_Station.CookRecipe(m_Matched);
            UIManager.Instance?.Close(PanelId);
        }

        // ── 헬퍼 ─────────────────────────────────────────────────────

        static Label EmptyMsg(string msg)
        {
            var lbl = new Label(msg);
            lbl.AddToClassList("cook-empty-msg");
            return lbl;
        }
    }
}
