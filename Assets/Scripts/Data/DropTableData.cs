using UnityEngine;
using System.Collections.Generic;

namespace MonsterKitchen.Data
{
    [System.Serializable]
    public struct DropEntry
    {
        public IngredientData ingredient;
        [Range(0f, 1f)] public float dropChance;
        public int minQuantity;
        public int maxQuantity;
    }

    [CreateAssetMenu(menuName = "MonsterKitchen/Data/DropTableData", fileName = "DRP_")]
    public class DropTableData : ScriptableObject
    {
        public string     id;   // DRP_001
        public DropEntry[] entries;

        /// <summary>랜덤 드롭 결과 반환. 드롭 없으면 빈 배열.</summary>
        public (IngredientData ingredient, int quantity)[] Roll()
        {
            var results = new List<(IngredientData, int)>();
            foreach (var e in entries)
            {
                if (Random.value <= e.dropChance)
                {
                    int qty = Random.Range(e.minQuantity, e.maxQuantity + 1);
                    results.Add((e.ingredient, qty));
                }
            }
            return results.ToArray();
        }
    }
}
