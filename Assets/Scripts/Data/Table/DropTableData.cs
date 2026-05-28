using System;
using System.Collections.Generic;
using MonsterKitchen.Core;
using UnityEngine;

namespace MonsterKitchen.Data
{
    [Serializable]
    public struct DropEntry
    {
        [Tooltip("재료 ID (TableData.Ingredients 키)")]
        public uint IngredientId;
        [Range(0f, 1f)] public float DropChance;
        public int MinQuantity;
        public int MaxQuantity;
    }

    [Serializable]
    public class DropTableData
    {
        public uint       Id;
        public DropEntry[] Entries;

        /// <summary>
        /// 랜덤 드롭 결과 반환. (ingredientId, quantity) 튜플 배열.
        /// 드롭 없으면 빈 배열.
        /// </summary>
        public (uint ingredientId, int quantity)[] Roll()
        {
            if (Entries == null) return Array.Empty<(uint, int)>();
            var results = new List<(uint, int)>();
            foreach (var e in Entries)
            {
                if (e.IngredientId == 0) continue;
                if (RandomUtil.Chance(e.DropChance))
                {
                    int qty = RandomUtil.Range(e.MinQuantity, e.MaxQuantity + 1);
                    results.Add((e.IngredientId, qty));
                }
            }
            return results.ToArray();
        }
    }
}
