#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MonsterKitchen;
using MonsterKitchen.Core;
using MonsterKitchen.Data;
using MonsterKitchen.Data.Pipeline;
using UnityEditor;
using UnityEngine;

namespace MonsterKitchen.Editor
{
    public class DataManagerWindow : EditorWindow
    {
        // ── 경로 상수 ────────────────────────────────────────────────
        const string CSV_ROOT           = "Assets/Data/CSV";
        const string TABLE_DATA_PATH    = "Assets/Data/SO/TableData.asset";
        const string MANIFEST_PATH      = "Assets/Data/AssetManifest.asset";
        const int    ROW_H    = 20;
        const int    DETAIL_H = 180;

        // ── 테이블 메타 ─────────────────────────────────────────────
        // 주의: 동기화 순서가 중요하다.
        //   SkillSteps(6) → SkillGroups(7) → Weapons(8) → Gathering Tools(9) → Players(10)
        //   Weapons 의 normalAttackGroup 은 SkillGroups 가 먼저 동기화되어야 해결된다.
        //   Monsters 의 skillGroups 는 SyncAllSO 의 ResolveAllReferences() 에서 해결된다.
        static readonly string[] TABLE_LABELS = {
            "Monsters", "Ingredients", "Recipes", "Foods", "Drop Tables", "Dungeon Spawn Tables",
            "Skill Steps", "Skill Groups", "Weapons", "Gathering Tools", "Players"
        };
        static readonly string[] TABLE_FILES  = {
            "Monsters.csv", "Ingredients.csv", "Recipes.csv", "Foods.csv",
            "DropTables.csv", "DungeonSpawnTables.csv",
            "SkillSteps.csv", "SkillGroups.csv", "Weapons.csv", "GatheringTools.csv", "Players.csv"
        };

        static readonly string[][] DEFAULT_HEADERS =
        {
            // 0: Monsters
            new[] { "_key","Id","DisplayName","Description","Hp","Attack","Defense","MoveSpeed","Attribute","Rarity","DropTableId","SkillGroupIds","ImmuneToKnockback","ImmuneToStun","ImmuneToPullIn","PrefabAddress","SpriteAddress","BtAssetAddress" },
            // 1: Ingredients
            new[] { "_key","Id","DisplayName","Description","Attribute","Rarity","DefaultState","SourceMonsterIds" },
            // 2: Recipes
            new[] { "_key","Id","DisplayName","Description","Ingredients","ResultFoodId","CookTimeSeconds","UnlockDay","IsUnlockedByDefault" },
            // 3: Foods
            new[] { "_key","Id","DisplayName","Description","BasePrice","HpRestore","BuffAttribute","BuffMultiplier","BuffDurationDays" },
            // 4: Drop Tables
            new[] { "_key","Id","Entries" },
            // 5: Dungeon Spawn Tables
            new[] { "_key","Id","Monsters" },
            // 6: Skill Steps
            new[] { "_key","Id","SkillId","SkillName","Cooltime","ComboWindow","DamageMultiplier","SearchRange","AttackRange","MaxTargets","MissileSpeed","MissileMaxRange","CcForce","CcDuration","StunDuration","AnimTriggerOverride","ProjectilePrefabAddress" },
            // 7: Skill Groups
            new[] { "_key","Id","SkillGroupId","SkillName","Description","SkillIconAddress","SkillCooltime","AllowedWeaponTypes","StepIds" },
            // 8: Weapons
            new[] { "_key","Id","WeaponName","WeaponType","Abils","MaxDurability","DecayMode","NormalAttackGroupId","WeaponSpriteAddress","WeaponAnimAddress" },
            // 9: Gathering Tools
            new[] { "_key","Id","ToolName","ToolType","MaxDurability","GatherMultiplier","SpeedMultiplier","CompatibleNodeTypes" },
            // 10: Players
            new[] { "_key","Id","DisplayName","PrefabAddress","BaseMaxHp","BaseAttack","BaseMoveSpeed","BaseDefense","AttackAttribute","DefaultWeaponId","SkillGroupId1","SkillGroupId2","UltimateSkillGroupId","MaxUltimateGauge","GaugeOnHit","GaugeOnKill" },
        };

        // ── 창 상태 ──────────────────────────────────────────────────
        int    _tableIdx = 0;
        int    _selRow   = -1;
        string _search   = "";
        int    _sortCol  = 0;
        bool   _sortAsc  = true;
        bool   _dirty    = false;

        Vector2 _tableScroll;
        Vector2 _detailScroll;
        Vector2 _manifestScroll;

        bool _showManifest = false;
        List<string> _manifestMissing;

