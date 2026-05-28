using UnityEngine;

namespace MonsterKitchen.Data
{
    /// <summary>
    /// 게임 전체 마스터 데이터를 하나의 SO 에 집약.
    /// 각 타입은 DataTable&lt;T&gt; 필드로 보관된다.
    ///
    /// 데이터 조회는 각 필드의 Get / All / Contains / Count 로 수행한다.
    ///   예) tableData.Monsters.Get("MON_001")
    ///       tableData.Ingredients.All
    ///
    /// 새 데이터 타입 추가 시: DataTable&lt;NewData&gt; 필드 하나만 추가하면 된다.
    ///
    /// 생성: Assets/Data/SO/ 우클릭 → Create → MonsterKitchen → Table Data
    /// </summary>
    [CreateAssetMenu(menuName = "MonsterKitchen/Table Data", fileName = "TableData")]
    public class TableData : ScriptableObject
    {
        [Header("CSV Synced — 기본 테이블")]
        public DataTable<MonsterData>    Monsters    = new();
        public DataTable<IngredientData> Ingredients = new();
        public DataTable<FoodData>       Foods       = new();
        public DataTable<RecipeData>     Recipes     = new();
        public DataTable<DropTableData>  DropTables  = new();
        public DataTable<DungeonSpawnTableData> DungeonSpawnTables = new();
        public DataTable<GatheringToolData>     GatheringTools     = new();

        [Header("CSV Synced — 스킬 테이블 (Weapons 보다 먼저 동기화할 것)")]
        public DataTable<SkillData>      SkillSteps  = new();
        public DataTable<SkillGroupData> SkillGroups = new();

        [Header("CSV Synced — 무기·플레이어 (스킬 테이블 동기화 이후)")]
        public DataTable<WeaponData>      Weapons     = new();
        public DataTable<PlayerCharData> Players     = new();
    }
}
