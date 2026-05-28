#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using MonsterKitchen.AI.BehaviorTree;
using UnityEditor;
using UnityEngine;

namespace MonsterKitchen.Editor
{
    // ====================================================================
    //  BT Scene Debugger — 씬 내 BTRunner 를 자동 탐색해 라이브 상태를 시각화한다.
    //
    //  ▶ 좌측: 씬의 BTRunner 목록 (상태 dot)
    //  ▶ 우측: 선택한 에이전트의 BT 그래프 (노드 상태 컬러 바 실시간)
    //
    //  조작:
    //    드래그 (LMB/MMB) : 패닝
    //    Scroll            : 이동
    //    Auto Layout       : 로컬 레이아웃 재계산 (에셋 EditorPosition 불변)
    // ====================================================================
    public class BTSceneDebugger : EditorWindow
    {
        // ── 레이아웃 상수 ──────────────────────────────────────────────
        const float LIST_W           = 210f;
        const float NODE_W           = 200f;
        const float HEADER_H         = 28f;
        const float FIELD_H          = 17f;
        const float STATUS_BAR_H     = 5f;
        const float REFRESH_INTERVAL = 0.2f;
        const float AUTO_X_SP        = 224f;   // Auto Layout 수평 간격
        const float AUTO_Y_SP        = 130f;   // Auto Layout 수직 간격

        // ── 색상 ───────────────────────────────────────────────────────
        static readonly Color s_ColCanvas    = new Color(0.13f, 0.13f, 0.13f);
        static readonly Color s_ColGrid      = new Color(0.19f, 0.19f, 0.19f);
        static readonly Color s_ColBody      = new Color(0.22f, 0.22f, 0.22f);
        static readonly Color s_ColWire      = new Color(0.65f, 0.65f, 0.65f);
        static readonly Color s_ColRoot      = new Color(1.00f, 0.40f, 0.40f);

        static readonly Color s_ColSelector  = new Color(0.35f, 0.60f, 1.00f);
        static readonly Color s_ColSequence  = new Color(0.35f, 0.85f, 0.45f);
        static readonly Color s_ColCondition = new Color(1.00f, 0.85f, 0.25f);
        static readonly Color s_ColService   = new Color(0.80f, 0.55f, 1.00f);
        static readonly Color s_ColDecorator = new Color(0.70f, 0.70f, 0.70f);
        static readonly Color s_ColAction    = new Color(0.55f, 0.90f, 0.95f);
        static readonly Color s_ColDefault   = new Color(0.60f, 0.60f, 0.60f);
        static readonly Color s_ColParallel  = new Color(1.00f, 0.55f, 0.20f);

        static readonly Color s_ColRunning   = new Color(1.00f, 0.85f, 0.00f);
        static readonly Color s_ColSuccess   = new Color(0.20f, 0.90f, 0.30f);
        static readonly Color s_ColFailure   = new Color(0.90f, 0.20f, 0.20f);
        static readonly Color s_ColUnknown   = new Color(0.18f, 0.18f, 0.18f);

        // ── 런타임 상태 ────────────────────────────────────────────────
        List<BTRunner> m_Runners  = new();
        BTRunner       m_SelRunner;
        List<BTNode>   m_Nodes    = new();

        // ── 그래프 조작 ────────────────────────────────────────────────
        Vector2 m_ListScroll;
        Vector2 m_GraphOffset = new Vector2(40f, 40f);
        bool    m_Panning;
        Vector2 m_PanStart;

        // ── 자동 갱신 ──────────────────────────────────────────────────
        double m_NextRefresh;
        bool   m_AutoRefresh = true;

        // ── 로컬 레이아웃 (에셋 EditorPosition 불변, 뷰 전용) ─────────
        readonly Dictionary<BTNode, Vector2> m_LocalLayout = new();
        bool m_HasLocalLayout;

        // ================================================================
        //  열기
        // ================================================================

        [MenuItem("MonsterKitchen/BT Scene Debugger")]
        public static void Open()
        {
            var w = GetWindow<BTSceneDebugger>("BT Debugger");
            w.minSize = new Vector2(640, 400);
        }

        // ================================================================
        //  Mono — 이벤트
        // ================================================================

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
            if (!m_AutoRefresh) return;
            if (EditorApplication.timeSinceStartup < m_NextRefresh) return;
            m_NextRefresh = EditorApplication.timeSinceStartup + REFRESH_INTERVAL;
            Scan();
            Repaint();
        }

