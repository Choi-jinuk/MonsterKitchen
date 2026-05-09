#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MonsterKitchen.Data;
using MonsterKitchen.Data.Pipeline;
using UnityEditor;
using UnityEngine;

namespace MonsterKitchen.Editor
{
    public class DataManagerWindow : EditorWindow
    {
        const string CSV_ROOT = "Assets/Data/CSV";
        const string SO_ROOT  = "Assets/Data/SO";
        const int    ROW_H    = 20;
        const int    DETAIL_H = 200;

        static readonly string[] TABLE_LABELS  = { "Monsters", "Ingredients", "Recipes", "Foods", "Drop Tables" };
        static readonly string[] TABLE_FILES   = { "Monsters.csv", "Ingredients.csv", "Recipes.csv", "Foods.csv", "DropTables.csv" };
        static readonly string[] TABLE_FOLDERS = { "Monsters", "Ingredients", "Recipes", "Foods", "DropTables" };

        static readonly string[][] DEFAULT_HEADERS =
        {
            new[] { "id","displayName","description","hp","attack","defense","attribute","rarity" },
            new[] { "id","displayName","description","attribute","rarity","defaultState","sourceMonsterIds" },
            new[] { "id","displayName","description","ingredients","resultFoodId","cookTimeSeconds","unlockDay","isUnlockedByDefault" },
            new[] { "id","displayName","description","basePrice","hpRestore","buffAttribute","buffMultiplier","buffDurationDays" },
            new[] { "id","entries" },
        };

        int     _tableIdx = 0;
        int     _selRow   = -1;
        string  _search   = "";
        int     _sortCol  = 0;
        bool    _sortAsc  = true;
        bool    _dirty    = false;

        Vector2 _tableScroll;
        Vector2 _detailScroll;

        CsvParser.ParseResult _parsed;
        List<Dictionary<string, string>> _filtered;

        // ---- Menu ----
        [MenuItem("MonsterKitchen/Data Manager")]
        public static void Open()
        {
            var w = GetWindow<DataManagerWindow>("Data Manager");
            w.minSize = new Vector2(800, 500);
        }

        /// <summary>
        /// 메뉴에서 직접 호출 가능. 창을 열지 않아도 전체 SO를 일괄 동기화한다.
        /// </summary>
        [MenuItem("MonsterKitchen/Sync All SO")]
        public static void SyncAllSO()
        {
            int grandTotal = 0;
            try
            {
                for (int i = 0; i < TABLE_FILES.Length; i++)
                {
                    EditorUtility.DisplayProgressBar(
                        "Syncing All SO",
                        $"[{i + 1}/{TABLE_FILES.Length}] {TABLE_LABELS[i]}...",
                        (float)i / TABLE_FILES.Length);

                    grandTotal += SyncTableAtIndex(i);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[DataManager] Sync All 완료 — 총 {grandTotal}개 SO 동기화.");
            EditorUtility.DisplayDialog("Sync All SO", $"전체 동기화 완료\n총 {grandTotal}개 SO", "OK");
        }

        /// <summary>idx번 테이블 CSV를 읽어 SO를 생성/갱신한다. 동기화된 SO 수를 반환.</summary>
        static int SyncTableAtIndex(int idx)
        {
            string dataPath = Application.dataPath;
            string root     = dataPath.Substring(0, dataPath.Length - "Assets".Length);
            string csvPath  = Path.Combine(root, CSV_ROOT, TABLE_FILES[idx]);

            if (!File.Exists(csvPath))
            {
                Debug.LogWarning($"[DataManager] CSV 없음: {TABLE_FILES[idx]} — 건너뜀.");
                return 0;
            }

            var parsed = CsvReadWriter.ReadFromFullPath(csvPath);
            if (parsed?.Rows == null || parsed.Rows.Count == 0) return 0;

            string folder = SO_ROOT + "/" + TABLE_FOLDERS[idx];
            int count = 0;
            switch (idx)
            {
                case 0: count = ScriptableObjectSync.Sync<MonsterData>   (parsed, folder)?.Count ?? 0; break;
                case 1: count = ScriptableObjectSync.Sync<IngredientData>(parsed, folder)?.Count ?? 0; break;
                case 2: count = ScriptableObjectSync.Sync<RecipeData>    (parsed, folder)?.Count ?? 0; break;
                case 3: count = ScriptableObjectSync.Sync<FoodData>      (parsed, folder)?.Count ?? 0; break;
                case 4: count = ScriptableObjectSync.Sync<DropTableData> (parsed, folder)?.Count ?? 0; break;
            }
            return count;
        }

        void OnEnable() { DoReload(); }
        void OnDestroy() { DoPromptSave(); }

        // ============================================================
        //  GUI
        // ============================================================

        void OnGUI()
        {
            DrawToolbar();
            DrawTable();
            DrawDetail();
        }

        void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            int newIdx = EditorGUILayout.Popup(_tableIdx, TABLE_LABELS, EditorStyles.toolbarPopup, GUILayout.Width(120));
            if (newIdx != _tableIdx)
            {
                DoPromptSave();
                _tableIdx = newIdx;
                _selRow   = -1;
                DoReload();
            }

            GUILayout.Space(8);
            EditorGUILayout.LabelField("Search:", GUILayout.Width(46));
            string ns = EditorGUILayout.TextField(_search, EditorStyles.toolbarSearchField, GUILayout.Width(160));
            if (ns != _search) { _search = ns; DoFilter(); }

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("+ Row",   EditorStyles.toolbarButton, GUILayout.Width(56))) DoAddRow();

            GUI.enabled = _selRow >= 0;
            if (GUILayout.Button("- Row",   EditorStyles.toolbarButton, GUILayout.Width(56))) DoRemoveRow();
            GUI.enabled = true;

            GUI.enabled = _dirty;
            if (GUILayout.Button("Save",    EditorStyles.toolbarButton, GUILayout.Width(50)))  DoSave();
            GUI.enabled = true;

            if (GUILayout.Button("Sync SO",  EditorStyles.toolbarButton, GUILayout.Width(62)))  DoSyncSO();
            if (GUILayout.Button("Sync All", EditorStyles.toolbarButton, GUILayout.Width(62)))  SyncAllSO();
            if (GUILayout.Button("Reload",   EditorStyles.toolbarButton, GUILayout.Width(56)))  DoReload();

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
            float tableH = position.height - DETAIL_H - 80;
            _tableScroll = EditorGUILayout.BeginScrollView(_tableScroll, GUILayout.Height(tableH));

            int cnt = _filtered == null ? 0 : _filtered.Count;
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

                if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
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
            _detailScroll = EditorGUILayout.BeginScrollView(_detailScroll, GUI.skin.box, GUILayout.Height(DETAIL_H));

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
                    EditorGUILayout.LabelField(col.h, GUILayout.Width(150));
                    string cur = selRow.TryGetValue(col.h, out var cv) ? cv : "";
                    string nv  = EditorGUILayout.TextField(cur);
                    if (nv != cur) { selRow[col.h] = nv; _dirty = true; }
                    EditorGUILayout.EndHorizontal();
                }
            }
            else
            {
                EditorGUILayout.LabelField("Select a row to edit.", EditorStyles.centeredGreyMiniLabel);
            }

