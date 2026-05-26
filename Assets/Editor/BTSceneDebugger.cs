#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using MonsterKitchen.AI.BehaviorTree;
using UnityEditor;
using UnityEngine;

namespace MonsterKitchen.Editor
{
    /// <summary>
    /// BT Scene Debugger — 씬 내 BTRunner 를 자동 탐색해 라이브 상태를 시각화한다.
    ///
    /// ▶ 좌측: 씬의 BTRunner 목록 (상태 dot 표시)
    /// ▶ 우측: 선택한 에이전트의 BT 그래프 (노드 상태 컬러 바 실시간 표시)
    ///
    /// 조작:
    ///   - 드래그: 그래프 패닝
    ///   - Scroll: 그래프 이동
    ///   - 에이전트 클릭: 해당 GameObject 를 Hierarchy 에서 선택
    /// </summary>
    public class BTSceneDebugger : EditorWindow
    {
        // ── 레이아웃 ──────────────────────────────────────────────────────
        const float LIST_W       = 200f;
        const float NODE_W       = 200f;
        const float HEADER_H     = 28f;
        const float FIELD_H      = 17f;
        const float STATUS_BAR_H = 5f;
        const float PORT_R       = 5f;
        const float REFRESH_INTERVAL = 0.2f;   // 5fps 갱신

        // ── 색상 ──────────────────────────────────────────────────────────
        static readonly Color ColCanvas   = new Color(0.13f, 0.13f, 0.13f);
        static readonly Color ColGrid     = new Color(0.19f, 0.19f, 0.19f);
        static readonly Color ColBody     = new Color(0.22f, 0.22f, 0.22f);
        static readonly Color ColWire     = new Color(0.65f, 0.65f, 0.65f);
        static readonly Color ColSelected = new Color(1.00f, 0.70f, 0.10f);
        static readonly Color ColRoot     = new Color(1.00f, 0.40f, 0.40f);

        static readonly Color ColSelector  = new Color(0.35f, 0.60f, 1.00f);
        static readonly Color ColSequence  = new Color(0.35f, 0.85f, 0.45f);
        static readonly Color ColCondition = new Color(1.00f, 0.85f, 0.25f);
        static readonly Color ColAction    = new Color(0.55f, 0.90f, 0.95f);
        static readonly Color ColDefault   = new Color(0.60f, 0.60f, 0.60f);

        static readonly Color ColRunning = new Color(1.00f, 0.85f, 0.00f);
        static readonly Color ColSuccess = new Color(0.20f, 0.90f, 0.30f);
        static readonly Color ColFailure = new Color(0.90f, 0.20f, 0.20f);
        static readonly Color ColUnknown = new Color(0.25f, 0.25f, 0.25f);

        // ── 런타임 상태 ───────────────────────────────────────────────────
        List<BTRunner> _runners    = new();
        BTRunner       _selRunner;
        List<BTNode>   _nodes      = new();

        Vector2 _listScroll;
        Vector2 _graphOffset = new Vector2(40, 40);
        bool    _panning;
        Vector2 _panStart;

        double _nextRefresh;
        bool   _autoRefresh = true;

        // ── 메뉴 ──────────────────────────────────────────────────────────
        [MenuItem("MonsterKitchen/BT Scene Debugger")]
        public static void Open()
        {
            var w = GetWindow<BTSceneDebugger>("BT Debugger");
            w.minSize = new Vector2(640, 400);
        }

        // ── Unity 콜백 ────────────────────────────────────────────────────
        void OnEnable()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            Scan();
        }