        CsvParser.ParseResult            _parsed;
        List<Dictionary<string, string>> _filtered;

        // ================================================================
        //  메뉴
        // ================================================================

        [MenuItem("MonsterKitchen/Data Manager")]
        public static void Open()
        {
            var w = GetWindow<DataManagerWindow>("Data Manager");
            w.minSize = new Vector2(860, 560);
        }

        [MenuItem("MonsterKitchen/Sync All SO")]
        public static void SyncAllSO()
        {
            int total = 0;
            try
            {
                for (int i = 0; i < TABLE_FILES.Length; i++)
                {
                    EditorUtility.DisplayProgressBar("Syncing All SO",
                        $"[{i+1}/{TABLE_FILES.Length}] {TABLE_LABELS[i]}...",
                        (float)i / TABLE_FILES.Length);
                    total += SyncTableAtIndex(i);
                }

                // 크로스 테이블 참조 해결
                EditorUtility.DisplayProgressBar("Syncing All SO", "참조 해결 중...", 1f);
                ResolveAllReferences();
            }
            finally { EditorUtility.ClearProgressBar(); }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[DataManager] Sync All 완료 — 총 {total}개 항목");
            EditorUtility.DisplayDialog("Sync All SO", $"전체 동기화 완료\n총 {total}개 항목", "OK");
        }

        /// <summary>
        /// 모든 테이블 동기화 후 크로스 테이블 참조를 해결한다.
        /// MonsterData.skillGroups ← skillGroupIds 로 SkillGroups 조회.
        /// WeaponData.normalAttackGroup ← normalAttackGroupId 로 SkillGroups 조회.
        /// </summary>
        static void ResolveAllReferences()
        {
            TableData td = GetOrCreateTableData();

            // SkillGroupData 역방향 캐시 (skillGroupId 문자열 → 인스턴스)
            var sgByStringId = new System.Collections.Generic.Dictionary<string, SkillGroupData>();
            foreach (var sg in td.SkillGroups.All)
                if (!string.IsNullOrEmpty(sg.SkillGroupId))
                    sgByStringId[sg.SkillGroupId] = sg;

            // Monsters: skillGroupIds → skillGroups
            foreach (var monster in td.Monsters.All)
            {
                if (monster.SkillGroupIds == null || monster.SkillGroupIds.Length == 0)
                {
                    monster.SkillGroups = System.Array.Empty<SkillGroupData>();
                    continue;
                }
                var list = new System.Collections.Generic.List<SkillGroupData>();
                foreach (var sgId in monster.SkillGroupIds)
                {
                    if (string.IsNullOrWhiteSpace(sgId)) continue;
                    if (sgByStringId.TryGetValue(sgId.Trim(), out var sg))
                        list.Add(sg);
                    else
                        Debug.LogWarning($"[DataManager] Monster({monster.Id}) skillGroupId '{sgId}' 를 SkillGroups 에서 찾을 수 없습니다.");
                }
                monster.SkillGroups = list.ToArray();
            }

            // Weapons: normalAttackGroupId → normalAttackGroup
            foreach (var weapon in td.Weapons.All)
            {
                if (string.IsNullOrEmpty(weapon.NormalAttackGroupId))
                {
                    weapon.NormalAttackGroup = null;
                    continue;
                }
                if (sgByStringId.TryGetValue(weapon.NormalAttackGroupId.Trim(), out var sg))
                    weapon.NormalAttackGroup = sg;
                else
                    Debug.LogWarning($"[DataManager] Weapon({weapon.Id}) normalAttackGroupId '{weapon.NormalAttackGroupId}' 를 SkillGroups 에서 찾을 수 없습니다.");
            }

            EditorUtility.SetDirty(td);
            Debug.Log($"[DataManager] 참조 해결 완료 — Monster {td.Monsters.Count}개, Weapon {td.Weapons.Count}개");
        }

        [MenuItem("MonsterKitchen/Check Manifest")]
        public static void CheckManifestMenu()
        {
            var missing = GetMissingManifestKeys();
            if (missing.Count == 0)
                EditorUtility.DisplayDialog("Manifest Check", "모든 에셋 키가 AssetManifest에 등록되어 있습니다.", "OK");
            else
                EditorUtility.DisplayDialog("Manifest Check — 누락 키",
                    $"누락된 키 {missing.Count}개:\n\n" + string.Join("\n", missing.Take(20))
                    + (missing.Count > 20 ? $"\n... 외 {missing.Count - 20}개" : ""), "OK");
        }

        // ================================================================
        //  SO 동기화 — 테이블별 fieldMapper 포함
        // ================================================================

