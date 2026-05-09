using MonsterKitchen.Data;
using UnityEngine;
using UnityEngine.UIElements;

namespace MonsterKitchen.UI
{
    /// <summary>
    /// 인벤토리 팝업 UI.
    /// UIPanel(isPopup=true, layer=Panel) 을 상속한다.
    ///
    /// - I 키         : UIManager.Toggle → OnOpen / OnClose
    /// - ✕ / 배경 클릭: UIManager.Close
    /// - 인벤토리 변경: Inventory.OnItemChanged / FoodInventory.OnFoodChanged
    ///
    /// 아이템 데이터는 DataRegistry 경유 조회 (Inspector SO 배열 제거).
    /// DataRegistry.IsReady 가 true 여야 아이콘·이름이 정상 표시됩니다.
    /// </summary>
    public class InventoryUI : UIPanel
    {

        VisualElement _ingGrid;
        VisualElement _foodGrid;

        // ── Unity 생명주기 ────────────────────────────────────────────

        protected override void Awake()
        {
            base.Awake(); // Document, Root 캐싱 + sortingOrder = UILayer.Panel(20)
        }

        protected override void Start()
        {
            base.Start(); // UIManager.Register(this)

            _ingGrid  = Root?.Q<VisualElement>("ing-grid");
            _foodGrid = Root?.Q<VisualElement>("food-grid");

            // ✕ 버튼
            Root?.Q<Label>("inv-close-btn")
                ?.RegisterCallback<ClickEvent>(_ => UIManager.Instance?.Close(PanelId));

            // 배경 클릭 닫기
            Root?.Q<VisualElement>("inv-overlay")
                ?.RegisterCallback<ClickEvent>(evt =>
                {
                    if (evt.target is VisualElement ve && ve.name == "inv-overlay")
                        UIManager.Instance?.Close(PanelId);
                });

            // 인벤토리 변경 → 열려있을 때만 갱신
            if (Inventory.Instance != null)
                Inventory.Instance.OnItemChanged += (_, _) => { if (IsOpen) BuildIngredients(); };
            if (FoodInventory.Instance != null)
                FoodInventory.Instance.OnFoodChanged += (_, _) => { if (IsOpen) BuildFoods(); };
        }

        // ── UIPanel 생명주기 override ────────────────────────────────
        // Update() 없음 — 키 입력은 UIPanel._toggleKey → InputManager 가 처리

        public override void OnOpen()
        {
            base.OnOpen(); // SetVisible(true)
            BuildIngredients();
            BuildFoods();
        }

        // OnClose() 는 base(SetVisible(false)) 만으로 충분

        // ── 그리드 빌드 ──────────────────────────────────────────────

        void BuildIngredients()
        {
            if (_ingGrid == null) return;
            _ingGrid.Clear();

            var all = Inventory.Instance?.All;
            if (all == null || all.Count == 0)
            {
                _ingGrid.Add(EmptyMsg("재료가 없습니다."));
                return;
            }

            var registry = DataRegistry.Instance;
            foreach (var kv in all)
            {
                var d = registry?.GetIngredient(kv.Key);
                _ingGrid.Add(Slot(d?.sprite, kv.Value.ToString(), d != null ? d.displayName : kv.Key));
            }
        }

        void BuildFoods()
        {
            if (_foodGrid == null) return;
            _foodGrid.Clear();

            var all = FoodInventory.Instance?.All;
            if (all == null || all.Count == 0)
            {
                _foodGrid.Add(EmptyMsg("요리가 없습니다."));
                return;
            }

            var registry = DataRegistry.Instance;
            foreach (var kv in all)
            {
                var d = registry?.GetFood(kv.Key);
                _foodGrid.Add(Slot(d?.sprite, kv.Value.ToString(), d != null ? d.displayName : kv.Key));
            }
        }

        // ── UI 요소 빌더 ─────────────────────────────────────────────

        static VisualElement Slot(Sprite sprite, string count, string name)
        {
            var wrap = new VisualElement();
            wrap.style.alignItems = Align.Center;

            var slot = new VisualElement();
            slot.AddToClassList("inv-slot");

            var icon = new VisualElement();
            icon.AddToClassList("inv-slot-icon");
            if (sprite != null) icon.style.backgroundImage = Background.FromSprite(sprite);
            slot.Add(icon);

            var countLbl = new Label(count);
            countLbl.AddToClassList("inv-slot-count");
            slot.Add(countLbl);

            var nameLbl = new Label(name);
            nameLbl.AddToClassList("inv-slot-name");

            wrap.Add(slot);
            wrap.Add(nameLbl);
            return wrap;
        }

        static Label EmptyMsg(string msg)
        {
            var lbl = new Label(msg);
            lbl.AddToClassList("inv-empty-msg");
            return lbl;
        }
    }
}