        void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        }

        void OnInspectorUpdate()
        {
            if (!_autoRefresh) return;
            if (EditorApplication.timeSinceStartup < _nextRefresh) return;
            _nextRefresh = EditorApplication.timeSinceStartup + REFRESH_INTERVAL;
            Scan();
            Repaint();
        }

        void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredPlayMode ||
                change == PlayModeStateChange.EnteredEditMode)
            {
                _selRunner = null;
                _nodes.Clear();
                Scan();
                Repaint();
            }
        }

        // ── GUI ───────────────────────────────────────────────────────────
        void OnGUI()
        {
            DrawToolbar();

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawList();

                // 구분선
                var div = EditorGUILayout.GetControlRect(false,
                    GUILayout.Width(1), GUILayout.ExpandHeight(true));
                EditorGUI.DrawRect(div, new Color(0.08f, 0.08f, 0.08f));

                DrawGraph();
            }
        }

        // ── 툴바 ─────────────────────────────────────────────────────────
        void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                string playLabel = Application.isPlaying ? "● Play" : "■ Edit";
                var    playStyle = new GUIStyle(EditorStyles.toolbarButton);
                playStyle.normal.textColor = Application.isPlaying ? Color.green : Color.gray;
                GUILayout.Label(playLabel, playStyle, GUILayout.Width(54));

                GUILayout.Space(6);

                if (GUILayout.Button("Scan Scene", EditorStyles.toolbarButton, GUILayout.Width(80)))
                {
                    Scan();
                    Repaint();
                }

                bool newAuto = GUILayout.Toggle(_autoRefresh, "Auto Refresh",
                    EditorStyles.toolbarButton, GUILayout.Width(88));
                if (newAuto != _autoRefresh) _autoRefresh = newAuto;

                GUILayout.Space(8);

                // 에이전트 수 표시
                var countStyle = new GUIStyle(EditorStyles.miniLabel)
                    { normal = { textColor = new Color(0.6f, 0.6f, 0.6f) } };
                GUILayout.Label($"{_runners.Count} runner(s) found", countStyle);

                GUILayout.FlexibleSpace();

                // 범례
                DrawLegendItem(ColRunning, "Running");
                GUILayout.Space(4);
                DrawLegendItem(ColSuccess, "Success");
                GUILayout.Space(4);
                DrawLegendItem(ColFailure, "Failure");
                GUILayout.Space(6);
            }
        }

        static void DrawLegendItem(Color col, string label)
        {
            var r = GUILayoutUtility.GetRect(10, 10, GUILayout.Width(10));
            EditorGUI.DrawRect(new Rect(r.x, r.y + 1, 10, 10), col);
            GUILayout.Label(label, EditorStyles.miniLabel);
        }

        // ── 에이전트 목록 ────────────────────────────────────────────────
        void DrawList()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(LIST_W)))
            {
                EditorGUILayout.LabelField("Agents in Scene", EditorStyles.boldLabel);
                _listScroll = EditorGUILayout.BeginScrollView(_listScroll,
                    GUILayout.ExpandHeight(true));

                if (_runners.Count == 0)
                {
                    EditorGUILayout.HelpBox(
                        "씬에 BTRunner 가 없습니다.\n" +
                        (Application.isPlaying ? "" : "Play Mode 에서 확인하세요."),
                        MessageType.None);
                }

                foreach (var runner in _runners)
                {
                    if (runner == null) continue;

                    bool isSelected = runner == _selRunner;
                    Color dotCol    = RunnerDotColor(runner);

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        // 상태 dot
                        var dotRect = GUILayoutUtility.GetRect(10, 10,
                            GUILayout.Width(10), GUILayout.Height(18));
                        EditorGUI.DrawRect(
                            new Rect(dotRect.x, dotRect.y + 4, 10, 10), dotCol);

                        // 이름 버튼
                        GUIStyle style = isSelected
                            ? new GUIStyle(GUI.skin.button)
                                { fontStyle = FontStyle.Bold,
                                  normal    = { textColor = new Color(1f, 0.85f, 0.3f) } }
                            : EditorStyles.miniButton;

                        if (GUILayout.Button(runner.name, style, GUILayout.ExpandWidth(true)))
                            SelectRunner(runner);
                    }
                }

                EditorGUILayout.EndScrollView();

                // 선택된 에이전트 정보
                if (_selRunner != null)
                {
                    EditorGUILayout.Space(4);
                    DrawSeparator();

                    string assetName = _selRunner.EditorAsset != null
                        ? _selRunner.EditorAsset.name : "—";
                    EditorGUILayout.LabelField("Asset:", assetName,
                        EditorStyles.miniLabel);

                    string status = _selRunner.EditorAborted  ? "Aborted"
                                  : _selRunner.EditorRoot == null ? "No Tree"
                                  : "Running";
                    EditorGUILayout.LabelField("Status:", status,
                        EditorStyles.miniLabel);

                    EditorGUILayout.Space(2);
                    if (GUILayout.Button("Hierarchy で Select", EditorStyles.miniButton))
                        Selection.activeGameObject = _selRunner.gameObject;
                }
            }
        }

        Color RunnerDotColor(BTRunner r)
        {
            if (!Application.isPlaying) return new Color(0.4f, 0.4f, 0.4f);
            if (r.EditorAborted)        return ColFailure;
            if (r.EditorRoot == null)   return new Color(0.9f, 0.7f, 0.1f);
            return ColSuccess;
        }

        // ── 그래프 ───────────────────────────────────────────────────────
        void DrawGraph()
        {
            Rect cr = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none,
                GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

            EditorGUI.DrawRect(cr, ColCanvas);
            DrawGrid(cr);

            GUI.BeginClip(cr);

            if (_selRunner != null && _nodes.Count > 0)
            {
                DrawWires();
                DrawNodes();
            }
            else if (_selRunner == null)
            {
                DrawHint(cr, "좌측 목록에서 에이전트를 선택하세요.");
            }
            else
            {
                DrawHint(cr, "BTAsset 이 할당되지 않았거나 노드가 없습니다.");
            }

            GUI.EndClip();

            HandleGraphInput(cr);
        }

        void DrawGrid(Rect r)
        {
            const float sp = 30f;
            Handles.color = ColGrid;
            float ox = _graphOffset.x % sp, oy = _graphOffset.y % sp;
            int   cols = Mathf.CeilToInt(r.width  / sp) + 1;
            int   rows = Mathf.CeilToInt(r.height / sp) + 1;
            for (int i = 0; i <= cols; i++)
                Handles.DrawLine(new Vector3(ox + i * sp, 0), new Vector3(ox + i * sp, r.height));
            for (int i = 0; i <= rows; i++)
                Handles.DrawLine(new Vector3(0, oy + i * sp), new Vector3(r.width, oy + i * sp));
        }

        void DrawNodes()
        {
            var asset = _selRunner?.EditorAsset;
            var ctx   = _selRunner?.EditorContext;

            foreach (var node in _nodes)
            {
                if (node == null) continue;

                Rect  nr     = NodeRect(node);
                bool  isRoot = asset != null && asset.root == node;
                Color bg     = NodeHeaderColor(node);

                // 루트 테두리
                if (isRoot) EditorGUI.DrawRect(Expand(nr, 3), ColRoot);

                EditorGUI.DrawRect(nr, ColBody);

                // 헤더
                var hr = new Rect(nr.x, nr.y, nr.width, HEADER_H);
                EditorGUI.DrawRect(hr, bg);

                string title = (isRoot ? "[R] " : "") + node.name;
                var hs = new GUIStyle(EditorStyles.boldLabel)
                    { alignment = TextAnchor.MiddleCenter, fontSize = 11 };
                hs.normal.textColor = Color.black;
                GUI.Label(hr, title, hs);

                // 직렬화 필드 (읽기 전용)
                var so   = new SerializedObject(node);
                var prop = so.GetIterator();
                float py = nr.y + HEADER_H + 2f;
                var   ps = new GUIStyle(EditorStyles.miniLabel)
                    { normal = { textColor = new Color(0.75f, 0.75f, 0.75f) } };
                prop.NextVisible(true);
                while (prop.NextVisible(false))
                {
                    if (prop.name == "editorPosition") continue;
                    GUI.Label(new Rect(nr.x + 5, py, nr.width - 10, FIELD_H),
                        $"{prop.displayName}: {PropVal(prop)}", ps);
                    py += FIELD_H;
                }

                // ── 런타임 상태 바 ────────────────────────────────────────
                Color statusCol = ColUnknown;
                string statusIcon = "";
                if (Application.isPlaying && ctx != null &&
                    ctx.TryGetLastStatus(node, out var st))
                {
                    statusCol  = st == BTStatus.Running ? ColRunning :
                                 st == BTStatus.Success  ? ColSuccess : ColFailure;
                    statusIcon = st == BTStatus.Running ? "▶" :
                                 st == BTStatus.Success  ? "✓" : "✕";
                }

                var barRect = new Rect(nr.x, nr.yMax - STATUS_BAR_H, nr.width, STATUS_BAR_H);
                EditorGUI.DrawRect(barRect, statusCol);

                // 상태 아이콘 (헤더 우측)
                if (!string.IsNullOrEmpty(statusIcon))
                {
                    var iconStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 10 };
                    iconStyle.normal.textColor = statusCol;
                    GUI.Label(new Rect(nr.xMax - 18, hr.y, 16, HEADER_H), statusIcon, iconStyle);
                }
            }
        }

        void DrawWires()
        {
            foreach (var node in _nodes)
            {
                if (node is not BTComposite composite) continue;
                Rect    pr   = NodeRect(node);
                Vector2 from = new Vector2(pr.center.x, pr.yMax - STATUS_BAR_H);
                foreach (var child in composite.Children)
                {
                    if (child == null) continue;
                    Rect cr = NodeRect(child);
                    DrawBezier(from, new Vector2(cr.center.x, cr.yMin), ColWire);
                }
            }
        }

        static void DrawBezier(Vector2 a, Vector2 b, Color col)
        {
            float dy = Mathf.Abs(b.y - a.y) * 0.5f + 20f;
            Handles.DrawBezier(a, b,
                new Vector3(a.x, a.y + dy),
                new Vector3(b.x, b.y - dy),
                col, null, 2f);
        }

        // ── 입력 처리 ────────────────────────────────────────────────────
        void HandleGraphInput(Rect cr)
        {
            var e = Event.current;
            if (!cr.Contains(e.mousePosition)) return;

            Vector2 local = e.mousePosition - cr.position;

            switch (e.type)
            {
                case EventType.MouseDown when e.button == 0 || e.button == 2:
                    _panning  = true;
                    _panStart = local;
                    e.Use();
                    break;

                case EventType.MouseDrag when (e.button == 0 || e.button == 2) && _panning:
                    _graphOffset += local - _panStart;
                    _panStart     = local;
                    e.Use();
                    Repaint();
                    break;

                case EventType.MouseUp:
                    _panning = false;
                    break;

                case EventType.ScrollWheel:
                    _graphOffset -= e.delta * 2f;
                    e.Use();
                    Repaint();
                    break;
            }
        }

        // ── 유틸리티 ─────────────────────────────────────────────────────
        Rect NodeRect(BTNode node)
        {
            var  so   = new SerializedObject(node);
            var  prop = so.GetIterator();
            int  fc   = 0;
            prop.NextVisible(true);
            while (prop.NextVisible(false))
                if (prop.name != "editorPosition") fc++;

            float h = HEADER_H + fc * FIELD_H + STATUS_BAR_H + 6f;
            return new Rect(
                _graphOffset.x + node.editorPosition.x,
                _graphOffset.y + node.editorPosition.y,
                NODE_W, h);
        }

        static Color NodeHeaderColor(BTNode node)
        {
            string n = node.GetType().Name;
            if (n.Contains("Selector"))  return ColSelector;
            if (n.Contains("Sequence"))  return ColSequence;
            if (n.Contains("Condition")) return ColCondition;
            if (n.Contains("Action"))    return ColAction;
            return ColDefault;
        }

        static Rect Expand(Rect r, float d) =>
            new Rect(r.x - d, r.y - d, r.width + d * 2, r.height + d * 2);

        static string PropVal(SerializedProperty p)
        {
            return p.propertyType switch
            {
                SerializedPropertyType.Float         => p.floatValue.ToString("F2"),
                SerializedPropertyType.Integer       => p.intValue.ToString(),
                SerializedPropertyType.Boolean       => p.boolValue.ToString(),
                SerializedPropertyType.String        => p.stringValue,
                SerializedPropertyType.ObjectReference =>
                    p.objectReferenceValue != null ? p.objectReferenceValue.name : "None",
                _ => "…"
            };
        }

        static void DrawHint(Rect r, string msg)
        {
            var s = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize  = 13,
                normal    = { textColor = new Color(0.38f, 0.38f, 0.38f) }
            };
            GUI.Label(new Rect(0, 0, r.width, r.height), msg, s);
        }

        static void DrawSeparator()
        {
            var r = GUILayoutUtility.GetRect(1, 1, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(r, new Color(0.28f, 0.28f, 0.28f));
            GUILayout.Space(3);
        }

        // ── 씬 스캔 / 선택 ───────────────────────────────────────────────

        /// <summary>씬에서 BTRunner 를 모두 탐색해 _runners 를 갱신한다.</summary>
        void Scan()
        {
            // 기존 선택 유지
            BTRunner prev = _selRunner;

            _runners.Clear();
            _runners.AddRange(
                FindObjectsByType<BTRunner>(FindObjectsInactive.Include)
                    .OrderBy(r => r.name));

            // null 정리 후 이전 선택 복원
            _selRunner = _runners.Contains(prev) ? prev : null;
            if (_selRunner != null)
                RefreshNodes();
            else
                _nodes.Clear();
        }

        void SelectRunner(BTRunner runner)
        {
            _selRunner = runner;
            Selection.activeGameObject = runner.gameObject;
            RefreshNodes();
            // 그래프를 루트 노드 부근으로 리셋
            _graphOffset = new Vector2(40, 40);
            Repaint();
        }

        void RefreshNodes()
        {
            _nodes.Clear();
            if (_selRunner?.EditorAsset == null) return;
            _nodes = _selRunner.EditorAsset.GetAllNodes();
        }
    }
}
#endif
