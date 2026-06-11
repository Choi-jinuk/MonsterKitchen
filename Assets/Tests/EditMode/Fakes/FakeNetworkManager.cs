using System;
using MonsterKitchen.Core;
using MonsterKitchen.Data;

namespace MonsterKitchen.Tests.EditMode
{
    /// <summary>
    /// INetworkManager 테스트용 Fake. 요리 성공/실패 등 시나리오를 직접 제어한다.
    /// </summary>
    public class FakeNetworkManager : INetworkManager
    {
        public bool     CookSucceeds = true;
        public FoodData CookResult;

        public void RequestEarnGold(int amount, Action<int> onResult = null)
            => onResult?.Invoke(amount);

        public void RequestSpendGold(int amount, Action<bool, int> onResult = null)
            => onResult?.Invoke(true, 0);

        public void RequestAddIngredient(uint id, int qty, Action<uint, int> onResult = null)
            => onResult?.Invoke(id, qty);

        public void RequestAddIngredient(uint id, int qty, IngredientQuality quality, Action<uint, int> onResult = null)
            => onResult?.Invoke(id, qty);

        public void RequestCook(RecipeData recipe, FoodGrade grade, Action<bool, FoodData> onResult = null)
            => onResult?.Invoke(CookSucceeds, CookResult);

        public void RequestServeFood(uint foodId, Action<bool, FoodGrade, int> onResult = null)
            => onResult?.Invoke(true, FoodGrade.Normal, 0);

        public void RequestUpgrade(PlayerUpgradeType type, Action<bool> onResult = null)
            => onResult?.Invoke(true);
    }
}
