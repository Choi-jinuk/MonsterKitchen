#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        static readonly string[] TABLE_LABELS = { "Monsters", "Ingredients", "Recipes", "Foods", "Drop Tables", "Dungeon Spawn Tables" };
        static readonly string[] TABLE_FILES  = { "Monsters.csv", "Ingredients.csv", "Recipes.csv", "Foods.csv", "DropTables.csv", "DungeonSpawnTables.csv" };

        static readonly string[][] DEFAULT_HEADERS =
        {
            // Monsters
            new[] { "_key","id","displayName","description","hp","attack","defense","attribute","rarity","dropTableId","immuneToKnockback","immuneToStun","immuneToPullIn" },
            // Ingredients
            new[] { "_key","id","displayName","description","attribute","rarity","defaultState","sourceMonsterIds" },
            // Recipes
            new[] { "_key","id","displayName","description","ingredients","resultFoodId","cookTimeSeconds","unlockDay","isUnlockedByDefault" },
            // Foods
            new[] { "_key","id","displayName","description","basePrice","hpRestore","buffAttribute","buffMultiplier","buffDurationDays" },
            // Drop Tables
            new[] { "_key","id","entries" },
            // Dungeon Spawn Tables
            new[] { "_key","id","monsters" },
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
            }
            finally { EditorUtility.ClearProgressBar(); }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[DataManager] Sync All 완료 — 총 {total}개 항목");
            EditorUtility.DisplayDialog("Sync All SO", $"전체 동기화 완료\n총 {total}개 항목", "OK");
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
                0 => ScriptableObjectSync.Sync<MonsterData>           (parsed, td.Monsters,           td, MonsterMapper),
                1 => ScriptableObjectSync.Sync<IngredientData>        (parsed, td.Ingredients,        td, IngredientMapper),
                2 => ScriptableObjectSync.Sync<RecipeData>            (parsed, td.Recipes,            td, RecipeMapper),
                3 => ScriptableObjectSync.Sync<FoodData>              (parsed, td.Foods,              td, FoodMapper),
                4 => ScriptableObjectSync.Sync<DropTableData>         (parsed, td.DropTables,         td, DropTableMapper),
                5 => ScriptableObjectSync.Sync<DungeonSpawnTableData> (parsed, td.DungeonSpawnTables, td, DungeonSpawnTableMapper),
                _ => 0,
            };
        }

        // ── 테이블별 fieldMapper ─────────────────────────────────────

        /// <summary>MonsterData: prefabAddress / spriteAddress 자동 생성 (빈 경우만)</summary>
        static void MonsterMapper(MonsterData d, Dictionary<string, string> _)
        {
            if (string.IsNullOrEmpty(d.prefabAddress))
                d.prefabAddress = AssetKeys.MonsterPrefab(d.id);
            if (string.IsNullOrEmpty(d.spriteAddress))
                d.spriteAddress = AssetKeys.MonsterSprite(d.id);
        }

        /// <summary>IngredientData: spriteAddress 자동 생성</summary>
        static void IngredientMapper(IngredientData d, Dictionary<string, string> _)
        {
            if (string.IsNullOrEmpty(d.spriteAddress))
                d.spriteAddress = AssetKeys.IngredientSprite(d.id);
        }

        /// <summary>FoodData: spriteAddress 자동 생성</summary>
        static void FoodMapper(FoodData d, Dictionary<string, string> _)
        {
            if (string.IsNullOrEmpty(d.spriteAddress))
                d.spriteAddress = AssetKeys.FoodSprite(d.id);
        }

        /// <summary>RecipeData: ingredients 배열 파싱 ("2001:2|2002:1" 형식)</summary>
        static void RecipeMapper(RecipeData d, Dictionary<string, string> row)
        {
            if (!row.TryGetValue("ingredients", out string raw) || string.IsNullOrWhiteSpace(raw))
                return;

            var list = new List<RecipeIngredient>();
            foreach (var part in raw.Split('|'))
            {
                var segs = part.Trim().Split(':');
                if (segs.Length >= 2
                    && uint.TryParse(segs[0].Trim(), out uint ingId)
                    && int.TryParse(segs[1].Trim(), out int qty))
                {
                    list.Add(new RecipeIngredient { ingredientId = ingId, quantity = qty });
                }
            }
            d.ingredients = list.ToArray();
        }

        /// <summary>DungeonSpawnTableData: monsters 배열 파싱 ("1001:2:0.5|..." 형식)</summary>
        static void DungeonSpawnTableMapper(DungeonSpawnTableData d, Dictionary<string, string> row)
        {
            if (!row.TryGetValue("monsters", out string raw) || string.IsNullOrWhiteSpace(raw))
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
                    list.Add(new MonsterSpawnEntry { monsterId = monId, count = count, spawnRadius = radius });
                }
            }
            d.monsters = list;
        }

        /// <summary>DropTableData: entries 배열 파싱 ("2001:0.8:1:2|..." 형식)</summary>
        static void DropTableMapper(DropTableData d, Dictionary<string, string> row)
        {
            if (!row.TryGetValue("entries", out string raw) || string.IsNullOrWhiteSpace(raw))
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
                        ingredientId = ingId,
                        dropChance   = Mathf.Clamp01(chance),
                        minQuantity  = minQ,
                        maxQuantity  = maxQ,
                    });
                }
            }
            d.entries = list.ToArray();
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
                if (!string.IsNullOrEmpty(d.prefabAddress))  Check(d.prefabAddress,  d.displayName);
                if (!string.IsNullOrEmpty(d.spriteAddress))  Check(d.spriteAddress,  d.displayName);
            }
            foreach (var d in td.Ingredients.All)
                if (!string.IsNullOrEmpty(d.spriteAddress))  Check(d.spriteAddress,  d.displayName);
            foreach (var d in td.Foods.All)
                if (!string.IsNullOrEmpty(d.spriteAddress))  Check(d.spriteAddress,  d.displayName);

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
                case 0: ScriptableObjectSync.Sync<MonsterData>           (_parsed, td.Monsters,           td, MonsterMapper);            break;
                case 1: ScriptableObjectSync.Sync<IngredientData>        (_parsed, td.Ingredients,        td, IngredientMapper);         break;
                case 2: ScriptableObjectSync.Sync<RecipeData>            (_parsed, td.Recipes,            td, RecipeMapper);             break;
                case 3: ScriptableObjectSync.Sync<FoodData>              (_parsed, td.Foods,              td, FoodMapper);               break;
                case 4: ScriptableObjectSync.Sync<DropTableData>         (_parsed, td.DropTables,         td, DropTableMapper);          break;
                case 5: ScriptableObjectSync.Sync<DungeonSpawnTableData> (_parsed, td.DungeonSpawnTables, td, DungeonSpawnTableMapper);  break;
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
