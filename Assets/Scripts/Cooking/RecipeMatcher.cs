using System.Collections.Generic;
using System.Linq;
using MonsterKitchen.Data;
using UnityEngine;

namespace MonsterKitchen.Cooking
{
    /// <summary>
    /// 현재 인벤토리 기준으로 조리 가능한 레시피를 찾는다.
    /// </summary>
    public static class RecipeMatcher
    {
        /// <summary>
        /// 슬롯에 담긴 재료 ID 목록이 정확히 일치하는 레시피를 반환. 없으면 null.
        /// 순서 무관. 빈 슬롯(null/empty)은 무시한다.
        /// </summary>
        public static RecipeData FindSlotMatch(IEnumerable<RecipeData> recipes, List<string> slotIngredientIds)
        {
            if (recipes == null || slotIngredientIds == null) return null;

            // 슬롯의 유효 재료를 id → count 딕셔너리로 변환
            var slotCounts = new Dictionary<string, int>();
            foreach (var id in slotIngredientIds)
            {
                if (string.IsNullOrEmpty(id)) continue;
                slotCounts.TryGetValue(id, out int c);
                slotCounts[id] = c + 1;
            }

            foreach (var recipe in recipes)
            {
                if (recipe == null) continue;
                if (!recipe.isUnlockedByDefault) continue;

                // 레시피 재료를 id → quantity 딕셔너리로 변환
                var reqCounts = new Dictionary<string, int>();
                foreach (var req in recipe.ingredients)
                {
                    if (req.ingredient == null) continue;
                    reqCounts.TryGetValue(req.ingredient.id, out int c);
                    reqCounts[req.ingredient.id] = c + req.quantity;
                }

                if (slotCounts.Count != reqCounts.Count) continue;
                bool match = reqCounts.All(kv =>
                    slotCounts.TryGetValue(kv.Key, out int sc) && sc == kv.Value);
                if (match) return recipe;
            }
            return null;
        }

        /// <summary>
        /// 인벤토리의 재료로 만들 수 있는 레시피 목록 반환.
        /// </summary>
        public static List<RecipeData> FindMatchable(RecipeData[] allRecipes, Inventory inventory)
        {
            var result = new List<RecipeData>();
            if (allRecipes == null || inventory == null) return result;

            foreach (var recipe in allRecipes)
            {
                if (recipe == null) continue;
                if (!recipe.isUnlockedByDefault) continue;  // 잠긴 레시피 건너뜀 (MVP)
                if (CanCook(recipe, inventory))
                    result.Add(recipe);
            }
            return result;
        }

        /// <summary>레시피 1개가 현재 인벤토리로 조리 가능한지 확인.</summary>
        public static bool CanCook(RecipeData recipe, Inventory inventory)
        {
            foreach (var req in recipe.ingredients)
            {
                if (req.ingredient == null) continue;
                if (!inventory.Has(req.ingredient.id, req.quantity))
                    return false;
            }
            return true;
        }

        /// <summary>레시피 재료를 인벤토리에서 소모.</summary>
        public static bool ConsumeIngredients(RecipeData recipe, Inventory inventory)
        {
            if (!CanCook(recipe, inventory)) return false;

            foreach (var req in recipe.ingredients)
            {
                if (req.ingredient == null) continue;
                inventory.Remove(req.ingredient.id, req.quantity);
            }
            return true;
        }
    }
}
