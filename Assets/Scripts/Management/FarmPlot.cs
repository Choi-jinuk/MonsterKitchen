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
        [SerializeField] IngredientData crop;
        [SerializeField] int            growDays    = 2;
        [SerializeField] int            yieldAmount = 2;

        [Header("Visual")]
        [SerializeField] SpriteRenderer plotRenderer;
        [SerializeField] Sprite         emptySprite;
        [SerializeField] Sprite         plantedSprite;
        [SerializeField] Sprite         readySprite;

        public IngredientData Crop      => crop;
        public bool           IsPlanted => _plantedDay >= 0;

        public bool IsReady
        {
            get
            {
                if (!IsPlanted || DayManager.Instance == null) return false;
                return (DayManager.Instance.CurrentDay - _plantedDay) >= growDays;
            }
        }

        public int DaysRemaining
        {
            get
            {
                if (!IsPlanted || DayManager.Instance == null) return 0;
                return Mathf.Max(0, growDays - (DayManager.Instance.CurrentDay - _plantedDay));
            }
        }

        int _plantedDay = -1;

        void Start()
        {
            UpdateVisual();
        }

        public bool Plant()
        {
            if (IsPlanted || crop == null || DayManager.Instance == null) return false;
            _plantedDay = DayManager.Instance.CurrentDay;
            UpdateVisual();
            Debug.Log($"[FarmPlot] {crop.displayName} 심기 (Day {_plantedDay}, {growDays}일 후 수확)");
            return true;
        }

        public bool Harvest()
        {
            if (!IsReady) return false;
            string cropName = crop != null ? crop.displayName : "?";
            _plantedDay = -1;
            if (crop != null)
                Inventory.Instance?.Add(crop.id, yieldAmount);
            UpdateVisual();
            Debug.Log($"[FarmPlot] {cropName} x{yieldAmount} 수확!");
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
                Debug.Log($"[FarmPlot] {crop?.displayName} 성장 중 (남은 {DaysRemaining}일)");
        }

        void UpdateVisual()
        {
            if (plotRenderer == null) return;

            if (IsReady && readySprite != null)
                plotRenderer.sprite = readySprite;
            else if (IsPlanted && plantedSprite != null)
                plotRenderer.sprite = plantedSprite;
            else if (emptySprite != null)
                plotRenderer.sprite = emptySprite;
        }
    }
}
