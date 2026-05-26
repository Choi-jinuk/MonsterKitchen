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
        public uint ingredientId;
        [Range(0f, 1f)] public float dropChance;
        public int minQuantity;
        public int maxQuantity;
    }

    [Serializable]
    public class DropTableData
    {
        public uint       id;
        public DropEntry[] entries;

        /// <summary>
        /// 랜덤 드롭 결과 반환. (ingredientId, quantity) 튜플 배열.
        /// 드롭 없으면 빈 배열.
        /// </summary>
        public (uint ingredientId, int quantity)[] Roll()
        {
            if (entries == null) return Array.Empty<(uint, int)>();
            var results = new List<(uint, int)>();
            foreach (var e in entries)
            {
                if (e.ingredientId == 0) continue;
                if (RandomUtil.Chance(e.dropChance))
                {
                    int qty = RandomUtil.Range(e.minQuantity, e.maxQuantity + 1);
                    results.Add((e.ingredientId, qty));
                }
            }
            return results.ToArray();
        }
    }
}
