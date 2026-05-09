using System.Collections.Generic;
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
        const int MaxSlots = 3;

        // ── 슬롯 상태 ────────────────────────────────────────────────
        readonly List<string>       _slotIds  = new();   // 현재 슬롯 재료 ID
        CookingStation              _station;
        RecipeData                  _matched;

        // ── UXML 요소 참조 ───────────────────────────────────────────
        VisualElement[]   _slotElements;     // cook-slot-0~2
        VisualElement[]   _slotIcons;        // cook-slot-icon-0~2
        Label[]           _slotNames;        // cook-slot-name-0~2
        VisualElement     _invGrid;
        Label             _recipeLabel;
        Label             _cookBtn;

        // ── Unity 생명주기 ────────────────────────────────────────────

        protected override void Awake()
        {
            base.Awake();
        }

        protected override void Start()
        {
            base.Start(); // UIManager.Register

            // 슬롯 요소 캐싱
            _slotElements = new VisualElement[MaxSlots];
            _slotIcons    = new VisualElement[MaxSlots];
            _slotNames    = new Label[MaxSlots];
            for (int i = 0; i < MaxSlots; i++)
            {
                int idx = i; // 클로저 캡처
                _slotElements[i] = Root?.Q<VisualElement>($"cook-slot-{i}");
                _slotIcons[i]    = Root?.Q<VisualElement>($"cook-slot-icon-{i}");
                _slotNames[i]    = Root?.Q<Label>($"cook-slot-name-{i}");

                // 슬롯 클릭 → 재료 제거
                _slotElements[i]?.RegisterCallback<ClickEvent>(_ => RemoveSlot(idx));
            }

            _invGrid      = Root?.Q<VisualElement>("cook-inv-grid");
            _recipeLabel  = Root?.Q<Label>("cook-recipe-label");
            _cookBtn      = Root?.Q<Label>("cook-btn");

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
            _cookBtn?.RegisterCallback<ClickEvent>(_ => TryCook());

            // 인벤토리 변경 → 열려 있을 때만 갱신
            if (Inventory.Instance != null)
                Inventory.Instance.OnItemChanged += (_, _) => { if (IsOpen) BuildInventoryGrid(); };
        }

        // ── 외부 진입점 ──────────────────────────────────────────────

        /// <summary>CookingStation에서 호출. 스테이션 참조를 저장하고 패널을 연다.</summary>
        public void OpenFor(CookingStation station)
        {
            _station = station;
            UIManager.Instance?.Open(PanelId);
        }

        // ── UIPanel override ─────────────────────────────────────────

        public override void OnOpen()
        {
            _slotIds.Clear();
            base.OnOpen();
            RefreshAll();
        }

        public override void OnClose()
        {
            _station = null;
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
            if (_invGrid == null) return;
            _invGrid.Clear();

            var all = Inventory.Instance?.All;
            if (all == null || all.Count == 0)
            {
                _invGrid.Add(EmptyMsg("재료가 없습니다."));
                return;
            }

            var registry = DataRegistry.Instance;
            foreach (var kv in all)
            {
                if (kv.Value <= 0) continue;
                var d   = registry?.GetIngredient(kv.Key);
                var wrap = BuildInvSlot(d?.sprite, kv.Value.ToString(), d != null ? d.displayName : kv.Key, kv.Key);
                _invGrid.Add(wrap);
            }
        }

        VisualElement BuildInvSlot(Sprite sprite, string count, string name, string ingredientId)
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
            for (int i = 0; i < MaxSlots; i++)
            {
                bool filled = i < _slotIds.Count;
                var se = _slotElements[i];
                var si = _slotIcons[i];
                var sn = _slotNames[i];

                if (se == null) continue;

                se.RemoveFromClassList("cook-slot-empty");
                se.RemoveFromClassList("cook-slot-filled");

                if (filled)
                {
                    string id = _slotIds[i];
                    var d = DataRegistry.Instance?.GetIngredient(id);

                    if (si != null)
                        si.style.backgroundImage = d?.sprite != null
                            ? Background.FromSprite(d.sprite)
                            : StyleKeyword.None;

                    if (sn != null) sn.text = d != null ? d.displayName : id;
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
            if (_recipeLabel == null || _cookBtn == null) return;

            _matched = (_slotIds.Count > 0)
                ? RecipeMatcher.FindSlotMatch(DataRegistry.Instance?.AllRecipes, _slotIds)
                : null;

            if (_matched != null)
            {
                _recipeLabel.text = $"레시피: {_matched.displayName}";
                _recipeLabel.RemoveFromClassList("cook-recipe-matched");
                _recipeLabel.AddToClassList("cook-recipe-matched");

                _cookBtn.RemoveFromClassList("cook-btn-disabled");
                _cookBtn.AddToClassList("cook-btn-enabled");
            }
            else
            {
                _recipeLabel.text = _slotIds.Count > 0 ? "재료 조합이 맞지 않습니다" : "재료를 선택하세요";
                _recipeLabel.RemoveFromClassList("cook-recipe-matched");

                _cookBtn.RemoveFromClassList("cook-btn-enabled");
                _cookBtn.AddToClassList("cook-btn-disabled");
            }
        }

        // ── 슬롯 조작 ────────────────────────────────────────────────

        void AddIngredientToSlot(string id)
        {
            if (_slotIds.Count >= MaxSlots) return;
            if (!Inventory.Instance.Has(id, 1)) return;

            _slotIds.Add(id);
            RefreshSlotDisplay();
            RefreshRecipeMatch();
        }

        void RemoveSlot(int index)
        {
            if (index < 0 || index >= _slotIds.Count) return;
            _slotIds.RemoveAt(index);
            RefreshSlotDisplay();
            RefreshRecipeMatch();
        }

        // ── 요리 실행 ────────────────────────────────────────────────

        void TryCook()
        {
            if (_matched == null || _station == null) return;

            // 슬롯 재료가 인벤토리에 있는지 재확인
            if (!RecipeMatcher.CanCook(_matched, Inventory.Instance))
            {
                _recipeLabel.text = "재료가 부족합니다!";
                return;
            }

            _station.CookRecipe(_matched);
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
