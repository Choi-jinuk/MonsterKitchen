using System.Collections.Generic;
using MonsterKitchen.Core;
using UnityEngine;
// StringUtil lives in MonsterKitchen.Core — already imported above

namespace MonsterKitchen.Data
{
    // ====================================================================
    //  DataRegistry — 런타임 ID → 정적 데이터 조회 레지스트리
    //
    //  ▶ 역할
    //    TableData SO 를 보유하고 편의 조회 API 를 제공한다.
    //    순수 C# 클래스. GlobalController 가 생성·관리한다.
    //
    //  ▶ 초기화 순서
    //    GlobalController.InitManagers() → Registry.Init()  (Instance 등록)
    //    GameStartup.StepDataLoad()      → Registry.Load()  (TableData SO 로드)
    //
    //  ▶ 사용법
    //    DataRegistry.Instance.GetMonster(id)
    //    DataRegistry.Instance.Table.Monsters.All
    //    GlobalController.Instance.Registry.IsReady  (로드 완료 여부 확인)
    // ====================================================================

    public class DataRegistry
    {
        public static DataRegistry Instance { get; private set; }

        TableData m_Table;

        /// <summary>TableData 직접 접근. DataTable&lt;T&gt; API 를 그대로 사용할 수 있다.</summary>
        public TableData Table => m_Table;

        /// <summary>TableData 로드 완료 여부. GameStartup.StepDataLoad 이후 true.</summary>
        public bool IsReady => m_Table != null;

        // ================================================================
        //  초기화
        // ================================================================

        public void Init() => Instance = this;

        /// <summary>
        /// TableData SO 를 AssetManifest 에서 로드한다.
        /// GameStartup.StepDataLoad() 에서 호출.
        /// </summary>
        public void Load()
        {
            m_Table = AssetLoadManager.Instance?.Load<TableData>(AssetKeys.DATA_TABLE_DATA);
            if (m_Table == null)
                Debug.LogError("[DataRegistry] TableData 로드 실패 — AssetManifest 에 'data/table_data' 키 등록 여부 확인.", null);
            else
                Debug.Log(StringUtil.Format("[DataRegistry] TableData 로드 완료 (Monsters:{0} Ingredients:{1} Recipes:{2})",
                    m_Table.Monsters?.Count ?? 0,
                    m_Table.Ingredients?.Count ?? 0,
                    m_Table.Recipes?.Count ?? 0));
        }

        // ── 캐시 — 문자열 ID 역방향 조회 ────────────────────────────────
        Dictionary<string, SkillGroupData> m_SkillGroupsByStringId;

        // ================================================================
        //  편의 조회 API
        // ================================================================

        public MonsterData            GetMonster         (uint id) => m_Table?.Monsters.Get(id);
        public IngredientData         GetIngredient      (uint id) => m_Table?.Ingredients.Get(id);
        public RecipeData             GetRecipe          (uint id) => m_Table?.Recipes.Get(id);
        public FoodData               GetFood            (uint id) => m_Table?.Foods.Get(id);
        public DropTableData          GetDropTable       (uint id) => m_Table?.DropTables.Get(id);
        public WeaponData             GetWeapon          (uint id) => m_Table?.Weapons.Get(id);
        public GatheringToolData      GetGatheringTool   (uint id) => m_Table?.GatheringTools.Get(id);
        public SkillData              GetSkillStep       (uint id) => m_Table?.SkillSteps.Get(id);
        public SkillGroupData         GetSkillGroup      (uint id) => m_Table?.SkillGroups.Get(id);
        public PlayerCharData         GetPlayer          (uint id) => m_Table?.Players.Get(id);
        public DungeonSpawnTableData  GetDungeonSpawnTable(uint id) => m_Table?.DungeonSpawnTables.Get(id);

        /// <summary>
        /// skillGroupId 문자열(예: "SGD_001")로 SkillGroupData 를 조회한다.
        /// 첫 호출 시 역방향 캐시를 빌드하고 이후 O(1) 조회.
        /// </summary>
        public SkillGroupData FindSkillGroupByStringId(string skillGroupId)
        {
            if (string.IsNullOrEmpty(skillGroupId) || m_Table == null) return null;

            if (m_SkillGroupsByStringId == null)
            {
                m_SkillGroupsByStringId = new Dictionary<string, SkillGroupData>();
                foreach (var sg in m_Table.SkillGroups.All)
                    if (!string.IsNullOrEmpty(sg.SkillGroupId))
                        m_SkillGroupsByStringId[sg.SkillGroupId] = sg;
            }

            return m_SkillGroupsByStringId.GetValueOrDefault(skillGroupId);
        }

        // ── IEnumerable 접근자 ───────────────────────────────────────────
        public IEnumerable<MonsterData>           AllMonsters           => m_Table?.Monsters.All;
        public IEnumerable<IngredientData>        AllIngredients        => m_Table?.Ingredients.All;
        public IEnumerable<RecipeData>            AllRecipes            => m_Table?.Recipes.All;
        public IEnumerable<FoodData>              AllFoods              => m_Table?.Foods.All;
        public IEnumerable<WeaponData>            AllWeapons            => m_Table?.Weapons.All;
        public IEnumerable<DungeonSpawnTableData> AllDungeonSpawnTables => m_Table?.DungeonSpawnTables.All;
        public IEnumerable<SkillGroupData>        AllSkillGroups        => m_Table?.SkillGroups.All;
        public IEnumerable<PlayerCharData>        AllPlayers            => m_Table?.Players.All;
    }
}
