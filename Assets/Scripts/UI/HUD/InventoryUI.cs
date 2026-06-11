using MonsterKitchen.Core;
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
    /// 아이템 데이터는 DataRegistry 경유 조회.
    /// </summary>
    public class InventoryUI : UIPanel
    {
        VisualElement m_IngGrid;
        VisualElement m_FoodGrid;

        // ── UIPanel 훅 ────────────────────────────────────────────────

        protected override void OnFirstOpen()
        {
            m_IngGrid  = Root?.Q<VisualElement>("ing-grid");
            m_FoodGrid = Root?.Q<VisualElement>("food-grid");

            Root?.Q<Label>("inv-close-btn")
                ?.RegisterCallback<ClickEvent>(_ => UIManager.Instance?.Close(PanelId));

            Root?.Q<VisualElement>("inv-overlay")
                ?.RegisterCallback<ClickEvent>(evt =>
                {
                    if (evt.target is VisualElement ve && ve.name == "inv-overlay")
                        UIManager.Instance?.Close(PanelId);
                });
        }

        // ── UIPanel override ─────────────────────────────────────────

        public override void OnOpen()
        {
            base.OnOpen();
            var inv = PlayerDataManager.Instance?.Inventory;
            if (inv != null)
            {
                inv.OnIngredientChanged += HandleIngredientChanged;
                inv.OnFoodChanged       += HandleFoodChanged;
            }
            BuildIngredients();
            BuildFoods();
        }

        public override void OnClose()
        {
            var inv = PlayerDataManager.Instance?.Inventory;
            if (inv != null)
            {
                inv.OnIngredientChanged -= HandleIngredientChanged;
                inv.OnFoodChanged       -= HandleFoodChanged;
            }
            base.OnClose();
        }

        void HandleIngredientChanged(uint id, int qty) => BuildIngredients();
        void HandleFoodChanged(uint id, int qty)       => BuildFoods();

        // ── 그리드 빌드 ──────────────────────────────────────────────

        void BuildIngredients()
        {
            if (m_IngGrid == null) return;
            m_IngGrid.Clear();

            var all = PlayerDataManager.Instance?.Inventory.AllIngredients;
            if (all == null || all.Count == 0)
            {
                m_IngGrid.Add(EmptyMsg("재료가 없습니다."));
                return;
            }

            var inv      = PlayerDataManager.Instance.Inventory;
            var registry = DataRegistry.Instance;
            var loader   = AssetLoadManager.Instance;
            foreach (var kv in all)
            {
                var d       = registry?.Ingredients?.Get(kv.Key);
                var sprite  = loader?.Load<Sprite>(d?.SpriteAddress);
                var quality = inv.GetBestQuality(kv.Key);
                m_IngGrid.Add(IngredientSlot(sprite, kv.Value.ToString(), d?.DisplayName ?? CommonString.Unknown, quality));
            }
        }

        void BuildFoods()
        {
            if (m_FoodGrid == null) return;
            m_FoodGrid.Clear();

            var all = PlayerDataManager.Instance?.Inventory.AllFoods;
            if (all == null || all.Count == 0)
            {
                m_FoodGrid.Add(EmptyMsg("요리가 없습니다."));
                return;
            }

            var registry = DataRegistry.Instance;
            var loader   = AssetLoadManager.Instance;
            foreach (var kv in all)
            {
                var d      = registry?.Foods?.Get(kv.Key);
                var sprite = loader?.Load<Sprite>(d?.SpriteAddress);
                m_FoodGrid.Add(Slot(sprite, kv.Value.ToString(), d?.DisplayName ?? CommonString.Unknown));
            }
        }

        // ── UI 요소 빌더 ─────────────────────────────────────────────

        static VisualElement IngredientSlot(Sprite sprite, string count, string name, IngredientQuality quality)
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

            // 품질 배지
            var badge = new Label(quality switch
            {
                IngredientQuality.III => "III",
                IngredientQuality.II  => "II",
                _                     => "I",
            });
            badge.AddToClassList("inv-quality-badge");
            badge.style.color = quality switch
            {
                IngredientQuality.III => new Color(1f,  0.84f, 0f),    // 금색
                IngredientQuality.II  => new Color(0.4f, 0.6f, 1f),   // 파란색
                _                     => new Color(0.6f, 0.6f, 0.6f), // 회색
            };
            slot.Add(badge);

            var nameLbl = new Label(name);
            nameLbl.AddToClassList("inv-slot-name");

            wrap.Add(slot);
            wrap.Add(nameLbl);
            return wrap;
        }

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