            EditorGUILayout.EndScrollView();
        }

        // ============================================================
        //  Data Logic (Do* prefix to avoid validator false-positives)
        // ============================================================

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
                if (_sortAsc)
                    rows = rows.OrderBy(r => r.TryGetValue(key, out var kv) ? kv : "");
                else
                    rows = rows.OrderByDescending(r => r.TryGetValue(key, out var kv) ? kv : "");
            }

            _filtered = rows.ToList();
            Repaint();
        }

        void DoSave()
        {
            if (_parsed == null) return;
            CsvReadWriter.Write(GetCsvPath(_tableIdx), _parsed.Headers, _parsed.TypeHints, _parsed.Rows, _parsed.RawLines);
            AssetDatabase.Refresh();
            _dirty = false;
            Debug.Log("[DataManager] Saved: " + TABLE_FILES[_tableIdx]);
        }

        void DoSyncSO()
        {
            if (_parsed == null) DoReload();
            if (_parsed == null) return;
            string folder = SO_ROOT + "/" + TABLE_FOLDERS[_tableIdx];
            switch (_tableIdx)
            {
                case 0: ScriptableObjectSync.Sync<MonsterData>   (_parsed, folder); break;
                case 1: ScriptableObjectSync.Sync<IngredientData>(_parsed, folder); break;
                case 2: ScriptableObjectSync.Sync<RecipeData>    (_parsed, folder); break;
                case 3: ScriptableObjectSync.Sync<FoodData>      (_parsed, folder); break;
                case 4: ScriptableObjectSync.Sync<DropTableData> (_parsed, folder); break;
            }
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

        // ============================================================
        //  Utils
        // ============================================================

        string GetCsvPath(int idx)
        {
            string dataPath = Application.dataPath;
            string root     = dataPath.Substring(0, dataPath.Length - "Assets".Length);
            return Path.Combine(root, CSV_ROOT, TABLE_FILES[idx]);
        }

        void CreateDefaultCsv(string path, int idx)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, string.Join(",", DEFAULT_HEADERS[idx]) + "\n", System.Text.Encoding.UTF8);
        }
    }
}
#endif