        void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change is PlayModeStateChange.EnteredPlayMode or PlayModeStateChange.EnteredEditMode)
            {
                m_SelRunner = null;
                m_Nodes.Clear();
                ClearLocalLayout();
                Scan();
                Repaint();
            }
        }

        // ================================================================
        //  GUI
        // ================================================================

        void OnGUI()
        {
            DrawToolbar();
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawList();
                var div = EditorGUILayout.GetControlRect(false, GUILayout.Width(1), GUILayout.ExpandHeight(true));
                EditorGUI.DrawRect(div, new Color(0.08f, 0.08f, 0.08f));
                DrawGraph();
            }
        }

        // ── 툴바 ─────────────────────────────────────────────────────
        void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                // 플레이 상태 표시
                string playLabel = Application.isPlaying ? "● Play" : "■ Edit";
                var playStyle = new GUIStyle(EditorStyles.toolbarButton);
                playStyle.normal.textColor = Application.isPlaying ? Color.green : Color.gray;
                GUILayout.Label(playLabel, playStyle, GUILayout.Width(54));
                GUILayout.Space(4);

                if (GUILayout.Button("Scan", EditorStyles.toolbarButton, GUILayout.Width(50)))
                { Scan(); Repaint(); }

                bool newAuto = GUILayout.Toggle(m_AutoRefresh, "Auto", EditorStyles.toolbarButton, GUILayout.Width(50));
                if (newAuto != m_AutoRefresh) m_AutoRefresh = newAuto;

                GUILayout.Space(4);

                // Auto Layout — 선택된 런너가 있을 때만 활성화
                bool canLayout = m_SelRunner != null && m_SelRunner.EditorAsset?.Root != null;
                GUI.enabled = canLayout;
                if (GUILayout.Button("Auto Layout", EditorStyles.toolbarButton, GUILayout.Width(84)))
                    ApplyAutoLayout();
                GUI.enabled = true;

                if (m_HasLocalLayout)
                {
                    if (GUILayout.Button("Reset Layout", EditorStyles.toolbarButton, GUILayout.Width(84)))
                    { ClearLocalLayout(); Repaint(); }
                }

                GUILayout.Space(6);
                var cntStyle = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(0.55f, 0.55f, 0.55f) } };
                GUILayout.Label($"{m_Runners.Count} runner(s)", cntStyle);
                GUILayout.FlexibleSpace();

                DrawLegendItem(s_ColRunning, "Running");
                GUILayout.Space(3);
                DrawLegendItem(s_ColSuccess, "Success");
                GUILayout.Space(3);
                DrawLegendItem(s_ColFailure, "Failure");
                GUILayout.Space(6);
            }
        }

        static void DrawLegendItem(Color col, string label)
        {
            var r = GUILayoutUtility.GetRect(10, 10, GUILayout.Width(10));
            EditorGUI.DrawRect(new Rect(r.x, r.y + 2, 10, 10), col);
            GUILayout.Label(label, EditorStyles.miniLabel);
        }

        // ── 좌측 목록 ────────────────────────────────────────────────
        void DrawList()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(LIST_W)))
            {
                EditorGUILayout.LabelField("Scene Agents", EditorStyles.boldLabel);
                m_ListScroll = EditorGUILayout.BeginScrollView(m_ListScroll, GUILayout.ExpandHeight(true));

                if (m_Runners.Count == 0)
                {
                    string msg = Application.isPlaying
                        ? "씬에 BTRunner 없음."
                        : "Play Mode 에서 확인하세요.";
                    EditorGUILayout.HelpBox(msg, MessageType.None);
                }

                foreach (var runner in m_Runners)
                {
                    if (runner == null) continue;
                    bool  isSelected = runner == m_SelRunner;
                    Color dotCol     = RunnerDotColor(runner);

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        var dotRect = GUILayoutUtility.GetRect(10, 10, GUILayout.Width(10), GUILayout.Height(18));
                        EditorGUI.DrawRect(new Rect(dotRect.x, dotRect.y + 4, 10, 10), dotCol);

                        var style = isSelected
                            ? new GUIStyle(GUI.skin.button)
                              { fontStyle = FontStyle.Bold, normal = { textColor = new Color(1f, 0.85f, 0.3f) } }
                            : EditorStyles.miniButton;

                        if (GUILayout.Button(runner.name, style, GUILayout.ExpandWidth(true)))
                            SelectRunner(runner);
                    }
                }
                EditorGUILayout.EndScrollView();

                // 선택 정보 패널
                if (m_SelRunner != null)
                {
                    EditorGUILayout.Space(4);
                    DrawSeparator();
                    var asset  = m_SelRunner.EditorAsset;
                    string status = m_SelRunner.EditorAborted ? "Aborted"
                                  : m_SelRunner.EditorRoot == null ? "No Tree"
                                  : "Running";
                    EditorGUILayout.LabelField("Asset:",  asset  != null ? asset.name : "—", EditorStyles.miniLabel);
                    EditorGUILayout.LabelField("Status:", status,                             EditorStyles.miniLabel);

                    if (Application.isPlaying)
                    {
                        var ctx = m_SelRunner.EditorContext;
                        if (ctx != null)
                        {
                            int runningCount = m_Nodes.Count(n => n != null && ctx.TryGetLastStatus(n, out var s) && s == BTStatus.Running);
                            EditorGUILayout.LabelField("Running:", $"{runningCount} node(s)", EditorStyles.miniLabel);
                        }
                    }

                    EditorGUILayout.Space(2);
                    if (GUILayout.Button("Select in Hierarchy", EditorStyles.miniButton))
                        Selection.activeGameObject = m_SelRunner.gameObject;
                }
            }
        }

        Color RunnerDotColor(BTRunner r)
        {
            if (!Application.isPlaying) return new Color(0.4f, 0.4f, 0.4f);
            if (r.EditorAborted)        return s_ColFailure;
            if (r.EditorRoot == null)   return new Color(0.9f, 0.7f, 0.1f);
            return s_ColSuccess;
        }

        // ── 그래프 캔버스 ─────────────────────────────────────────────
        void DrawGraph()
        {
            Rect cr = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none,
                GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            EditorGUI.DrawRect(cr, s_ColCanvas);
            DrawGrid(cr);
            GUI.BeginClip(cr);

            if (m_SelRunner == null)
                DrawHint(cr, "좌측 목록에서 에이전트를 선택하세요.");
            else if (m_Nodes.Count == 0)
                DrawHint(cr, "BTAsset 이 없거나 노드가 없습니다.");
            else
            {
                DrawWires();
                DrawNodes();
            }

            GUI.EndClip();
            HandleGraphInput(cr);
        }

        void DrawGrid(Rect r)
        {
            const float sp = 30f;
            Handles.color = s_ColGrid;
            float ox = m_GraphOffset.x % sp, oy = m_GraphOffset.y % sp;
            int cols = Mathf.CeilToInt(r.width  / sp) + 1;
            int rows = Mathf.CeilToInt(r.height / sp) + 1;
            for (int i = 0; i <= cols; i++)
                Handles.DrawLine(new Vector3(ox + i * sp, 0), new Vector3(ox + i * sp, r.height));
            for (int i = 0; i <= rows; i++)
                Handles.DrawLine(new Vector3(0, oy + i * sp), new Vector3(r.width, oy + i * sp));
        }

        // ── 노드 그리기 ───────────────────────────────────────────────
        void DrawNodes()
        {
            var asset = m_SelRunner?.EditorAsset;
            var ctx   = m_SelRunner?.EditorContext;

            foreach (var node in m_Nodes)
            {
                if (node == null) continue;
                Rect  nr     = NodeRect(node);
                bool  isRoot = asset != null && asset.Root == node;
                Color bg     = NodeHeaderColor(node);

                // 루트 강조 테두리
                if (isRoot) EditorGUI.DrawRect(Expand(nr, 3), s_ColRoot);
                EditorGUI.DrawRect(nr, s_ColBody);

                // 헤더
                var hr = new Rect(nr.x, nr.y, nr.width, HEADER_H);
                EditorGUI.DrawRect(hr, bg);

                string badge = GetBadge(node);
                string label = node.DebugLabel;   // DebugLabel 사용
                string title = (isRoot ? "[R] " : "") + (badge.Length > 0 ? $"[{badge}] " : "") + label;
                var hs = new GUIStyle(EditorStyles.boldLabel)
                    { alignment = TextAnchor.MiddleCenter, fontSize = 11, normal = { textColor = Color.black } };
                GUI.Label(hr, title, hs);

                // 직렬화 필드 표시
                var so   = new SerializedObject(node);
                var prop = so.GetIterator();
                float py = nr.y + HEADER_H + 2f;
                var ps = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(0.75f, 0.75f, 0.75f) } };
                prop.NextVisible(true);
                while (prop.NextVisible(false))
                {
                    if (prop.name is "EditorPosition" or "m_Child" or "m_Children") continue;
                    GUI.Label(new Rect(nr.x + 5, py, nr.width - 10, FIELD_H),
                        $"{prop.displayName}: {PropVal(prop)}", ps);
                    py += FIELD_H;
                }

                // 런타임 상태 바
                Color statusCol  = s_ColUnknown;
                string statusIcon = "";
                if (Application.isPlaying && ctx != null && ctx.TryGetLastStatus(node, out var st))
                {
                    statusCol  = st == BTStatus.Running ? s_ColRunning
                               : st == BTStatus.Success ? s_ColSuccess : s_ColFailure;
                    statusIcon = st == BTStatus.Running ? "▶" : st == BTStatus.Success ? "✓" : "✕";
                }
                EditorGUI.DrawRect(new Rect(nr.x, nr.yMax - STATUS_BAR_H, nr.width, STATUS_BAR_H), statusCol);

                if (!string.IsNullOrEmpty(statusIcon))
                {
                    var iconStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 10, normal = { textColor = statusCol } };
                    GUI.Label(new Rect(nr.xMax - 18, hr.y, 16, HEADER_H), statusIcon, iconStyle);
                }
            }
        }

        // ── 와이어 그리기 ─────────────────────────────────────────────
        void DrawWires()
        {
            // 트리 구조를 따라 루트에서 재귀 순회 — 삽입 순서와 무관하게 정확히 그림
            var asset = m_SelRunner?.EditorAsset;
            if (asset?.Root == null) return;
            DrawWiresRecursive(asset.Root);
        }

        void DrawWiresRecursive(BTNode node)
        {
            if (node == null) return;
            Rect    pr   = NodeRect(node);
            Vector2 from = new Vector2(pr.center.x, pr.yMax - STATUS_BAR_H);

            if (node is BTComposite composite)
            {
                foreach (var child in composite.Children)
                {
                    if (child == null) continue;
                    Rect cr = NodeRect(child);
                    DrawBezier(from, new Vector2(cr.center.x, cr.yMin), s_ColWire);
                    DrawWiresRecursive(child);
                }
            }
            else if (node is BTDecorator decorator && decorator.Child != null)
            {
                Rect cr = NodeRect(decorator.Child);
                DrawBezier(from, new Vector2(cr.center.x, cr.yMin), s_ColWire);
                DrawWiresRecursive(decorator.Child);
            }
        }

        static void DrawBezier(Vector2 a, Vector2 b, Color col)
        {
            float dy = Mathf.Abs(b.y - a.y) * 0.5f + 20f;
            Handles.color = col;
            Handles.DrawBezier(a, b, new Vector3(a.x, a.y + dy), new Vector3(b.x, b.y - dy), col, null, 2f);
        }

        // ── 입력 처리 ─────────────────────────────────────────────────
        void HandleGraphInput(Rect cr)
        {
            var e = Event.current;
            if (!cr.Contains(e.mousePosition)) return;
            Vector2 local = e.mousePosition - cr.position;

            switch (e.type)
            {
                case EventType.MouseDown when e.button is 0 or 2:
                    m_Panning = true; m_PanStart = local; e.Use(); break;
                case EventType.MouseDrag when (e.button is 0 or 2) && m_Panning:
                    m_GraphOffset += local - m_PanStart; m_PanStart = local; e.Use(); Repaint(); break;
                case EventType.MouseUp:
                    m_Panning = false; break;
                case EventType.ScrollWheel:
                    m_GraphOffset -= e.delta * 2f; e.Use(); Repaint(); break;
            }
        }

        // ── 레이아웃 유틸 ─────────────────────────────────────────────
        Rect NodeRect(BTNode node)
        {
            // 직렬화 필드 수 계산 (m_Children 제외 — 자식 수가 많으면 노드가 너무 커짐)
            int fc = 0;
            var so   = new SerializedObject(node);
            var prop = so.GetIterator();
            prop.NextVisible(true);
            while (prop.NextVisible(false))
                if (prop.name is not "EditorPosition" and not "m_Child" and not "m_Children") fc++;

            float h  = HEADER_H + fc * FIELD_H + STATUS_BAR_H + 6f;
            Vector2 p = m_HasLocalLayout && m_LocalLayout.TryGetValue(node, out var lp) ? lp : node.EditorPosition;
            return new Rect(m_GraphOffset.x + p.x, m_GraphOffset.y + p.y, NODE_W, h);
        }

        // ── Auto Layout (로컬, 에셋 불변) ────────────────────────────
        void ApplyAutoLayout()
        {
            m_LocalLayout.Clear();
            m_HasLocalLayout = false;

            var root = m_SelRunner?.EditorAsset?.Root;
            if (root == null) return;

            int xCounter = 0;
            ComputeLayout(root, 0, ref xCounter);
            m_HasLocalLayout = true;
            m_GraphOffset    = new Vector2(40f, 40f);
            Repaint();
        }

        void ComputeLayout(BTNode node, int depth, ref int xCounter)
        {
            var children = GetNodeChildren(node);

            if (children.Count == 0)
            {
                m_LocalLayout[node] = new Vector2(xCounter * AUTO_X_SP, depth * AUTO_Y_SP);
                xCounter++;
                return;
            }

            // 먼저 자식들을 재귀 배치
            foreach (var child in children)
                ComputeLayout(child, depth + 1, ref xCounter);

            // 부모를 자식들의 중심 위에 배치
            float minX = m_LocalLayout[children[0]].x;
            float maxX = m_LocalLayout[children[^1]].x;
            m_LocalLayout[node] = new Vector2((minX + maxX) * 0.5f, depth * AUTO_Y_SP);
        }

        static List<BTNode> GetNodeChildren(BTNode node)
        {
            if (node is BTComposite comp)
                return comp.Children.Where(c => c != null).ToList();
            if (node is BTDecorator dec && dec.Child != null)
                return new List<BTNode> { dec.Child };
            return new List<BTNode>();
        }

        void ClearLocalLayout() { m_LocalLayout.Clear(); m_HasLocalLayout = false; }

        // ── 노드 색상 / 배지 ──────────────────────────────────────────
        static Color NodeHeaderColor(BTNode node)
        {
            if (node is BTSelector)  return s_ColSelector;
            if (node is BTSequence)  return s_ColSequence;
            if (node is BTParallel)  return s_ColParallel;
            if (node is BTCondition) return s_ColCondition;
            if (node is BTService)   return s_ColService;
            if (node is BTDecorator) return s_ColDecorator;
            string n = node.GetType().Name;
            if (n.Contains("Action")) return s_ColAction;
            return s_ColDefault;
        }

        static string GetBadge(BTNode node)
        {
            if (node is BTCondition) return "C";
            if (node is BTService)   return "S";
            if (node is BTDecorator) return "D";
            return "";
        }

        static Rect Expand(Rect r, float d) => new Rect(r.x - d, r.y - d, r.width + d * 2, r.height + d * 2);

        static string PropVal(SerializedProperty p) => p.propertyType switch
        {
            SerializedPropertyType.Float           => p.floatValue.ToString("F2"),
            SerializedPropertyType.Integer         => p.intValue.ToString(),
            SerializedPropertyType.Boolean         => p.boolValue.ToString(),
            SerializedPropertyType.String          => $"\"{p.stringValue}\"",
            SerializedPropertyType.ObjectReference => p.objectReferenceValue != null ? p.objectReferenceValue.name : "None",
            _                                      => "…"
        };

        static void DrawHint(Rect r, string msg)
        {
            var s = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter, fontSize = 13,
                normal    = { textColor = new Color(0.35f, 0.35f, 0.35f) }
            };
            GUI.Label(new Rect(0, 0, r.width, r.height), msg, s);
        }

        static void DrawSeparator()
        {
            var r = GUILayoutUtility.GetRect(1, 1, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(r, new Color(0.28f, 0.28f, 0.28f));
            GUILayout.Space(3);
        }

        // ── 씬 탐색 ──────────────────────────────────────────────────
        void Scan()
        {
            BTRunner prev = m_SelRunner;
            m_Runners.Clear();
            m_Runners.AddRange(
                FindObjectsByType<BTRunner>(FindObjectsInactive.Include).OrderBy(r => r.name));

            // 이전 선택 유지 or 초기화
            bool prevAlive = prev != null && m_Runners.Contains(prev);
            m_SelRunner = prevAlive ? prev : null;

            if (m_SelRunner != null)
                RefreshNodes();
            else
            {
                m_Nodes.Clear();
                ClearLocalLayout();
            }
        }

        void SelectRunner(BTRunner runner)
        {
            m_SelRunner = runner;
            Selection.activeGameObject = runner.gameObject;
            ClearLocalLayout();
            RefreshNodes();
            m_GraphOffset = new Vector2(40f, 40f);
            Repaint();
        }

        void RefreshNodes()
        {
            m_Nodes.Clear();
            if (m_SelRunner?.EditorAsset == null) return;
            m_Nodes = m_SelRunner.EditorAsset.GetAllNodes();
        }
    }
}
#endif
