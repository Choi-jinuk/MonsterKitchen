using System.Collections.Generic;
using MonsterKitchen.Data;

namespace MonsterKitchen.Core
{
    // ====================================================================
    //  PlayerDataManager — 플레이어 런타임 데이터 통합 접근점 + Apply 조율
    //
    //  ▶ 역할
    //    서브 데이터 클래스(Coin · Inventory · Upgrades)를 생성·초기화하고
    //    단일 읽기 접근점을 제공한다.
    //    데이터 변경은 NetworkManager.Request*() → Apply*() 경로만 허용.
    //    저장/로드(I/O) 는 ServerDBManager 에 위임한다.
    //
    //  ▶ 3-레이어 + NetworkManager 구조에서의 위치
    //    TableData         — 정적 게임 정의 (읽기 전용)
    //    PlayerDataManager — 런타임 상태 접근점 (이 클래스)   ← 현재
    //    NetworkManager    — 서버 요청/응답 → Apply*() 호출
    //    ServerDBManager   — 영속 저장/로드
    //
    //  ▶ 접근 방법
    //    PlayerDataManager.Instance.Coin.Gold              // 읽기
    //    PlayerDataManager.Instance.Inventory.AllFoods     // 읽기
    //    NetworkManager.Instance.RequestEarnGold(100)      // 변경 요청
    //
    //  ▶ 생명주기
    //    GlobalController.CreateManagers() → new PlayerDataManager()
    //    GlobalController.InitManagers()   → Init()
    //    NetworkManager.Request*()         → Apply*()
    //    ServerDBManager.Load()            → LoadFrom(ServerSaveData)
    // ====================================================================

    public class PlayerDataManager
    {
        public static PlayerDataManager Instance { get; private set; }

        // ── 서브 데이터 클래스 ───────────────────────────────────────
        public PlayerCoinData      Coin      { get; private set; }
        public PlayerInventoryData Inventory { get; private set; }
        public PlayerUpgradeData   Upgrades  { get; private set; }

        /// <summary>현재 선택된 플레이어 캐릭터 ID. ServerDB 저장/로드 시 사용.</summary>
        public uint SelectedCharId { get; set; } = 9001;

        // ================================================================
        //  생명주기
        // ================================================================

        public void Init()
        {
            Instance  = this;
            Coin      = new PlayerCoinData();
            Inventory = new PlayerInventoryData();
            Upgrades  = new PlayerUpgradeData();

            Coin.Init();
            Inventory.Init();
            Upgrades.Init();
        }

        // ================================================================
        //  Apply — NetworkManager 가 서버 응답 후 호출한다
        // ================================================================

        /// <summary>골드를 newGold 로 설정한다.</summary>
        public void ApplyGold(int newGold) => Coin.SetGold(newGold);

        /// <summary>재료 id 의 수량을 newQty 로 설정한다. 0 이하면 제거.</summary>
        public void ApplyIngredient(uint id, int newQty) => Inventory.SetIngredientQty(id, newQty);

        /// <summary>음식 id 를 등급 grade 로 1개 추가한다.</summary>
        public void ApplyFoodAdd(uint id, FoodGrade grade) => Inventory.AddFoodEntry(id, grade);

        /// <summary>음식 id 를 1개 소모하고 (등급, 잔여 수량) 을 반환한다.</summary>
        public (FoodGrade grade, int remaining) ApplyFoodConsume(uint id) =>
            Inventory.ConsumeFood(id);

        /// <summary>업그레이드 type 을 newLevel 로 설정한다.</summary>
        public void ApplyUpgradeLevel(PlayerUpgradeType type, int newLevel) =>
            Upgrades.SetLevel(type, newLevel);

        // ================================================================
        //  세이브/로드 — ServerDBManager 전용
        // ================================================================

        /// <summary>저장 데이터 복원 시 ServerDBManager 가 호출한다.</summary>
        public void LoadFrom(ServerSaveData save)
        {
            if (save == null) return;

            SelectedCharId = save.SelectedCharId;
            Coin.SetGold(save.Gold);
            Upgrades.LoadLevels(
                save.ToolDamageLevel, save.ToolRangeLevel, save.ToolCooldownLevel,
                save.ShopSeatLevel,   save.ShopTipLevel);

            Inventory.LoadIngredients(FlatEntries(save.Ingredients));
            Inventory.LoadFoods(FlatEntries(save.Foods));
        }

        // ── 내부 헬퍼 ────────────────────────────────────────────────

        static IEnumerable<(uint id, int qty)> FlatEntries(List<InventoryEntry> list)
        {
            if (list == null) yield break;
            foreach (var e in list) yield return (e.Id, e.Qty);
        }

        static IEnumerable<(uint id, int qty)> FlatEntries(List<FoodEntry> list)
        {
            if (list == null) yield break;
            foreach (var e in list) yield return (e.Id, e.Qty);
        }
    }
}
