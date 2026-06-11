using MonsterKitchen.Core;
using MonsterKitchen.Data;
using MonsterKitchen.Dungeon;
using UnityEngine;
using UnityEngine.UIElements;

namespace MonsterKitchen.UI
{
    // ====================================================================
    //  DungeonBagUI — 던전 가방 패널. UIPanel(isPopup=true), B키 토글.
    //
    //  ▶ DungeonBag.Current 에서 내용물 조회.
    //  ▶ 버리기 버튼 → DungeonBag.Remove() → 즉시 리스트 갱신.
    //  ▶ DungeonScene 에만 씬 직접 배치 → B키 등록은 UIPanel 이 자동 처리.
    // ====================================================================

    public class DungeonBagUI : UIPanel
    {
        Label      m_WeightText;
        ScrollView m_ItemList;

        // ── UIPanel 훅 ────────────────────────────────────────────────

        protected override void OnFirstOpen()
        {
            m_WeightText = Root?.Q<Label>("bag-weight-text");
            m_ItemList   = Root?.Q<ScrollView>("bag-item-list");

            Root?.Q<Button>("bag-close-btn")
                ?.RegisterCallback<ClickEvent>(_ => UIManager.Instance?.Close(PanelId));
        }

        public override void OnOpen()
        {
            base.OnOpen();
            Refresh();

            if (DungeonBag.Current != null)
                DungeonBag.Current.OnBagChanged += Refresh;
        }

        public override void OnClose()
        {
            if (DungeonBag.Current != null)
                DungeonBag.Current.OnBagChanged -= Refresh;

            base.OnClose();
        }

        // ── 리스트 갱신 ──────────────────────────────────────────────

        void Refresh()
        {
            var bag = DungeonBag.Current;
            if (bag == null) return;

            // 무게 텍스트
            if (m_WeightText != null)
            {
                m_WeightText.text = $"무게: {bag.CurrentWeight} / {bag.MaxWeight}";
                if (bag.IsFull)
                    m_WeightText.AddToClassList("bag-weight-text--full");
                else
                    m_WeightText.RemoveFromClassList("bag-weight-text--full");
            }

            // 슬롯 목록 재빌드
            m_ItemList?.Clear();
            foreach (var kv in bag.Contents)
            {
                uint              id      = kv.Key;
                int               qty     = kv.Value.qty;
                IngredientQuality quality = kv.Value.quality;

                var data = DataRegistry.Instance?.Ingredients?.Get(id);
                var slot = BuildSlot(id, qty, quality, data);
                m_ItemList?.Add(slot);
            }
        }

        VisualElement BuildSlot(uint id, int qty, IngredientQuality quality, IngredientData data)
        {
            var slot = new VisualElement();
            slot.AddToClassList("bag-slot");

            // 아이콘
            var icon = new VisualElement();
            icon.AddToClassList("bag-slot-icon");
            if (data != null && !string.IsNullOrEmpty(data.SpriteAddress))
            {
                var sprite = AssetLoadManager.Instance?.Load<Sprite>(data.SpriteAddress);
                if (sprite != null)
                    icon.style.backgroundImage = new StyleBackground(sprite);
            }
            slot.Add(icon);

            // 정보
            var info = new VisualElement();
            info.AddToClassList("bag-slot-info");

            var nameLabel = new Label(data?.DisplayName ?? $"ID:{id}");
            nameLabel.AddToClassList("bag-slot-name");
            info.Add(nameLabel);

            string qualityStr = quality == IngredientQuality.III ? "최상" :
                                quality == IngredientQuality.II  ? "상"   : "보통";
            var detailLabel = new Label($"x{qty}  품질:{qualityStr}");
            detailLabel.AddToClassList("bag-slot-detail");
            info.Add(detailLabel);

            slot.Add(info);

            // 버리기 버튼
            var discard = new Button();
            discard.text = "버리기";
            discard.AddToClassList("bag-discard-btn");
            discard.RegisterCallback<ClickEvent>(_ => DungeonBag.Current?.Remove(id, qty));
            slot.Add(discard);

            return slot;
        }
    }
}