        static TableData GetOrCreateTableData()
        {
            var td = AssetDatabase.LoadAssetAtPath<TableData>(TABLE_DATA_PATH);
            if (td != null) return td;

            Directory.CreateDirectory(
                Path.GetDirectoryName(Path.Combine(
                    Application.dataPath.Replace("Assets", ""), TABLE_DATA_PATH)) ?? "");
            td = ScriptableObject.CreateInstance<TableData>();
            AssetDatabase.CreateAsset(td, TABLE_DATA_PATH);
            AssetDatabase.SaveAssets();
            Debug.Log($"[DataManager] TableData 생성: {TABLE_DATA_PATH}");
            return td;
        }

        static int SyncTableAtIndex(int idx)
        {
            string root    = Application.dataPath[..^"Assets".Length];
            string csvPath = Path.Combine(root, CSV_ROOT, TABLE_FILES[idx]);

            if (!File.Exists(csvPath))
            {
                Debug.LogWarning($"[DataManager] CSV 없음: {TABLE_FILES[idx]}");
                return 0;
            }

            var parsed = CsvReadWriter.ReadFromFullPath(csvPath);
            if (parsed?.Rows == null || parsed.Rows.Count == 0) return 0;

            TableData td = GetOrCreateTableData();
            return idx switch
            {
                0 => ScriptableObjectSync.Sync<MonsterData>           (parsed, td.Monsters,           td, (d, row) => MonsterMapper(d, row)),
                1 => ScriptableObjectSync.Sync<IngredientData>        (parsed, td.Ingredients,        td, IngredientMapper),
                2 => ScriptableObjectSync.Sync<RecipeData>            (parsed, td.Recipes,            td, RecipeMapper),
                3 => ScriptableObjectSync.Sync<FoodData>              (parsed, td.Foods,              td, FoodMapper),
                4 => ScriptableObjectSync.Sync<DropTableData>         (parsed, td.DropTables,         td, DropTableMapper),
                5 => ScriptableObjectSync.Sync<DungeonSpawnTableData> (parsed, td.DungeonSpawnTables, td, DungeonSpawnTableMapper),
                6 => ScriptableObjectSync.Sync<SkillData>             (parsed, td.SkillSteps,         td, SkillStepMapper),
                7 => ScriptableObjectSync.Sync<SkillGroupData>        (parsed, td.SkillGroups,        td, (d, row) => SkillGroupMapper(d, row, td)),
                8 => ScriptableObjectSync.Sync<WeaponData>            (parsed, td.Weapons,            td, (d, row) => WeaponMapper(d, row, td)),
                9 => ScriptableObjectSync.Sync<GatheringToolData>     (parsed, td.GatheringTools,     td, GatheringToolMapper),
               10 => ScriptableObjectSync.Sync<PlayerCharData>         (parsed, td.Players,            td, PlayerMapper),
                _ => 0,
            };
        }

        // ── 테이블별 fieldMapper ─────────────────────────────────────

        /// <summary>MonsterData: prefabAddress / spriteAddress / btAssetAddress 자동 생성 + skillGroupIds 파싱</summary>
        static void MonsterMapper(MonsterData d, Dictionary<string, string> row)
        {
            // CSV 값이 비어있을 때만 id 기반으로 자동 생성 (CSV에 명시된 값 우선)
            if (string.IsNullOrEmpty(d.PrefabAddress))
                d.PrefabAddress  = AssetKeys.MonsterPrefab(d.Id);
            if (string.IsNullOrEmpty(d.SpriteAddress))
                d.SpriteAddress  = AssetKeys.MonsterSprite(d.Id);
            if (string.IsNullOrEmpty(d.BtAssetAddress))
                d.BtAssetAddress = AssetKeys.MonsterBT(d.Id);

            // skillGroupIds 파싱 ("SGD_004|SGD_005" → string[])
            if (row.TryGetValue("SkillGroupIds", out string sgidsRaw) && !string.IsNullOrWhiteSpace(sgidsRaw))
            {
                var parts = sgidsRaw.Split('|');
                var list = new System.Collections.Generic.List<string>();
                foreach (var p in parts)
                {
                    var trimmed = p.Trim();
                    if (!string.IsNullOrEmpty(trimmed)) list.Add(trimmed);
                }
                d.SkillGroupIds = list.ToArray();
            }
            // skillGroups 는 ResolveAllReferences() 에서 해결됨
        }

        /// <summary>IngredientData: spriteAddress 자동 생성</summary>
        static void IngredientMapper(IngredientData d, Dictionary<string, string> _)
        {
            // CSV에 주소 컬럼이 없으므로 항상 id 기반으로 재생성
            d.SpriteAddress = AssetKeys.IngredientSprite(d.Id);
        }

