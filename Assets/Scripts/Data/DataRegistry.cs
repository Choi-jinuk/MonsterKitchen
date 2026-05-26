using System.Collections.Generic;
using UnityEngine;

namespace MonsterKitchen.Data
{
    /// <summary>
    /// 런타임 ID → 데이터 조회 레지스트리. DontDestroyOnLoad 싱글톤.
    /// TableData SO 를 Inspector 에서 연결해 사용한다.
    ///
    /// 각 데이터 테이블에 직접 접근할 때는 Table 프로퍼티를 사용한다.
    ///   예) DataRegistry.Instance.Table.Monsters.Get(1u)
    /// 편의 메서드(GetMonster 등)도 제공한다.
    /// </summary>
    public class DataRegistry : MonoBehaviour
    {
        public static DataRegistry Instance { get; private set; }

        [SerializeField] TableData _table;

        /// <summary>TableData 직접 접근. DataTable&lt;T&gt; API 를 그대로 사용할 수 있다.</summary>
        public TableData Table => _table;

        public bool IsReady => _table != null;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (_table == null)
                Debug.LogError("[DataRegistry] TableData 가 연결되지 않았습니다. Inspector 에서 TableData.asset 을 연결하세요.", this);
        }

        // ── 편의 조회 API ────────────────────────────────────────────────

        public MonsterData       GetMonster      (uint id) => _table?.Monsters.Get(id);
        public IngredientData    GetIngredient   (uint id) => _table?.Ingredients.Get(id);
        public RecipeData        GetRecipe       (uint id) => _table?.Recipes.Get(id);
        public FoodData          GetFood         (uint id) => _table?.Foods.Get(id);
        public DropTableData     GetDropTable    (uint id) => _table?.DropTables.Get(id);
        public WeaponData        GetWeapon       (uint id) => _table?.Weapons.Get(id);
        public GatheringToolData GetGatheringTool(uint id) => _table?.GatheringTools.Get(id);

        public DungeonSpawnTableData GetDungeonSpawnTable(uint id) => _table?.DungeonSpawnTables.Get(id);

        public IEnumerable<MonsterData>          AllMonsters          => _table?.Monsters.All;
        public IEnumerable<IngredientData>       AllIngredients       => _table?.Ingredients.All;
        public IEnumerable<RecipeData>           AllRecipes           => _table?.Recipes.All;
        public IEnumerable<FoodData>             AllFoods             => _table?.Foods.All;
        public IEnumerable<WeaponData>           AllWeapons           => _table?.Weapons.All;
        public IEnumerable<DungeonSpawnTableData> AllDungeonSpawnTables => _table?.DungeonSpawnTables.All;
    }
}
