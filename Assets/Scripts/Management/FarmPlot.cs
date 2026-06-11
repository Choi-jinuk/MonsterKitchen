using MonsterKitchen.Core;
using MonsterKitchen.Data;
using UnityEngine;

namespace MonsterKitchen.Management
{
    /// <summary>
    /// 밭/양식장 한 칸.
    /// 비어 있을 때 Player 접촉 → Plant().
    /// 수확 가능할 때 Player 접촉 → Harvest() → 인벤토리에 추가.
    /// </summary>
    public class FarmPlot : MonoBehaviour
    {
        [Header("Crop Settings")]
        [SerializeField] uint m_CropIngredientId;
        [SerializeField] int  m_GrowDays    = 2;
        [SerializeField] int  m_YieldAmount = 2;

        [Header("Visual")]
        [SerializeField] SpriteRenderer m_PlotRenderer;
        [SerializeField] Sprite         m_EmptySprite;
        [SerializeField] Sprite         m_PlantedSprite;
        [SerializeField] Sprite         m_ReadySprite;

        public uint CropIngredientId => m_CropIngredientId;
        public bool IsPlanted        => m_PlantedDay >= 0;

        public bool IsReady
        {
            get
            {
                if (!IsPlanted || DayManager.Instance == null) return false;
                return (DayManager.Instance.CurrentDay - m_PlantedDay) >= m_GrowDays;
            }
        }

        public int DaysRemaining
        {
            get
            {
                if (!IsPlanted || DayManager.Instance == null) return 0;
                return Mathf.Max(0, m_GrowDays - (DayManager.Instance.CurrentDay - m_PlantedDay));
            }
        }

        int m_PlantedDay = -1;

        void Start()
        {
            UpdateVisual();
        }

        public bool Plant()
        {
            if (IsPlanted || m_CropIngredientId == 0u || DayManager.Instance == null) return false;
            m_PlantedDay = DayManager.Instance.CurrentDay;
            UpdateVisual();
            var data = DataRegistry.Instance?.Ingredients?.Get(m_CropIngredientId);
            DebugUtil.Log($"[FarmPlot] {(data != null ? data.DisplayName : m_CropIngredientId.ToString())} 심기 (Day {m_PlantedDay}, {m_GrowDays}일 후 수확)");
            return true;
        }

        public bool Harvest()
        {
            if (!IsReady) return false;
            var data = DataRegistry.Instance?.Ingredients?.Get(m_CropIngredientId);
            string cropName = data != null ? data.DisplayName : m_CropIngredientId.ToString();
            m_PlantedDay = -1;
            if (m_CropIngredientId != 0u)
                NetworkManager.Instance?.RequestAddIngredient(m_CropIngredientId, m_YieldAmount);
            UpdateVisual();
            DebugUtil.Log($"[FarmPlot] {cropName} x{m_YieldAmount} 수확!");
            return true;
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;

            if (IsReady)
                Harvest();
            else if (!IsPlanted)
                Plant();
            else
            {
                var data = DataRegistry.Instance?.Ingredients?.Get(m_CropIngredientId);
                DebugUtil.Log($"[FarmPlot] {(data != null ? data.DisplayName : m_CropIngredientId.ToString())} 성장 중 (남은 {DaysRemaining}일)");
            }
        }

        void UpdateVisual()
        {
            if (m_PlotRenderer == null) return;

            if (IsReady && m_ReadySprite != null)
                m_PlotRenderer.sprite = m_ReadySprite;
            else if (IsPlanted && m_PlantedSprite != null)
                m_PlotRenderer.sprite = m_PlantedSprite;
            else if (m_EmptySprite != null)
                m_PlotRenderer.sprite = m_EmptySprite;
        }
    }
}
