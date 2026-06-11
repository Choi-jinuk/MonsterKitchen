using System;
using MonsterKitchen.Data;

namespace MonsterKitchen.Core
{
    // ====================================================================
    //  INetworkManager — NetworkManager 테스트 격리용 인터페이스
    //
    //  FakeNetworkManager(Tests/EditMode)에서 구현해 RequestCook 성공/실패 등
    //  시나리오를 주입한다. 콜사이트(NetworkManager.Instance.*) 는 변경 없음.
    // ====================================================================

    public interface INetworkManager
    {
        void RequestEarnGold(int amount, Action<int> onResult = null);
        void RequestSpendGold(int amount, Action<bool, int> onResult = null);
        void RequestAddIngredient(uint id, int qty, Action<uint, int> onResult = null);
        void RequestAddIngredient(uint id, int qty, IngredientQuality quality, Action<uint, int> onResult = null);
        void RequestCook(RecipeData recipe, FoodGrade grade, Action<bool, FoodData> onResult = null);
        void RequestServeFood(uint foodId, Action<bool, FoodGrade, int> onResult = null);
        void RequestUpgrade(PlayerUpgradeType type, Action<bool> onResult = null);
    }
}
