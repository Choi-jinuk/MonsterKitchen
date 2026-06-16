using System.Collections.Generic;
using MonsterKitchen.Core;
using MonsterKitchen.Data;
using MonsterKitchen.UI;
using UnityEngine;
using UnityEngine.Localization;
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

        // ── 로컬라이즈 문자열 캐시 ───────────────────────────────────
        static readonly LocalizedString s_LsCookStart     = Loc.Create("UI_COOKING_START");
        static readonly LocalizedString s_LsNoIngredients = Loc.Create("UI_COOKING_NO_INGREDIENTS");
        static readonly LocalizedString s_LsSlotEmpty     = Loc.Create("UI_COOKING_SLOT_EMPTY");
        static readonly LocalizedString s_LsRecipePrefix  = Loc.Create("UI_COOKING_RECIPE_PREFIX");
        static readonly LocalizedString s_LsNoMatch       = Loc.Create("UI_COOKING_NO_MATCH");
        static readonly LocalizedString s_LsSelect        = Loc.Create("UI_COOKING_SELECT");

        // ── 슬롯 상태 ────────────────────────────────────────────────
        readonly List<uint> m_SlotIds = new();
        CookingStation      m_Station;
        RecipeData          m_Matched;
        FoodGrade           m_CurrentGrade;

        // ── UXML 요소 참조 ───────────────────────────────────────────
        VisualElement[]   m_SlotElements;
        VisualElement[]   m_SlotIcons;
        Label[]           m_SlotNames;
        VisualElement     m_InvGrid;
        Label             m_RecipeLabel;
        Label             m_CookBtn;

        // ── UIPanel 훅 ────────────────────────────────────────────────

        protected override void OnFirstOpen()
        {
            // UXML 요소 캐싱 (첫 열기 시 1회)
            m_SlotElements = new VisualElement[MAX_SLOTS];
            m_SlotIcons    = new VisualElement[MAX_SLOTS];
            m_SlotNames    = new Label[MAX_SLOTS];
            for (int i = 0; i < MAX_SLOTS; i++)
            {
                int idx = i;
                m_SlotElements[i] = Root?.Q<VisualElement>($"cook-slot-{i}");
                m_SlotIcons[i]    = Root?.Q<VisualElement>($"cook-slot-icon-{i}");
                m_SlotNames[i]    = Root?.Q<Label>($"cook-slot-name-{i}");

                m_SlotElements[i]?.RegisterCallback<ClickEvent>(_ => RemoveSlot(idx));
            }

            m_InvGrid     = Root?.Q<VisualElement>("cook-inv-grid");
            m_RecipeLabel = Root?.Q<Label>("cook-recipe-label");
            m_CookBtn     = Root?.Q<Label>("cook-btn");

            Root?.Q<Label>("cook-close-btn")
                ?.RegisterCallback<ClickEvent>(_ => UIManager.Instance?.Close(PanelId));

            Root?.Q<VisualElement>("cook-overlay")
                ?.RegisterCallback<ClickEvent>(evt =>
                {
                    if (evt.target is VisualElement ve && ve.name == "cook-overlay")
                        UIManager.Instance?.Close(PanelId);
                });

            m_CookBtn?.RegisterCallback<ClickEvent>(_ => TryCook());
            if (m_CookBtn != null) m_CookBtn.text = s_LsCookStart.GetLocalizedString();
        }

        // ── 외부 진입점 ──────────────────────────────────────────────

        /// <summary>CookingStation에서 호출. 스테이션 참조 저장 후 패널 열기.</summary>
        public void OpenFor(CookingStation station)
        {
            m_Station = station;
            UIManager.Instance?.Open(PanelId);
        }

        // ── UIPanel override ─────────────────────────────────────────

        public override void OnOpen()
        {
            m_SlotIds.Clear();
            base.OnOpen(); // OnFirstOpen 포함
            if (PlayerDataManager.Instance?.Inventory != null)
                PlayerDataManager.Instance.Inventory.OnIngredientChanged += HandleIngredientChanged;
            RefreshAll();
        }

        public override void OnClose()
        {
            if (PlayerDataManager.Instance?.Inventory != null)
                PlayerDataManager.Instance.Inventory.OnIngredientChanged -= HandleIngredientChanged;
            m_Station = null;
            base.OnClose();
        }

        void HandleIngredientChanged(uint id, int qty) => BuildInventoryGrid();

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
                m_InvGrid.Add(EmptyMsg(s_LsNoIngredients.GetLocalizedString()));
                return;
            }

            var registry = DataRegistry.Instance;
            var loader   = AssetLoadManager.Instance;
            foreach (var kv in all)
            {
                if (kv.Value <= 0) continue;
                var d      = registry?.Ingredients?.Get(kv.Key);
                var sprite = loader?.Load<Sprite>(d?.SpriteAddress);
                m_InvGrid.Add(BuildInvSlot(sprite, kv.Value.ToString(), d?.DisplayName ?? kv.Key.ToString(), kv.Key));
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
                var se = m_SlotElements?[i];
                var si = m_SlotIcons?[i];
                var sn = m_SlotNames?[i];

                if (se == null) continue;

                se.RemoveFromClassList("cook-slot-empty");
                se.RemoveFromClassList("cook-slot-filled");

                if (filled)
                {
                    uint id    = m_SlotIds[i];
                    var d      = DataRegistry.Instance?.Ingredients?.Get(id);
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
                    if (sn != null) sn.text = s_LsSlotEmpty.GetLocalizedString();
                    se.AddToClassList("cook-slot-empty");
                }
            }
        }

        void RefreshRecipeMatch()
        {
            if (m_RecipeLabel == null || m_CookBtn == null) return;

            var all = DataRegistry.Instance?.Recipes?.All;
            if (all == null && m_SlotIds.Count > 0) return;

            m_Matched = m_SlotIds.Count > 0
                ? RecipeMatcher.FindSlotMatch(all, m_SlotIds)
                : null;

            if (m_Matched != null)
            {
                m_CurrentGrade    = PlayerDataManager.Instance?.Inventory
                                        .CalculateCookingGrade(m_Matched) ?? FoodGrade.Normal;
                string gradeSuffix = $"  [{m_CurrentGrade}]";
                m_RecipeLabel.text = s_LsRecipePrefix.GetLocalizedString()
                                     + m_Matched.DisplayName + gradeSuffix;
                m_RecipeLabel.RemoveFromClassList("cook-recipe-matched");
                m_RecipeLabel.AddToClassList("cook-recipe-matched");
                m_CookBtn.RemoveFromClassList("cook-btn-disabled");
                m_CookBtn.AddToClassList("cook-btn-enabled");
            }
            else
            {
                m_CurrentGrade = FoodGrade.Normal;
                m_RecipeLabel.text = m_SlotIds.Count > 0
                    ? s_LsNoMatch.GetLocalizedString()
                    : s_LsSelect.GetLocalizedString();
                m_RecipeLabel.RemoveFromClassList("cook-recipe-matched");
                m_CookBtn.RemoveFromClassList("cook-btn-enabled");
                m_CookBtn.AddToClassList("cook-btn-disabled");
            }
        }

        // ── 슬롯 조작 ────────────────────────────────────────────────

        void AddIngredientToSlot(uint id)
        {
            if (m_SlotIds.Count >= MAX_SLOTS) return;

            var inv = PlayerDataManager.Instance?.Inventory;
            if (inv == null) return;

            int alreadySlotted = 0;
            foreach (var slotId in m_SlotIds)
                if (slotId == id) alreadySlotted++;

            if (!inv.HasIngredient(id, alreadySlotted + 1)) return;

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

            if (!RecipeMatcher.CanCook(m_Matched, PlayerDataManager.Instance?.Inventory))
            {
                GameHUD.Instance?.ShowNotification(s_LsNoIngredients.GetLocalizedString(), 2f);
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
