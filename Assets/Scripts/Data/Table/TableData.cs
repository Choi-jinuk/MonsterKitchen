using System.Collections.Generic;
using UnityEngine;

namespace MonsterKitchen.Data
{
    /// <summary>
    /// 게임 전체 마스터 데이터를 하나의 SO 에 집약.
    /// 각 타입은 전용 *Table 클래스로 보관된다.
    ///
    /// 데이터 조회는 각 필드의 Get / All / Contains / Count 로 수행한다.
    ///   예) tableData.Monsters.Get(1001)
    ///       tableData.SkillGroups.FindByStringId("SGD_001")
    ///
    /// 새 데이터 타입 추가 시:
    ///   1. XxxData.cs 에 데이터 클래스 + XxxTable : DataTable&lt;XxxData&gt; 추가
    ///   2. 이 파일에 XxxTable 필드 추가
    ///   3. DataRegistry 에 접근자·편의 메서드 추가
    ///
    /// 생성: Assets/Data/SO/ 우클릭 → Create → MonsterKitchen → Table Data
    /// </summary>
    [CreateAssetMenu(menuName = "MonsterKitchen/Table Data", fileName = "TableData")]
    public class TableData : ScriptableObject
    {
        [Header("CSV Synced — 기본 테이블")]
        public MonsterTable       Monsters           = new();
        public IngredientTable    Ingredients        = new();
        public FoodTable          Foods              = new();
        public RecipeTable        Recipes            = new();
        public DropTable          DropTables         = new();
        public DungeonSpawnTable  DungeonSpawnTables = new();
        public GatheringToolTable GatheringTools     = new();
        public ResourceNodeTable  ResourceNodes      = new();
        public UITable            UIPanels           = new();

        [Header("CSV Synced — 스킬 테이블 (Weapons 보다 먼저 동기화할 것)")]
        public SkillStepTable  SkillSteps  = new();
        public SkillGroupTable SkillGroups = new();

        [Header("CSV Synced — 무기·플레이어 (스킬 테이블 동기화 이후)")]
        public WeaponTable Weapons = new();
        public PlayerCharTable PlayersChar = new();

        // ── 일괄 처리 ─────────────────────────────────────────────────────
        /// <summary>
        /// 모든 테이블을 순서대로 반환한다.
        /// DataRegistry.Load() 에서 RuntimeSetData() 일괄 호출에 사용.
        /// 새 테이블 추가 시 반드시 여기에도 추가할 것.
        /// </summary>
        public IEnumerable<DataTableBase> AllTables()
        {
            yield return Monsters;
            yield return Ingredients;
            yield return Foods;
            yield return Recipes;
            yield return DropTables;
            yield return DungeonSpawnTables;
            yield return GatheringTools;
            yield return ResourceNodes;
            yield return UIPanels;
            yield return SkillSteps;
            yield return SkillGroups;
            yield return Weapons;
            yield return PlayersChar;
        }
    }
}