        /// <summary>FoodData: spriteAddress 자동 생성</summary>
        static void FoodMapper(FoodData d, Dictionary<string, string> _)
        {
            // CSV에 주소 컬럼이 없으므로 항상 id 기반으로 재생성
            d.SpriteAddress = AssetKeys.FoodSprite(d.Id);
        }

        /// <summary>RecipeData: ingredients 배열 파싱 ("2001:2|2002:1" 형식)</summary>
        static void RecipeMapper(RecipeData d, Dictionary<string, string> row)
        {
            if (!row.TryGetValue("Ingredients", out string raw) || string.IsNullOrWhiteSpace(raw))
                return;

            var list = new List<RecipeIngredient>();
            foreach (var part in raw.Split('|'))
            {
                var segs = part.Trim().Split(':');
                if (segs.Length >= 2
                    && uint.TryParse(segs[0].Trim(), out uint ingId)
                    && int.TryParse(segs[1].Trim(), out int qty))
                {
                    list.Add(new RecipeIngredient { IngredientId = ingId, Quantity = qty });
                }
            }
            d.Ingredients = list.ToArray();
        }

        /// <summary>SkillData: projectilePrefabAddress 자동 생성</summary>
        static void SkillStepMapper(SkillData d, Dictionary<string, string> row)
        {
            if (string.IsNullOrEmpty(d.ProjectilePrefabAddress) && !string.IsNullOrEmpty(d.SkillId))
                d.ProjectilePrefabAddress = AssetKeys.ProjectilePrefab(d.SkillId);
        }

        /// <summary>SkillGroupData: stepIds 파싱 → skillChain 재구성, skillIconAddress 자동 생성, allowedWeaponTypes 파싱</summary>
        static void SkillGroupMapper(SkillGroupData d, Dictionary<string, string> row, TableData td)
        {
            // skillIconAddress 자동 생성
            if (string.IsNullOrEmpty(d.SkillIconAddress) && !string.IsNullOrEmpty(d.SkillGroupId))
                d.SkillIconAddress = AssetKeys.SkillIcon(d.SkillGroupId);

            // allowedWeaponTypes 파싱 ("Sword|DualSword" 형식)
            if (row.TryGetValue("AllowedWeaponTypes", out string wtRaw) && !string.IsNullOrWhiteSpace(wtRaw))
            {
                var list = new List<WeaponType>();
                foreach (var part in wtRaw.Split('|'))
                    if (System.Enum.TryParse<WeaponType>(part.Trim(), true, out var wt))
                        list.Add(wt);
                d.AllowedWeaponTypes = list;
            }

            // stepIds 파싱 ("11001|11002|11003" 형식) → d.stepIds
            if (row.TryGetValue("StepIds", out string stepsRaw) && !string.IsNullOrWhiteSpace(stepsRaw))
            {
                var parts = stepsRaw.Split('|');
                var ids = new System.Collections.Generic.List<string>();
                foreach (var p in parts)
                {
                    var trimmed = p.Trim();
                    if (!string.IsNullOrEmpty(trimmed)) ids.Add(trimmed);
                }
                d.StepIds = ids.ToArray();
            }

            // skillChain 재구성: stepIds → SkillSteps 테이블 조회
            d.SkillChain = new List<SkillData>();
            if (d.StepIds != null)
            {
                foreach (var stepIdStr in d.StepIds)
                {
                    if (uint.TryParse(stepIdStr, out uint stepId))
                    {
                        var step = td.SkillSteps.Get(stepId);
                        if (step != null)
                            d.SkillChain.Add(step);
                        else
                            Debug.LogWarning($"[DataManager] SkillGroup({d.SkillGroupId}) stepId {stepId} 를 SkillSteps 에서 찾을 수 없습니다.");
                    }
                }
            }
        }

