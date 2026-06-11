using MonsterKitchen.Core;
using UnityEngine;

namespace MonsterKitchen.Data
{
    // ====================================================================
    //  DataRegistry — 런타임 ID → 정적 데이터 조회 레지스트리
    //
    //  ▶ 역할
    //    TableData SO 를 보유하고 편의 조회 API 를 제공한다.
    //    역방향 캐시(문자열 ID 조회 등)는 각 특화 Table 클래스가 소유.
    //    순수 C# 클래스. GlobalController 가 생성·관리한다.
    //
    //  ▶ 초기화 순서
    //    GlobalController.InitManagers() → Registry.Init()  (Instance 등록)
    //    GameStartup.StepDataLoad()      → Registry.Load()  (TableData SO 로드)
    //
    //  ▶ 사용법
    //    DataRegistry.Instance.Monsters.Get(id)
    //    DataRegistry.Instance.SkillGroups.FindByStringId("SGD_001")
    //    DataRegistry.Instance.UIPanels.FindUIByPanelId("CookingUI")
    //    DataRegistry.Instance.Strings.FindByStringId("MON_001_NAME")
    //    GlobalController.Instance.Registry.IsReady
    // ====================================================================

    public class DataRegistry
    {
        public static DataRegistry Instance { get; private set; }

        TableData m_Table;

        /// <summary>TableData 직접 접근. DataTable&lt;T&gt; API 를 그대로 사용할 수 있다.</summary>
        public TableData Table => m_Table;

        /// <summary>TableData 로드 완료 여부. GameStartup.StepDataLoad 이후 true.</summary>
        public bool IsReady => m_Table != null;

        // ── 특화 테이블 직접 접근 ─────────────────────────────────────────
        public MonsterTable       Monsters           => m_Table?.Monsters;
        public IngredientTable    Ingredients        => m_Table?.Ingredients;
        public FoodTable          Foods              => m_Table?.Foods;
        public RecipeTable        Recipes            => m_Table?.Recipes;
        public DropTable          DropTables         => m_Table?.DropTables;
        public DungeonSpawnTable  DungeonSpawnTables => m_Table?.DungeonSpawnTables;
        public GatheringToolTable GatheringTools     => m_Table?.GatheringTools;
        public ResourceNodeTable  ResourceNodes      => m_Table?.ResourceNodes;
        public UITable            UIPanels           => m_Table?.UIPanels;
        public SkillStepTable     SkillSteps         => m_Table?.SkillSteps;
        public SkillGroupTable    SkillGroups        => m_Table?.SkillGroups;
        public WeaponTable        Weapons            => m_Table?.Weapons;
        public PlayerCharTable    PlayerChars        => m_Table?.PlayersChar;
        public StringTable        Strings            => m_Table?.Strings;

        // ================================================================
        //  초기화
        // ================================================================

        /// <summary>
        /// foodId 를 결과물로 갖는 레시피를 반환한다.
        /// 레시피 수가 적어 선형 탐색(O(n)) 허용. 없으면 null.
        /// </summary>
        public RecipeData GetRecipeByFoodId(uint foodId)
        {
            if (m_Table?.Recipes == null) return null;
            foreach (var recipe in m_Table.Recipes.All)
            {
                if (recipe.ResultFoodId == foodId) return recipe;
            }
            return null;
        }

        public void Init() => Instance = this;

        /// <summary>
        /// TableData SO 를 AssetManifest 에서 로드한다.
        /// GameStartup.StepDataLoad() 에서 호출.
        /// </summary>
        public void Load()
        {
            m_Table = AssetLoadManager.Instance?.Load<TableData>(AssetKeys.DATA_TABLE_DATA);
            if (m_Table == null)
            {
                DebugUtil.LogError("[DataRegistry] TableData 로드 실패 — AssetManifest 에 'data/table_data' 키 등록 여부 확인.", null);
                return;
            }

            // 역방향 캐시 빌드 — 모든 테이블 RuntimeSetData 일괄 호출
            foreach (var table in m_Table.AllTables())
                table.RuntimeSetData();

            // 재료 Weight 검증 — 0 이하 시 1 로 보정
            if (m_Table.Ingredients != null)
            {
                foreach (var ing in m_Table.Ingredients.All)
                {
                    if (ing.Weight <= 0)
                    {
                        DebugUtil.LogWarning($"[DataRegistry] 재료 ID:{ing.Id} Weight={ing.Weight} — 1 로 보정.");
                        ing.Weight = 1;
                    }
                }
            }

            DebugUtil.Log(
                $"[DataRegistry] TableData 로드 완료 | Monsters:{m_Table.Monsters.Count} Ingredients:{m_Table.Ingredients.Count} Foods:{m_Table.Foods.Count} Recipes:{m_Table.Recipes.Count} DropTables:{m_Table.DropTables.Count} DungeonSpawns:{m_Table.DungeonSpawnTables.Count} GatheringTools:{m_Table.GatheringTools.Count} UIPanels:{m_Table.UIPanels.Count} SkillSteps:{m_Table.SkillSteps.Count} SkillGroups:{m_Table.SkillGroups.Count} Weapons:{m_Table.Weapons.Count} Players:{m_Table.PlayersChar.Count} Strings:{m_Table.Strings.Count}");

            // LocaleManager 초기화 (StringTable 캐시 빌드 이후)
            LocaleManager.Instance?.OnDataLoaded();
        }
    }
}
