using System;
using MonsterKitchen.Core;
using MonsterKitchen.Data;
using MonsterKitchen.Restaurant;
using UnityEngine;
using UnityEngine.UIElements;

namespace MonsterKitchen.UI
{
    // ====================================================================
    //  MenuSetupUI — 영업 전 메뉴 구성 패널
    //
    //  ▶ RestaurantSceneController 가 OnInit 시 열고 OnStartDay 콜백을 주입.
    //  ▶ "영업 시작" 버튼 클릭 → OnStartDay() 호출 → DayManager.StartDay().
    // ====================================================================

    public class MenuSetupUI : UIPanel
    {
        public Action OnStartDay;   // RestaurantSceneController 주입

        VisualElement m_SlotsContainer;
        VisualElement m_FoodGrid;
        Button        m_StartBtn;

        // ── UIPanel 훅 ────────────────────────────────────────────────

        protected override void OnFirstOpen()
        {
            m_SlotsContainer = Root?.Q<VisualElement>("menu-slots-container");
            m_FoodGrid       = Root?.Q<VisualElement>("menu-food-grid");
            m_StartBtn       = Root?.Q<Button>("menu-start-btn");

            m_StartBtn?.RegisterCallback<ClickEvent>(_ => HandleStartDay());
        }

        public override void OnOpen()
        {
            base.OnOpen();
            Refresh();
        }

        // ── 갱신 ─────────────────────────────────────────────────────

        void Refresh()
        {
            BuildSlots();
            BuildFoodGrid();
            UpdateStartButton();
        }

        // ── 메뉴 슬롯 ────────────────────────────────────────────────

        void BuildSlots()
        {
            m_SlotsContainer?.Clear();
            var menu = PlayerDataManager.Instance?.DailyMenu;
            if (menu == null) return;

            for (int i = 0; i < menu.SlotCount; i++)
            {
                int idx  = i;
                var slot = menu.Slots[idx];
                var row  = new VisualElement();
                row.AddToClassList("menu-slot-row");

                if (slot.IsEmpty)
                {
                    var emptyLabel = new Label($"슬롯 {idx + 1} — 비어있음");
                    emptyLabel.AddToClassList("menu-slot-empty-label");
                    row.Add(emptyLabel);
                }
                else
                {
                    var food = DataRegistry.Instance?.Foods?.Get(slot.FoodId);
                    var nameLabel = new Label(food?.DisplayName ?? $"ID:{slot.FoodId}");
                    nameLabel.AddToClassList("menu-slot-label");
                    row.Add(nameLabel);

                    var priceLabel = new Label($"{slot.CustomPrice}G");
                    priceLabel.AddToClassList("menu-slot-price-label");
                    row.Add(priceLabel);

                    var clearBtn = new Button(() => { menu.ClearSlot(idx); Refresh(); });
                    clearBtn.text = "✕";
                    clearBtn.AddToClassList("menu-slot-clear-btn");
                    row.Add(clearBtn);
                }

                m_SlotsContainer?.Add(row);
            }
        }

        // ── 음식 목록 ─────────────────────────────────────────────────

        void BuildFoodGrid()
        {
            m_FoodGrid?.Clear();
            var inv  = PlayerDataManager.Instance?.Inventory;
            var menu = PlayerDataManager.Instance?.DailyMenu;
            if (inv == null || menu == null) return;

            foreach (var kv in inv.AllFoods)
            {
                if (kv.Value <= 0) continue;
                uint foodId = kv.Key;
                if (menu.IsOnMenu(foodId)) continue;

                var food = DataRegistry.Instance?.Foods?.Get(foodId);
                if (food == null) continue;

                var item = new VisualElement();
                item.AddToClassList("menu-food-item");

                var nameLabel = new Label(food.DisplayName);
                nameLabel.AddToClassList("menu-food-name");
                item.Add(nameLabel);

                var countLabel = new Label($"×{kv.Value}");
                countLabel.AddToClassList("menu-food-count");
                item.Add(countLabel);

                item.RegisterCallback<ClickEvent>(_ =>
                {
                    int emptyIdx = menu.FirstEmptySlotIndex();
                    if (emptyIdx < 0) return;
                    menu.SetSlot(emptyIdx, foodId, food.BasePrice);
                    Refresh();
                });

                m_FoodGrid?.Add(item);
            }
        }

        // ── 영업 시작 버튼 ────────────────────────────────────────────

        void UpdateStartButton()
        {
            if (m_StartBtn == null) return;
            bool canStart = PlayerDataManager.Instance?.DailyMenu?.HasAnyItem() ?? false;
            m_StartBtn.SetEnabled(canStart);
        }

        void HandleStartDay()
        {
            UIManager.Instance?.Close(PanelId);
            OnStartDay?.Invoke();
        }
    }
}
