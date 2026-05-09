using System.Collections.Generic;
using MonsterKitchen.Core;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace MonsterKitchen.Data
{
    /// <summary>
    /// 런타임 ID → ScriptableObject 조회 레지스트리. DontDestroyOnLoad 싱글톤.
    ///
    /// ┌─ MVP (현재) ────────────────────────────────────────────────────────┐
    /// │ Start()에서 AssetLoadManager.LoadAll<T>() 로 동기 로드.            │
    /// │ AssetManifest에 SO가 등록되어 있으면 자동으로 인식.                 │
    /// └─────────────────────────────────────────────────────────────────────┘
    ///
    /// ┌─ Phase 2 (Addressables 마이그레이션) ──────────────────────────────┐
    /// │ Start()의 LoadFromAssetManager() 호출을 LoadAllAsync() 호출로 교체. │
    /// │ Addressables 레이블: MonsterData / IngredientData / RecipeData /   │
    /// │   FoodData / DropTableData                                         │
    /// └─────────────────────────────────────────────────────────────────────┘
    /// </summary>
    public class DataRegistry : MonoBehaviour
    {
        public static DataRegistry Instance { get; private set; }

        // ── 테이블 ───────────────────────────────────────────────────
        readonly Dictionary<string, MonsterData>    _monsters    = new();
        readonly Dictionary<string, IngredientData> _ingredients = new();
        readonly Dictionary<string, RecipeData>     _recipes     = new();
        readonly Dictionary<string, FoodData>       _foods       = new();
        readonly Dictionary<string, DropTableData>  _dropTables  = new();

        public bool IsReady { get; private set; }

        // ── Unity ────────────────────────────────────────────────────

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void Start()
        {
            // MVP: AssetLoadManager 경유 동기 로드.
            // Phase 2: 아래 줄을 주석 처리하고 StartCoroutine / Awaitable 로드로 교체.
            LoadFromAssetManager();
        }

        // ── MVP 로드 ─────────────────────────────────────────────────

        /// <summary>
        /// AssetLoadManager에 등록된 모든 SO를 타입별로 일괄 등록한다.
        /// Addressables 마이그레이션 시 이 메서드 대신 LoadAllAsync()를 사용.
        /// </summary>
        public void LoadFromAssetManager()
        {
            var loader = AssetLoadManager.Instance;
            if (loader == null)
            {
                Debug.LogError("[DataRegistry] AssetLoadManager 인스턴스 없음. GameManager에 컴포넌트가 추가됐는지 확인하세요.");
                return;
            }

            IsReady = false;
            _monsters.Clear(); _ingredients.Clear(); _recipes.Clear();
            _foods.Clear();    _dropTables.Clear();

            foreach (var so in loader.LoadAll<MonsterData>())    RegisterById(so, _monsters);
            foreach (var so in loader.LoadAll<IngredientData>()) RegisterById(so, _ingredients);
            foreach (var so in loader.LoadAll<RecipeData>())     RegisterById(so, _recipes);
            foreach (var so in loader.LoadAll<FoodData>())       RegisterById(so, _foods);
            foreach (var so in loader.LoadAll<DropTableData>())  RegisterById(so, _dropTables);

            IsReady = true;
            Debug.Log($"[DataRegistry] MVP 로드 완료: monsters={_monsters.Count}, " +
                      $"ingredients={_ingredients.Count}, recipes={_recipes.Count}, " +
                      $"foods={_foods.Count}, dropTables={_dropTables.Count}");
        }

        static void RegisterById<T>(T so, Dictionary<string, T> dict) where T : ScriptableObject
        {
            string id = GetId(so);
            if (string.IsNullOrEmpty(id))
            {
                Debug.LogWarning($"[DataRegistry] '{so.name}' 에 id 필드가 없거나 비어 있습니다.");
                return;
            }
            if (!dict.TryAdd(id, so))
                Debug.LogWarning($"[DataRegistry] 중복 id '{id}' — 첫 번째 항목 유지.");
        }

        // ── Phase 2: Addressables 비동기 로드 ───────────────────────

        /// <summary>
        /// Addressables 레이블로 전체 SO를 비동기 로드한다.
        /// Phase 2에서 Start()의 LoadFromAssetManager() 대신 호출.
        /// </summary>
        public async Awaitable LoadAllAsync()
        {
            IsReady = false;

            await LoadLabelAsync<MonsterData>   ("MonsterData",    _monsters);
            await LoadLabelAsync<IngredientData>("IngredientData", _ingredients);
            await LoadLabelAsync<RecipeData>    ("RecipeData",     _recipes);
            await LoadLabelAsync<FoodData>      ("FoodData",       _foods);
            await LoadLabelAsync<DropTableData> ("DropTableData",  _dropTables);

            IsReady = true;
            Debug.Log($"[DataRegistry] Addressables 로드 완료: monsters={_monsters.Count}, " +
                      $"ingredients={_ingredients.Count}, recipes={_recipes.Count}, " +
                      $"foods={_foods.Count}, dropTables={_dropTables.Count}");
        }

        async Awaitable LoadLabelAsync<T>(string label, Dictionary<string, T> dict) where T : ScriptableObject
        {
            var handle = Addressables.LoadAssetsAsync<T>(label, null);
            await handle.Task;

            if (handle.Status != AsyncOperationStatus.Succeeded)
            {
                Debug.LogWarning($"[DataRegistry] Addressables 레이블 '{label}' 로드 실패: {handle.OperationException?.Message}");
                return;
            }

            dict.Clear();
            foreach (var so in handle.Result)
            {
                string id = GetId(so);
                if (!string.IsNullOrEmpty(id))
                    dict[id] = so;
            }
        }

        static string GetId(ScriptableObject so)
        {
            var field = so.GetType().GetField("id");
            return field?.GetValue(so) as string;
        }

        // ── 조회 API ─────────────────────────────────────────────────

        public MonsterData    GetMonster   (string id) => _monsters.TryGetValue(id, out var v)    ? v : null;
        public IngredientData GetIngredient(string id) => _ingredients.TryGetValue(id, out var v) ? v : null;
        public RecipeData     GetRecipe    (string id) => _recipes.TryGetValue(id, out var v)     ? v : null;
        public FoodData       GetFood      (string id) => _foods.TryGetValue(id, out var v)       ? v : null;
        public DropTableData  GetDropTable (string id) => _dropTables.TryGetValue(id, out var v)  ? v : null;

        public IEnumerable<MonsterData>    AllMonsters    => _monsters.Values;
        public IEnumerable<IngredientData> AllIngredients => _ingredients.Values;
        public IEnumerable<RecipeData>     AllRecipes     => _recipes.Values;
        public IEnumerable<FoodData>       AllFoods       => _foods.Values;
        public IEnumerable<DropTableData>  AllDropTables  => _dropTables.Values;
    }
}