        /// <summary>WeaponData: abils 배열 파싱, address 자동 생성, normalAttackGroup 해결</summary>
        static void WeaponMapper(WeaponData d, Dictionary<string, string> row, TableData td)
        {
            // abils 파싱
            if (row.TryGetValue("Abils", out string abilsRaw) && !string.IsNullOrWhiteSpace(abilsRaw))
            {
                var list = new List<AbilEntry>();
                foreach (var part in abilsRaw.Split('|'))
                {
                    var segs = part.Trim().Split(':');
                    if (segs.Length >= 2
                        && System.Enum.TryParse<AbilType>(segs[0].Trim(), true, out var abilType)
                        && float.TryParse(segs[1].Trim(),
                            System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture,
                            out float val))
                    {
                        list.Add(new AbilEntry(abilType, val));
                    }
                }
                d.Abils = list;
            }

            // address 자동 생성 (빈 경우만)
            if (string.IsNullOrEmpty(d.WeaponSpriteAddress))
                d.WeaponSpriteAddress = AssetKeys.WeaponSprite(d.Id);
            if (string.IsNullOrEmpty(d.WeaponAnimAddress))
                d.WeaponAnimAddress = AssetKeys.WeaponAnim(d.Id);

            // normalAttackGroup 해결 (SkillGroups 테이블에서 문자열 ID로 조회)
            if (!string.IsNullOrEmpty(d.NormalAttackGroupId))
            {
                d.NormalAttackGroup = null;
                foreach (var sg in td.SkillGroups.All)
                {
                    if (sg.SkillGroupId == d.NormalAttackGroupId)
                    {
                        d.NormalAttackGroup = sg;
                        break;
                    }
                }
                if (d.NormalAttackGroup == null)
                    Debug.LogWarning($"[DataManager] Weapon({d.Id}) normalAttackGroupId '{d.NormalAttackGroupId}' 를 SkillGroups 에서 찾을 수 없습니다. (SkillGroups 를 먼저 동기화하세요.)");
            }
        }

        /// <summary>PlayerCharData: prefabAddress 자동 생성</summary>
        static void PlayerMapper(PlayerCharData d, Dictionary<string, string> row)
        {
            if (string.IsNullOrEmpty(d.PrefabAddress))
                d.PrefabAddress = AssetKeys.PlayerCharPrefab(d.Id);
        }

        /// <summary>GatheringToolData: compatibleNodeTypes 배열 파싱 ("Tree|Rock|Ore" 형식)</summary>
        static void GatheringToolMapper(GatheringToolData d, Dictionary<string, string> row)
        {
            if (!row.TryGetValue("CompatibleNodeTypes", out string raw) || string.IsNullOrWhiteSpace(raw))
                return;

            var list = new List<ResourceNodeType>();
            foreach (var part in raw.Split('|'))
            {
                if (System.Enum.TryParse<ResourceNodeType>(part.Trim(), true, out var nodeType))
                    list.Add(nodeType);
            }
            d.CompatibleNodeTypes = list.ToArray();
        }

        /// <summary>DungeonSpawnTableData: monsters 배열 파싱 ("1001:2:0.5|..." 형식)</summary>
        static void DungeonSpawnTableMapper(DungeonSpawnTableData d, Dictionary<string, string> row)
        {
            if (!row.TryGetValue("Monsters", out string raw) || string.IsNullOrWhiteSpace(raw))
                return;

            var list = new List<MonsterSpawnEntry>();
            foreach (var part in raw.Split('|'))
            {
                var segs = part.Trim().Split(':');
                if (segs.Length >= 2
                    && uint.TryParse(segs[0].Trim(), out uint monId)
                    && int.TryParse(segs[1].Trim(), out int count))
                {
                    float radius = segs.Length >= 3
                        && float.TryParse(segs[2].Trim(),
                            System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture,
                            out float r) ? r : 0.5f;
                    list.Add(new MonsterSpawnEntry { MonsterId = monId, Count = count, SpawnRadius = radius });
                }
            }
            d.Monsters = list;
        }

        /// <summary>DropTableData: entries 배열 파싱 ("2001:0.8:1:2|..." 형식)</summary>
        static void DropTableMapper(DropTableData d, Dictionary<string, string> row)
        {
            if (!row.TryGetValue("Entries", out string raw) || string.IsNullOrWhiteSpace(raw))
                return;

            var list = new List<DropEntry>();
            foreach (var part in raw.Split('|'))
            {
                var segs = part.Trim().Split(':');
                if (segs.Length >= 4
                    && uint.TryParse(segs[0].Trim(), out uint ingId)
                    && float.TryParse(segs[1].Trim(),
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out float chance)
                    && int.TryParse(segs[2].Trim(), out int minQ)
                    && int.TryParse(segs[3].Trim(), out int maxQ))
                {
                    list.Add(new DropEntry
                    {
                        IngredientId = ingId,
                        DropChance   = Mathf.Clamp01(chance),
                        MinQuantity  = minQ,
                        MaxQuantity  = maxQ,
                    });
                }
            }
            d.Entries = list.ToArray();
        }

        // ================================================================
        //  Manifest 누락 키 검사
        // ================================================================

