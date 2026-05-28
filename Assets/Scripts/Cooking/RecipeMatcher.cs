using System.Collections.Generic;
using System.Linq;
using MonsterKitchen.Data;

namespace MonsterKitchen.Cooking
{
    /// <summary>
    /// 현재 인벤토리 기준으로 조리 가능한 레시피를 찾는 읽기 전용 유틸리티.
    /// 실제 재료 소모·음식 추가는 NetworkManager.RequestCook() 이 담당한다.
    /// </summary>
    public static class RecipeMatcher
    {
        /// <summary>
        /// 슬롯에 담긴 재료 ID 목록이 정확히 일치하는 레시피를 반환. 없으면 null.
        /// 순서 무관. 빈 슬롯(0)은 무시한다.
        /// </summary>
        public static RecipeData FindSlotMatch(IEnumerable<RecipeData> recipes, List<uint> slotIngredientIds)
        {
            if (recipes == null || slotIngredientIds == null) return null;

            var slotCounts = new Dictionary<uint, int>();
            foreach (var id in slotIngredientIds)
            {
                if (id == 0u) continue;
                slotCounts.TryGetValue(id, out int c);
                slotCounts[id] = c + 1;
            }

            foreach (var recipe in recipes)
            {
                if (recipe == null) continue;
                if (!recipe.IsUnlockedByDefault) continue;

                var reqCounts = new Dictionary<uint, int>();
                foreach (var req in recipe.Ingredients)
                {
                    if (req.IngredientId == 0u) continue;
                    reqCounts.TryGetValue(req.IngredientId, out int c);
                    reqCounts[req.IngredientId] = c + req.Quantity;
                }

                if (slotCounts.Count != reqCounts.Count) continue;
                bool match = reqCounts.All(kv =>
                    slotCounts.TryGetValue(kv.Key, out int sc) && sc == kv.Value);
                if (match) return recipe;
            }
            return null;
        }

        /// <summary>PlayerInventoryData 의 재료로 만들 수 있는 레시피 목록 반환 (UI 미리보기용).</summary>
        public static List<RecipeData> FindMatchable(IEnumerable<RecipeData> allRecipes,
                                                     PlayerInventoryData inventory)
        {
            var result = new List<RecipeData>();
            if (allRecipes == null || inventory == null) return result;

            foreach (var recipe in allRecipes)
            {
                if (recipe == null) continue;
                if (!recipe.IsUnlockedByDefault) continue;
                if (CanCook(recipe, inventory))
                    result.Add(recipe);
            }
            return result;
        }

        /// <summary>레시피 1개가 현재 인벤토리로 조리 가능한지 확인 (UI 미리보기용).</summary>
        public static bool CanCook(RecipeData recipe, PlayerInventoryData inventory)
        {
            if (recipe == null || inventory == null) return false;
            foreach (var req in recipe.Ingredients)
            {
                if (req.IngredientId == 0u) continue;
                if (!inventory.HasIngredient(req.IngredientId, req.Quantity))
                    return false;
            }
            return true;
        }
    }
}
