using MonsterKitchen.Data;

namespace MonsterKitchen.Cooking
{
    // ====================================================================
    //  CookingGradeCalculator — 레시피 재료로부터 FoodGrade 를 계산한다.
    //
    //  ▶ 공식
    //    각 재료 슬롯: score += RarityValue × StateMultiplier × Quantity
    //    weightedAvg = score / totalQty
    //    thresholds (GameConfig): Good / Perfect / Legendary
    //
    //  ▶ 테스트 가능 분리
    //    GradeFromAverage — 순수 함수 (float 만 사용)
    //    Calculate        — DataRegistry/GameConfig 조회 후 GradeFromAverage 위임
    // ====================================================================

    public static class CookingGradeCalculator
    {
        /// <summary>
        /// 레시피의 재료 구성으로부터 FoodGrade 를 계산한다.
        /// DataRegistry 또는 GameConfig 가 없으면 Normal 반환.
        /// </summary>
        public static FoodGrade Calculate(RecipeData recipe)
        {
            if (recipe?.Ingredients == null || recipe.Ingredients.Length == 0)
                return FoodGrade.Normal;

            var config   = GameConfig.Current;
            var registry = DataRegistry.Instance;
            if (config == null || registry == null) return FoodGrade.Normal;

            float totalScore = 0f;
            int   totalQty   = 0;

            foreach (var slot in recipe.Ingredients)
            {
                var data = registry.Ingredients?.Get(slot.IngredientId);
                if (data == null || slot.Quantity <= 0) continue;

                float stateMult = data.DefaultState switch
                {
                    IngredientState.Cooked  => config.GradeMultiplierCooked,
                    IngredientState.Spoiled => config.GradeMultiplierSpoiled,
                    _                       => config.GradeMultiplierRaw,
                };

                totalScore += (int)data.Rarity * stateMult * slot.Quantity;
                totalQty   += slot.Quantity;
            }

            if (totalQty == 0) return FoodGrade.Normal;

            return GradeFromAverage(
                totalScore / totalQty,
                config.GradeThresholdGood,
                config.GradeThresholdPerfect,
                config.GradeThresholdLegendary);
        }

        /// <summary>
        /// 순수 함수 — 가중 평균 점수와 threshold 값으로 FoodGrade 반환.
        /// 테스트에서 직접 호출한다.
        /// </summary>
        public static FoodGrade GradeFromAverage(
            float avg,
            float thresholdGood,
            float thresholdPerfect,
            float thresholdLegendary)
        {
            if (avg >= thresholdLegendary) return FoodGrade.Legendary;
            if (avg >= thresholdPerfect)   return FoodGrade.Perfect;
            if (avg >= thresholdGood)       return FoodGrade.Good;
            return FoodGrade.Normal;
        }
    }
}