        static List<string> GetMissingManifestKeys()
        {
            var manifest = AssetDatabase.LoadAssetAtPath<AssetManifest>(MANIFEST_PATH);
            var td       = AssetDatabase.LoadAssetAtPath<TableData>(TABLE_DATA_PATH);
            if (manifest == null || td == null) return new List<string> { "AssetManifest 또는 TableData 없음" };

            var registered = manifest.BuildDictionary().Keys.ToHashSet();
            var missing    = new List<string>();

            void Check(string key, string hint)
            {
                if (!registered.Contains(key))
                    missing.Add($"{key}  [{hint}]");
            }

            foreach (var d in td.Monsters.All)
            {
                if (!string.IsNullOrEmpty(d.PrefabAddress))  Check(d.PrefabAddress,  d.DisplayName);
                if (!string.IsNullOrEmpty(d.SpriteAddress))  Check(d.SpriteAddress,  d.DisplayName);
                if (!string.IsNullOrEmpty(d.BtAssetAddress)) Check(d.BtAssetAddress, d.DisplayName + " BT");
            }
            foreach (var d in td.Ingredients.All)
                if (!string.IsNullOrEmpty(d.SpriteAddress))  Check(d.SpriteAddress,  d.DisplayName);
            foreach (var d in td.Foods.All)
                if (!string.IsNullOrEmpty(d.SpriteAddress))  Check(d.SpriteAddress,  d.DisplayName);
            foreach (var d in td.Players.All)
                if (!string.IsNullOrEmpty(d.PrefabAddress))  Check(d.PrefabAddress,  d.DisplayName);
            foreach (var d in td.SkillSteps.All)
                if (!string.IsNullOrEmpty(d.ProjectilePrefabAddress) && d.MissileSpeed > 0f)
                    Check(d.ProjectilePrefabAddress, d.SkillName + " Projectile");

            return missing;
        }

        // ================================================================
        //  OnEnable / OnDestroy
        // ================================================================

        void OnEnable()  { DoReload(); }
        void OnDestroy() { DoPromptSave(); }

        // ================================================================
        //  GUI
        // ================================================================

        void OnGUI()
        {
            DrawToolbar();
            DrawTable();
            DrawDetail();
            if (_showManifest) DrawManifestChecker();
        }

        void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            int newIdx = EditorGUILayout.Popup(_tableIdx, TABLE_LABELS,
                EditorStyles.toolbarPopup, GUILayout.Width(120));
            if (newIdx != _tableIdx)
            {
                DoPromptSave();
                _tableIdx = newIdx;
                _selRow   = -1;
                DoReload();
            }

            GUILayout.Space(8);
            EditorGUILayout.LabelField("Search:", GUILayout.Width(46));
            string ns = EditorGUILayout.TextField(_search,
                EditorStyles.toolbarSearchField, GUILayout.Width(160));
            if (ns != _search) { _search = ns; DoFilter(); }

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("+ Row",    EditorStyles.toolbarButton, GUILayout.Width(56))) DoAddRow();
            GUI.enabled = _selRow >= 0;
            if (GUILayout.Button("- Row",    EditorStyles.toolbarButton, GUILayout.Width(56))) DoRemoveRow();
            GUI.enabled = true;
            GUI.enabled = _dirty;
            if (GUILayout.Button("Save",     EditorStyles.toolbarButton, GUILayout.Width(50)))  DoSave();
            GUI.enabled = true;
            if (GUILayout.Button("Sync SO",  EditorStyles.toolbarButton, GUILayout.Width(62)))  DoSyncSO();
            if (GUILayout.Button("Sync All", EditorStyles.toolbarButton, GUILayout.Width(66)))  SyncAllSO();
            if (GUILayout.Button("Reload",   EditorStyles.toolbarButton, GUILayout.Width(56)))  DoReload();

            bool wasShow = _showManifest;
            _showManifest = GUILayout.Toggle(_showManifest, "Manifest",
                EditorStyles.toolbarButton, GUILayout.Width(66));
            if (_showManifest && !wasShow) RefreshManifestCheck();

            EditorGUILayout.EndHorizontal();
        }

        void DrawTable()
        {
            if (_parsed == null || _parsed.Headers == null)
            {
                EditorGUILayout.HelpBox("No CSV data. Click Reload.", MessageType.Info);
                return;
            }

            var cols = _parsed.Headers
                .Select((h, i) => new { h, i })
                .Where(x => !CsvParser.IsIgnoredColumn(x.h))
                .ToArray();
            if (cols.Length == 0) return;

            float colW = Mathf.Max(70, (position.width - 24) / cols.Length);

            // 헤더 행
            EditorGUILayout.BeginHorizontal(GUI.skin.box);
            foreach (var col in cols)
            {
                string lbl = col.h + (_sortCol == col.i ? (_sortAsc ? " ▲" : " ▼") : "");
                if (GUILayout.Button(lbl, EditorStyles.miniButtonMid, GUILayout.Width(colW)))
                {
                    if (_sortCol == col.i) _sortAsc = !_sortAsc;
                    else { _sortCol = col.i; _sortAsc = true; }
                    DoFilter();
                }
            }
            EditorGUILayout.EndHorizontal();

            // 데이터 행
            float usedH = DETAIL_H + (_showManifest ? 120 : 0) + 80;
            _tableScroll = EditorGUILayout.BeginScrollView(
                _tableScroll, GUILayout.Height(position.height - usedH));
            int cnt = _filtered?.Count ?? 0;
            for (int ri = 0; ri < cnt; ri++)
            {
                var  row  = _filtered[ri];
                bool sel  = ri == _selRow;
                Rect rect = EditorGUILayout.GetControlRect(false, ROW_H);

                if (Event.current.type == EventType.Repaint)
                {
                    Color bg = sel
                        ? new Color(0.3f, 0.5f, 0.9f, 0.35f)
                        : (ri % 2 == 0 ? Color.clear : new Color(0, 0, 0, 0.07f));
                    EditorGUI.DrawRect(rect, bg);
                }

                EditorGUILayout.BeginHorizontal();
                foreach (var col in cols)
                {
                    string val = row.TryGetValue(col.h, out var v) ? v : "";
                    EditorGUILayout.LabelField(val, GUILayout.Width(colW));
                }
                EditorGUILayout.EndHorizontal();

                if (Event.current.type == EventType.MouseDown
                    && rect.Contains(Event.current.mousePosition))
                {
                    _selRow = ri;
                    Event.current.Use();
                    Repaint();
                }
            }
            EditorGUILayout.EndScrollView();
        }

        void DrawDetail()
        {
            if (_parsed == null) return;

            EditorGUILayout.LabelField("Detail", EditorStyles.boldLabel);
            _detailScroll = EditorGUILayout.BeginScrollView(
                _detailScroll, GUI.skin.box, GUILayout.Height(DETAIL_H));

            if (_selRow >= 0 && _filtered != null && _selRow < _filtered.Count)
            {
                var cols = _parsed.Headers
                    .Select((h, i) => new { h, i })
                    .Where(x => !CsvParser.IsIgnoredColumn(x.h))
                    .ToArray();
                var selRow = _filtered[_selRow];
                foreach (var col in cols)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(col.h, GUILayout.Width(160));
                    string cur = selRow.TryGetValue(col.h, out var cv) ? cv : "";
                    string nv  = EditorGUILayout.TextField(cur);
                    if (nv != cur) { selRow[col.h] = nv; _dirty = true; }
                    EditorGUILayout.EndHorizontal();
                }
            }
            else
            {
                EditorGUILayout.LabelField(
                    "Select a row to edit.", EditorStyles.centeredGreyMiniLabel);
            }
            EditorGUILayout.EndScrollView();
        }

        void DrawManifestChecker()
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            EditorGUILayout.LabelField(
                $"Manifest 누락 키: {_manifestMissing?.Count ?? 0}개",
                EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Refresh", EditorStyles.miniButton, GUILayout.Width(60)))
                RefreshManifestCheck();
            if (GUILayout.Button("Open Manifest", EditorStyles.miniButton, GUILayout.Width(90)))
            {
                var m = AssetDatabase.LoadAssetAtPath<AssetManifest>(MANIFEST_PATH);
                if (m != null) Selection.activeObject = m;
            }
            EditorGUILayout.EndHorizontal();

            if (_manifestMissing != null && _manifestMissing.Count > 0)
            {
                _manifestScroll = EditorGUILayout.BeginScrollView(
                    _manifestScroll, GUI.skin.box, GUILayout.Height(90));
                foreach (var key in _manifestMissing)
                    EditorGUILayout.LabelField(key, EditorStyles.miniLabel);
                EditorGUILayout.EndScrollView();
            }
            else
            {
                EditorGUILayout.LabelField(
                    "모든 키 등록 완료 ✓", EditorStyles.centeredGreyMiniLabel);
            }
        }

        // ================================================================
        //  내부 액션
        // ================================================================

        void DoReload()
        {
            string path = GetCsvPath(_tableIdx);
            if (!File.Exists(path)) CreateDefaultCsv(path, _tableIdx);
            _parsed = CsvReadWriter.ReadFromFullPath(path);
            _dirty  = false;
            _selRow = -1;
            DoFilter();
        }

        void DoFilter()
        {
            if (_parsed?.Rows == null) { _filtered = null; Repaint(); return; }

            IEnumerable<Dictionary<string, string>> rows = _parsed.Rows;

            if (!string.IsNullOrEmpty(_search))
            {
                string q = _search.ToLower();
                rows = rows.Where(r => r.Values.Any(v => v != null && v.ToLower().Contains(q)));
            }

            if (_parsed.Headers != null && _sortCol < _parsed.Headers.Length)
            {
                string key = _parsed.Headers[_sortCol];
                rows = _sortAsc
                    ? rows.OrderBy(r => r.TryGetValue(key, out var v) ? v : "")
                    : rows.OrderByDescending(r => r.TryGetValue(key, out var v) ? v : "");
            }

            _filtered = rows.ToList();
            Repaint();
        }

        void DoSave()
        {
            if (_parsed == null) return;
            CsvReadWriter.Write(GetCsvPath(_tableIdx),
                _parsed.Headers, _parsed.TypeHints, _parsed.Rows, _parsed.RawLines);
            AssetDatabase.Refresh();
            _dirty = false;
            Debug.Log("[DataManager] Saved: " + TABLE_FILES[_tableIdx]);
        }

        void DoSyncSO()
        {
            if (_parsed == null) DoReload();
            if (_parsed == null) return;
            TableData td = GetOrCreateTableData();
            switch (_tableIdx)
            {
                case 0:  ScriptableObjectSync.Sync<MonsterData>           (_parsed, td.Monsters,           td, (d, row) => MonsterMapper(d, row));          break;
                case 1:  ScriptableObjectSync.Sync<IngredientData>        (_parsed, td.Ingredients,        td, IngredientMapper);                            break;
                case 2:  ScriptableObjectSync.Sync<RecipeData>            (_parsed, td.Recipes,            td, RecipeMapper);                                break;
                case 3:  ScriptableObjectSync.Sync<FoodData>              (_parsed, td.Foods,              td, FoodMapper);                                  break;
                case 4:  ScriptableObjectSync.Sync<DropTableData>         (_parsed, td.DropTables,         td, DropTableMapper);                             break;
                case 5:  ScriptableObjectSync.Sync<DungeonSpawnTableData> (_parsed, td.DungeonSpawnTables, td, DungeonSpawnTableMapper);                     break;
                case 6:  ScriptableObjectSync.Sync<SkillData>             (_parsed, td.SkillSteps,         td, SkillStepMapper);                             break;
                case 7:  ScriptableObjectSync.Sync<SkillGroupData>        (_parsed, td.SkillGroups,        td, (d, row) => SkillGroupMapper(d, row, td));    break;
                case 8:  ScriptableObjectSync.Sync<WeaponData>            (_parsed, td.Weapons,            td, (d, row) => WeaponMapper(d, row, td));        break;
                case 9:  ScriptableObjectSync.Sync<GatheringToolData>     (_parsed, td.GatheringTools,     td, GatheringToolMapper);                         break;
                case 10: ScriptableObjectSync.Sync<PlayerCharData>         (_parsed, td.Players,            td, PlayerMapper);                                break;
            }
            AssetDatabase.SaveAssets();
            if (_showManifest) RefreshManifestCheck();
        }

        void DoAddRow()
        {
            if (_parsed == null) return;
            var row = new Dictionary<string, string>();
            foreach (var h in _parsed.Headers) row[h] = "";
            _parsed.Rows.Add(row);
            _dirty = true;
            DoFilter();
            _selRow = (_filtered?.Count ?? 1) - 1;
        }

        void DoRemoveRow()
        {
            if (_selRow < 0 || _filtered == null || _selRow >= _filtered.Count) return;
            _parsed.Rows.Remove(_filtered[_selRow]);
            _dirty  = true;
            _selRow = -1;
            DoFilter();
        }

        void DoPromptSave()
        {
            if (!_dirty) return;
            if (EditorUtility.DisplayDialog("Unsaved Changes",
                    "Save changes to " + TABLE_LABELS[_tableIdx] + "?", "Save", "Discard"))
                DoSave();
            _dirty = false;
        }

        void RefreshManifestCheck()
        {
            _manifestMissing = GetMissingManifestKeys();
            Repaint();
        }

        // ================================================================
        //  유틸
        // ================================================================

        string GetCsvPath(int idx)
        {
            string root = Application.dataPath[..^"Assets".Length];
            return Path.Combine(root, CSV_ROOT, TABLE_FILES[idx]);
        }

        void CreateDefaultCsv(string path, int idx)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? "");
            File.WriteAllText(path,
                string.Join(",", DEFAULT_HEADERS[idx]) + "\n",
                System.Text.Encoding.UTF8);
        }
    }
}
#endif
