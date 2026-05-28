#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using MonsterKitchen.AI.BehaviorTree;
using UnityEditor;
using UnityEngine;

namespace MonsterKitchen.Editor
{
    /// <summary>
    /// BehaviorTree 그래프 에디터.
    ///
    /// ▶ 에디트 모드
    ///   - 우클릭 → Add Node
    ///   - 노드 드래그 → 위치 이동
    ///   - Alt + 드래그 → 연결선 연결 (Composite: 자식 추가 / Decorator: 단일 자식 설정)
    ///   - 우클릭 노드 → Set as Root / Delete
    ///   - 우측 Inspector 에서 파라미터 편집
    ///   - 우측 하단 Blackboard 패널에서 기본값 설정
    ///
    /// ▶ 플레이 모드
    ///   - 좌측 BTRunners 패널에서 실행 중인 에이전트 선택
    ///   - 노드 하단 컬러 바로 실행 상태 표시
    ///   - 우측 Blackboard 패널에서 라이브 값 확인
    /// </summary>
    public class BTGraphEditor : EditorWindow
    {
        // ── 레이아웃 ──────────────────────────────────────────────────────
        const float RUNTIME_PANEL_W = 175f;
        const float INSPECTOR_W    = 260f;
        const float NODE_W         = 200f;
        const float HEADER_H       = 28f;
        const float FIELD_H        = 18f;
        const float PORT_R         = 8f;
        const float STATUS_BAR_H   = 5f;

        // ── 색상 ──────────────────────────────────────────────────────────
        static readonly Color s_ColCanvas    = new Color(0.14f, 0.14f, 0.14f);
        static readonly Color s_ColGrid      = new Color(0.20f, 0.20f, 0.20f);
        static readonly Color s_ColSelector  = new Color(0.35f, 0.60f, 1.00f);
        static readonly Color s_ColSequence  = new Color(0.35f, 0.85f, 0.45f);
        static readonly Color s_ColCondition = new Color(1.00f, 0.85f, 0.25f);
        static readonly Color s_ColService   = new Color(0.80f, 0.55f, 1.00f);
        static readonly Color s_ColDecorator = new Color(0.70f, 0.70f, 0.70f);
        static readonly Color s_ColAction    = new Color(0.55f, 0.90f, 0.95f);
        static readonly Color s_ColDefault   = new Color(0.60f, 0.60f, 0.60f);
        static readonly Color s_ColSelected  = new Color(1.00f, 0.70f, 0.10f);
        static readonly Color s_ColRoot      = new Color(1.00f, 0.40f, 0.40f);
        static readonly Color s_ColWire      = Color.white;
        static readonly Color s_ColBody      = new Color(0.22f, 0.22f, 0.22f);

        static readonly Color s_ColStatusRunning = new Color(1.00f, 0.85f, 0.00f);
        static readonly Color s_ColStatusSuccess = new Color(0.20f, 0.90f, 0.30f);
        static readonly Color s_ColStatusFailure = new Color(0.90f, 0.20f, 0.20f);

        // ── 에셋 / 노드 상태 ──────────────────────────────────────────────
        BTAsset      m_Asset;
        List<BTNode> m_Nodes = new();
        BTNode       m_Selected;

        Vector2 m_Offset  = new Vector2(80, 80);
        bool    m_DraggingCanvas;
        Vector2 m_DragCanvasStart;

        bool    m_DraggingNode;
        BTNode  m_DragNode;
        Vector2 m_DragNodeStart;
        Vector2 m_DragMouseStart;

        bool    m_Connecting;
        BTNode  m_ConnectFrom;
        Vector2 m_ConnectMouse;

        // ── 런타임 ────────────────────────────────────────────────────────
        List<BTRunner> m_Runners    = new();
        BTRunner       m_SelRunner;
        Vector2        m_RuntimeScroll;
        double         m_NextRefresh;

        // ── 인스펙터 / 블랙보드 스크롤 ───────────────────────────────────
        Vector2 m_InspectorScroll;
        Vector2 m_BbScroll;
        bool    m_BbFoldout = true;

        [MenuItem("MonsterKitchen/BehaviorTree Editor")]
        public static void Open()
        {
            var w = GetWindow<BTGraphEditor>("BT Editor");
            w.minSize = new Vector2(860, 520);
        }

        void OnEnable()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            RefreshRunners();
        }

        void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        }

        void OnInspectorUpdate()
        {
            if (!Application.isPlaying) return;
            if (EditorApplication.timeSinceStartup < m_NextRefresh) return;
            m_NextRefresh = EditorApplication.timeSinceStartup + 0.25;
            RefreshRunners();
            Repaint();
        }

        void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode ||
                state == PlayModeStateChange.EnteredEditMode)
            {
                m_SelRunner = null;
                RefreshRunners();
                Repaint();
            }
        }

        // ── GUI ───────────────────────────────────────────────────────────

        void OnGUI()
        {
            DrawToolbar();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (Application.isPlaying) DrawRuntimePanel();
                DrawGraphCanvas();
                DrawRightPanel();
            }
            if (m_Connecting) Repaint();
            HandleHotkeys();
        }

        // ── 툴바 ─────────────────────────────────────────────────────────
        void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                var newAsset = (BTAsset)EditorGUILayout.ObjectField(
                    m_Asset, typeof(BTAsset), false, GUILayout.Width(220));
                if (newAsset != m_Asset) LoadAsset(newAsset);

                GUILayout.Space(6);
                if (GUILayout.Button("New Asset", EditorStyles.toolbarButton, GUILayout.Width(76)))
                    CreateNewAsset();

                if (m_Asset != null)
                {
                    if (GUILayout.Button("Save", EditorStyles.toolbarButton, GUILayout.Width(46)))
                        SaveAsset();
                    if (GUILayout.Button("Auto Layout", EditorStyles.toolbarButton, GUILayout.Width(80)))
                        AutoLayout();
                }

                GUILayout.FlexibleSpace();
                string mode = Application.isPlaying ? "● Play" : "■ Edit";
                var ms = new GUIStyle(EditorStyles.toolbarButton);
                ms.normal.textColor = Application.isPlaying ? Color.green : Color.gray;
                GUILayout.Label(mode, ms);
            }
        }

        // ── 런타임 패널 ───────────────────────────────────────────────────
        void DrawRuntimePanel()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(RUNTIME_PANEL_W)))
            {
                EditorGUILayout.LabelField("BTRunners", EditorStyles.boldLabel);
                m_RuntimeScroll = EditorGUILayout.BeginScrollView(m_RuntimeScroll, GUILayout.ExpandHeight(true));

                foreach (var r in m_Runners)
                {
                    if (r == null) continue;
                    bool sel   = r == m_SelRunner;
                    var  style = sel ? GUI.skin.button : EditorStyles.miniButton;
                    if (GUILayout.Button(r.name, style))
                    {
                        m_SelRunner = r;
                        Selection.activeGameObject = r.gameObject;
                        if (r.EditorAsset != null) LoadAsset(r.EditorAsset);
                    }
                }
                EditorGUILayout.EndScrollView();

                if (m_SelRunner != null)
                {
                    EditorGUILayout.Space(2);
                    var statusStyle = new GUIStyle(EditorStyles.miniLabel);
                    if (m_SelRunner.EditorAborted)
                    {
                        statusStyle.normal.textColor = new Color(1f, 0.4f, 0.4f);
                        EditorGUILayout.LabelField("⛔ Aborted", statusStyle);
                    }
                    else if (m_SelRunner.EditorRoot == null)
                    {
                        statusStyle.normal.textColor = new Color(1f, 0.8f, 0f);
                        EditorGUILayout.LabelField("⚠ No Tree", statusStyle);
                    }
                    else
                    {
                        statusStyle.normal.textColor = new Color(0.3f, 1f, 0.3f);
                        EditorGUILayout.LabelField("▶ Running", statusStyle);
                    }
                }

                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Node Status", EditorStyles.boldLabel);
                DrawLegend(s_ColStatusRunning, "Running");
                DrawLegend(s_ColStatusSuccess, "Success");
                DrawLegend(s_ColStatusFailure, "Failure");
            }

            var div = EditorGUILayout.GetControlRect(false, GUILayout.Width(1), GUILayout.ExpandHeight(true));
            EditorGUI.DrawRect(div, new Color(0.1f, 0.1f, 0.1f));
        }

        static void DrawLegend(Color col, string label)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                var r = GUILayoutUtility.GetRect(12, 12, GUILayout.Width(12));
                EditorGUI.DrawRect(new Rect(r.x, r.y + 2, 12, 10), col);
                GUILayout.Label(label, EditorStyles.miniLabel);
            }
        }

        // ── 그래프 캔버스 ────────────────────────────────────────────────
        void DrawGraphCanvas()
        {
            Rect cr = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none,
                GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

            EditorGUI.DrawRect(cr, s_ColCanvas);
            DrawGrid(cr);
            GUI.BeginClip(cr);

            if (m_Asset != null)
            {
                DrawWires();
                DrawNodes();
                if (m_Connecting && m_ConnectFrom != null)
                {
                    Rect    fr   = NodeRect(m_ConnectFrom);
                    Vector2 from = PortRect(fr).center;
                    DrawBezier(from, m_ConnectMouse - cr.position, s_ColSelected);
                }
            }
            else
            {
                DrawHint(cr, "툴바에서 BTAsset 을 선택하거나 New Asset 으로 새로 만드세요.");
            }

            GUI.EndClip();
            HandleGraphInput(cr);
        }

        void DrawGrid(Rect r)
        {
            float sp = 32f;
            Handles.color = s_ColGrid;
            float ox = m_Offset.x % sp, oy = m_Offset.y % sp;
            int   cols = Mathf.CeilToInt(r.width  / sp) + 1;
            int   rows = Mathf.CeilToInt(r.height / sp) + 1;
            for (int i = 0; i <= cols; i++)
                Handles.DrawLine(new Vector3(ox + i * sp, 0), new Vector3(ox + i * sp, r.height));
            for (int i = 0; i <= rows; i++)
                Handles.DrawLine(new Vector3(0, oy + i * sp), new Vector3(r.width, oy + i * sp));
        }

        // ── 노드 / 와이어 ─────────────────────────────────────────────────
        void DrawNodes()
        {
            foreach (var node in m_Nodes)
                if (node != null) DrawNode(node);
        }

        void DrawNode(BTNode node)
        {
            Rect  nr    = NodeRect(node);
            Color bg    = NodeColor(node);
            bool  isRoot = m_Asset != null && m_Asset.Root == node;
            bool  isSel  = node == m_Selected;

            if (isRoot)     EditorGUI.DrawRect(Expand(nr, 3), s_ColRoot);
            else if (isSel) EditorGUI.DrawRect(Expand(nr, 2), s_ColSelected);

            EditorGUI.DrawRect(nr, s_ColBody);

            var hr = new Rect(nr.x, nr.y, nr.width, HEADER_H);
            EditorGUI.DrawRect(hr, bg);

            // 노드 타입 배지 (Decorator 계열)
            string badge = GetNodeBadge(node);
            string title = (isRoot ? "[R] " : "") + (badge.Length > 0 ? $"[{badge}] " : "") + node.name;
            var hs = new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter, fontSize = 11 };
            hs.normal.textColor = Color.black;
            GUI.Label(hr, title, hs);

            // 필드 표시
            var so   = new SerializedObject(node);
            var prop = so.GetIterator();
            float py = nr.y + HEADER_H + 3f;
            var ps = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(0.8f, 0.8f, 0.8f) } };
            prop.NextVisible(true);
            while (prop.NextVisible(false))
            {
                if (prop.name is "EditorPosition" or "m_Child") continue;
                GUI.Label(new Rect(nr.x + 6, py, nr.width - 12, FIELD_H), $"{prop.displayName}: {PropValueString(prop)}", ps);
                py += FIELD_H;
            }

            // 런타임 상태 바
            if (Application.isPlaying && m_SelRunner?.EditorContext != null)
            {
                Color statusCol = Color.clear;
                if (m_SelRunner.EditorContext.TryGetLastStatus(node, out var st))
                {
                    statusCol = st switch
                    {
                        BTStatus.Running => s_ColStatusRunning,
                        BTStatus.Success => s_ColStatusSuccess,
                        BTStatus.Failure => s_ColStatusFailure,
                        _                => Color.clear
                    };
                }
                var statusBar = new Rect(nr.x, nr.yMax - STATUS_BAR_H, nr.width, STATUS_BAR_H);
                EditorGUI.DrawRect(statusBar, statusCol != Color.clear ? statusCol : new Color(0.15f, 0.15f, 0.15f));

                if (statusCol != Color.clear)
                {
                    string stText = st == BTStatus.Running ? "▶" : st == BTStatus.Success ? "✓" : "✕";
                    var stStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 10 };
                    stStyle.normal.textColor = statusCol;
                    GUI.Label(new Rect(nr.xMax - 18, hr.y, 16, HEADER_H), stText, stStyle);
                }
            }

            // 하단 포트 — Composite·Decorator 만 표시 (리프 노드는 자식을 가질 수 없으므로 포트 없음)
            bool canHaveChildren = node is BTComposite || node is BTDecorator;
            if (canHaveChildren)
            {
                Rect  port      = PortRect(nr);
                bool  portHover = port.Contains(Event.current.mousePosition);
                Color portCol   = (m_Connecting && m_ConnectFrom == node)
                    ? s_ColSelected
                    : portHover ? Color.white : new Color(0.85f, 0.85f, 0.85f);
                EditorGUI.DrawRect(Expand(port, 1), new Color(0.1f, 0.1f, 0.1f)); // 테두리
                EditorGUI.DrawRect(port, portCol);
            }
        }

        void DrawWires()
        {
            foreach (var node in m_Nodes)
            {
                if (node == null) continue;
                Rect    pr   = NodeRect(node);
                Vector2 from = PortRect(pr).center;   // 포트 중심에서 출발

                if (node is BTComposite composite)
                {
                    foreach (var child in composite.Children)
                    {
                        if (child == null) continue;
                        Rect cr = NodeRect(child);
                        DrawBezier(from, new Vector2(cr.center.x, cr.yMin), s_ColWire);
                    }
                }
                else if (node is BTDecorator decorator && decorator.Child != null)
                {
                    Rect cr = NodeRect(decorator.Child);
                    DrawBezier(from, new Vector2(cr.center.x, cr.yMin), s_ColWire);
                }
            }
        }

        static void DrawBezier(Vector2 a, Vector2 b, Color col)
        {
            float dy = Mathf.Abs(b.y - a.y) * 0.5f + 20f;
            // Handles.color 를 명시적으로 설정 — GUI.BeginClip 내부에서도 색이 유지됨
            Handles.color = col;
            Handles.DrawBezier(a, b, new Vector3(a.x, a.y + dy), new Vector3(b.x, b.y - dy), col, null, 2.5f);
        }

        // ── 우측 패널 ────────────────────────────────────────────────────
        void DrawRightPanel()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(INSPECTOR_W)))
            {
                m_InspectorScroll = EditorGUILayout.BeginScrollView(m_InspectorScroll, GUILayout.ExpandHeight(true));
                DrawInspector();
                DrawBlackboardPanel();
                EditorGUILayout.EndScrollView();
            }
        }

        void DrawInspector()
        {
            EditorGUILayout.LabelField("Inspector", EditorStyles.boldLabel);

            if (m_Asset == null) { EditorGUILayout.HelpBox("BTAsset 을 선택하세요.", MessageType.None); return; }

            if (m_Selected == null)
            {
                EditorGUILayout.HelpBox("노드를 클릭해 선택하세요.", MessageType.None);
            }
            else
            {
                bool isRoot    = m_Asset.Root == m_Selected;
                bool newIsRoot = EditorGUILayout.Toggle("Root Node", isRoot);
                if (newIsRoot != isRoot)
                {
                    Undo.RecordObject(m_Asset, "Set Root");
                    m_Asset.Root = newIsRoot ? m_Selected : null;
                    EditorUtility.SetDirty(m_Asset);
                }

                EditorGUILayout.Space(4);

                var so   = new SerializedObject(m_Selected);
                so.Update();
                var prop = so.GetIterator();
                prop.NextVisible(true);
                EditorGUI.BeginChangeCheck();
                while (prop.NextVisible(false))
                {
                    if (prop.name is "EditorPosition" or "m_Child") continue;
                    EditorGUILayout.PropertyField(prop, true);
                }
                if (EditorGUI.EndChangeCheck()) so.ApplyModifiedProperties();

                EditorGUILayout.Space(6);

                // BTComposite: 다자식 목록
                if (m_Selected is BTComposite composite)
                {
                    EditorGUILayout.LabelField("Children", EditorStyles.boldLabel);
                    for (int i = 0; i < composite.Children.Count; i++)
                    {
                        var child = composite.Children[i];
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            EditorGUILayout.LabelField($"[{i}] {child?.name ?? "null"}", GUILayout.ExpandWidth(true));
                            if (GUILayout.Button("↑", GUILayout.Width(22)) && i > 0)
                            { Undo.RecordObject(m_Selected, "Reorder"); composite.ReorderChild(i, i - 1); }
                            if (GUILayout.Button("↓", GUILayout.Width(22)) && i < composite.Children.Count - 1)
                            { Undo.RecordObject(m_Selected, "Reorder"); composite.ReorderChild(i, i + 1); }
                            if (GUILayout.Button("×", GUILayout.Width(22)))
                            { Undo.RecordObject(m_Selected, "Remove Child"); composite.RemoveChild(child); break; }
                        }
                    }
                }
                // BTDecorator: 단일 자식 표시
                else if (m_Selected is BTDecorator decorator)
                {
                    EditorGUILayout.LabelField("Child", EditorStyles.boldLabel);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        var childStyle = new GUIStyle(EditorStyles.miniLabel)
                            { normal = { textColor = decorator.Child != null ? new Color(0.7f, 1f, 0.7f) : new Color(1f, 0.5f, 0.5f) } };
                        EditorGUILayout.LabelField(decorator.Child?.name ?? "— 없음 —", childStyle, GUILayout.ExpandWidth(true));
                        if (decorator.Child != null && GUILayout.Button("×", GUILayout.Width(22)))
                        { Undo.RecordObject(m_Selected, "Clear Child"); decorator.ClearChild(); }
                    }
                }

                EditorGUILayout.Space(8);
                GUI.color = new Color(1f, 0.5f, 0.5f);
                if (GUILayout.Button("Delete Node")) { DeleteNode(m_Selected); m_Selected = null; }
                GUI.color = Color.white;
            }

            EditorGUILayout.Space(8);
            DrawSeparator();
        }

        // ── 블랙보드 패널 ─────────────────────────────────────────────────
        void DrawBlackboardPanel()
        {
            m_BbFoldout = EditorGUILayout.Foldout(m_BbFoldout, "Blackboard", true, EditorStyles.foldoutHeader);
            if (!m_BbFoldout) return;
            EditorGUI.indentLevel++;
            if (Application.isPlaying) DrawBlackboardLive();
            else                       DrawBlackboardEdit();
            EditorGUI.indentLevel--;
        }

        void DrawBlackboardLive()
        {
            if (m_SelRunner == null) { EditorGUILayout.HelpBox("좌측에서 BTRunner 를 선택하세요.", MessageType.None); return; }
            var bb = m_SelRunner.EditorContext?.Blackboard;
            if (bb == null) { EditorGUILayout.HelpBox("블랙보드가 초기화되지 않았습니다.", MessageType.None); return; }

            var headerStyle = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(0.6f, 0.9f, 1f) } };
            EditorGUILayout.LabelField($"Agent: {m_SelRunner.name}", headerStyle);

            EditorGUI.BeginDisabledGroup(true);
            foreach (var kv in bb.EditorEntries)
            {
                string valStr = kv.Value switch
                {
                    float f    => f.ToString("F3"),
                    int i      => i.ToString(),
                    bool b     => b.ToString(),
                    Vector2 v2 => v2.ToString("F2"),
                    Vector3 v3 => v3.ToString("F2"),
                    UnityEngine.Object o => o != null ? o.name : "null",
                    _          => kv.Value?.ToString() ?? "null"
                };
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(kv.Key, GUILayout.Width(INSPECTOR_W * 0.45f));
                    EditorGUILayout.LabelField(valStr, EditorStyles.miniLabel);
                }
            }
            EditorGUI.EndDisabledGroup();
        }

        void DrawBlackboardEdit()
        {
            if (m_Asset == null) { EditorGUILayout.HelpBox("BTAsset 을 선택하세요.", MessageType.None); return; }

            EditorGUILayout.HelpBox(
                "여기서 설정한 값은 BTRunner.Start() 시 블랙보드에 먼저 적용됩니다.\n" +
                "IBTBlackboardInitializer 에서 같은 키를 덮어쓸 수 있습니다.",
                MessageType.Info);

            var so = new SerializedObject(m_Asset);
            so.Update();
            var listProp = so.FindProperty("BlackboardDefaults");
            EditorGUI.BeginChangeCheck();

            for (int i = 0; i < listProp.arraySize; i++)
            {
                var entry    = listProp.GetArrayElementAtIndex(i);
                var keyProp  = entry.FindPropertyRelative("Key");
                var typeProp = entry.FindPropertyRelative("ValueType");

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.PropertyField(keyProp, GUIContent.none, GUILayout.ExpandWidth(true));
                        EditorGUILayout.PropertyField(typeProp, GUIContent.none, GUILayout.Width(70));
                        if (GUILayout.Button("−", GUILayout.Width(20))) { listProp.DeleteArrayElementAtIndex(i); break; }
                    }
                    var bbType = (BBValueType)typeProp.enumValueIndex;
                    string valRelName = bbType switch
                    {
                        BBValueType.Float   => "FloatValue",
                        BBValueType.Int     => "IntValue",
                        BBValueType.Bool    => "BoolValue",
                        BBValueType.String  => "StringValue",
                        BBValueType.Vector2 => "Vector2Value",
                        BBValueType.Vector3 => "Vector3Value",
                        _                   => "FloatValue"
                    };
                    EditorGUILayout.PropertyField(entry.FindPropertyRelative(valRelName), new GUIContent("Value"));
                }
            }

            if (GUILayout.Button("＋ Add Entry")) listProp.arraySize++;
            if (EditorGUI.EndChangeCheck()) { so.ApplyModifiedProperties(); EditorUtility.SetDirty(m_Asset); }
        }

        static void DrawSeparator()
        {
            var r = GUILayoutUtility.GetRect(1, 1, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(r, new Color(0.3f, 0.3f, 0.3f));
            GUILayout.Space(4);
        }

        // ── 입력 처리 ────────────────────────────────────────────────────
        void HandleGraphInput(Rect cr)
        {
            if (m_Asset == null) return;
            var e = Event.current;
            if (!cr.Contains(e.mousePosition)) return;
            Vector2 local = e.mousePosition - cr.position;

            switch (e.type)
            {
                case EventType.MouseDown when e.button == 0:
                {
                    // 포트 클릭 우선 체크 → 연결선 시작
                    BTNode portHit = HitTestPort(local);
                    if (portHit != null)
                    {
                        m_Connecting = true; m_ConnectFrom = portHit; m_ConnectMouse = e.mousePosition;
                        e.Use(); Repaint(); break;
                    }

                    BTNode hit = HitTest(local);
                    if (hit != null)
                    {
                        m_Selected = hit; m_DraggingNode = true; m_DragNode = hit;
                        m_DragNodeStart = hit.EditorPosition; m_DragMouseStart = local;
                        e.Use(); Repaint();
                    }
                    else
                    {
                        m_Selected = null; m_DraggingCanvas = true; m_DragCanvasStart = local; e.Use(); Repaint();
                    }
                    break;
                }

                case EventType.MouseDrag when e.button == 0:
                    if (m_DraggingNode && m_DragNode != null)
                    {
                        Undo.RecordObject(m_DragNode, "Move Node");
                        m_DragNode.EditorPosition = m_DragNodeStart + (local - m_DragMouseStart);
                        EditorUtility.SetDirty(m_DragNode); e.Use(); Repaint();
                    }
                    else if (m_DraggingCanvas)
                    {
                        m_Offset += local - m_DragCanvasStart; m_DragCanvasStart = local; e.Use(); Repaint();
                    }
                    else if (m_Connecting) { m_ConnectMouse = e.mousePosition; e.Use(); }
                    break;

                case EventType.MouseUp when e.button == 0:
                    if (m_DraggingNode)   { m_DraggingNode = false; m_DragNode = null; }
                    if (m_DraggingCanvas) m_DraggingCanvas = false;
                    if (m_Connecting)
                    {
                        m_Connecting = false;
                        BTNode target = HitTest(local);
                        if (target != null && target != m_ConnectFrom)
                        {
                            if (m_ConnectFrom is BTComposite comp)
                            { Undo.RecordObject(m_ConnectFrom, "Add Connection"); comp.AddChild(target); }
                            else if (m_ConnectFrom is BTDecorator dec)
                            { Undo.RecordObject(m_ConnectFrom, "Set Child"); dec.SetChild(target); }
                        }
                        m_ConnectFrom = null; Repaint();
                    }
                    break;

                case EventType.MouseDown when e.button == 1:
                    ShowContextMenu(local, HitTest(local)); e.Use(); break;

                case EventType.ScrollWheel:
                    m_Offset -= e.delta * 2f; e.Use(); Repaint(); break;
            }
        }

        void HandleHotkeys()
        {
            var e = Event.current;
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Delete && m_Selected != null && m_Asset != null)
            { DeleteNode(m_Selected); m_Selected = null; e.Use(); Repaint(); }
        }

        // ── 컨텍스트 메뉴 ────────────────────────────────────────────────
        void ShowContextMenu(Vector2 local, BTNode hit)
        {
            var menu = new GenericMenu();
            foreach (var kv in BTNodeRegistry.AllByPath.OrderBy(x => x.Key))
            {
                var type = kv.Value; var menuPath = "Add Node/" + kv.Key; var pos = local - m_Offset;
                menu.AddItem(new GUIContent(menuPath), false, () => AddNode(type, pos));
            }

            if (hit != null)
            {
                menu.AddSeparator("");
                menu.AddItem(new GUIContent("Set as Root"), false, () =>
                { Undo.RecordObject(m_Asset, "Set Root"); m_Asset.Root = hit; EditorUtility.SetDirty(m_Asset); Repaint(); });

                if (m_Selected != null && m_Selected != hit)
                {
                    if (m_Selected is BTComposite selComp)
                        menu.AddItem(new GUIContent("Connect Selected → This"), false, () =>
                        { Undo.RecordObject(m_Selected, "Add Connection"); selComp.AddChild(hit); Repaint(); });
                    else if (m_Selected is BTDecorator selDec)
                        menu.AddItem(new GUIContent("Set Selected's Child → This"), false, () =>
                        { Undo.RecordObject(m_Selected, "Set Child"); selDec.SetChild(hit); Repaint(); });
                }

                menu.AddSeparator("");
                var captured = hit;
                menu.AddItem(new GUIContent("Delete"), false, () =>
                { DeleteNode(captured); if (m_Selected == captured) m_Selected = null; Repaint(); });
            }
            menu.ShowAsContext();
        }

        // ── 유틸리티 ─────────────────────────────────────────────────────
        Rect NodeRect(BTNode node)
        {
            int fieldCount = 0;
            var so   = new SerializedObject(node);
            var prop = so.GetIterator();
            prop.NextVisible(true);
            while (prop.NextVisible(false))
                if (prop.name is not "EditorPosition" and not "m_Child") fieldCount++;

            // Composite·Decorator 는 포트 전용 영역(PORT_R*2 + 위아래 여백)을 추가로 확보
            bool  hasPort = node is BTComposite || node is BTDecorator;
            float h = HEADER_H + fieldCount * FIELD_H + STATUS_BAR_H
                    + (hasPort ? PORT_R * 2 + 13f : 8f);
            return new Rect(m_Offset.x + node.EditorPosition.x, m_Offset.y + node.EditorPosition.y, NODE_W, h);
        }

        BTNode HitTest(Vector2 local)
        {
            if (m_Nodes == null) return null;
            for (int i = m_Nodes.Count - 1; i >= 0; i--)
                if (m_Nodes[i] != null && NodeRect(m_Nodes[i]).Contains(local)) return m_Nodes[i];
            return null;
        }

        // 노드 하단 포트 Rect — 포트 전용 영역 안에 4px 여백을 두고 배치
        static Rect PortRect(Rect nr) =>
            new Rect(nr.center.x - PORT_R, nr.yMax - STATUS_BAR_H - 4f - PORT_R * 2, PORT_R * 2, PORT_R * 2);

        // BTNode 오버로드 (DrawNode 내부용)
        Rect PortRect(BTNode node) => PortRect(NodeRect(node));

        // 포트 영역 히트 테스트 — Composite·Decorator 만 연결 가능, 리프 노드 차단
        BTNode HitTestPort(Vector2 local)
        {
            if (m_Nodes == null) return null;
            for (int i = m_Nodes.Count - 1; i >= 0; i--)
            {
                var node = m_Nodes[i];
                if (node == null) continue;
                if (!(node is BTComposite) && !(node is BTDecorator)) continue;
                if (PortRect(node).Contains(local)) return node;
            }
            return null;
        }

        static Color NodeColor(BTNode node)
        {
            if (node is BTSelector)  return s_ColSelector;
            if (node is BTSequence)  return s_ColSequence;
            if (node is BTCondition) return s_ColCondition;
            if (node is BTService)   return s_ColService;
            if (node is BTDecorator) return s_ColDecorator;
            string n = node.GetType().Name;
            if (n.Contains("Action")) return s_ColAction;
            return s_ColDefault;
        }

        static string GetNodeBadge(BTNode node)
        {
            if (node is BTCondition) return "C";
            if (node is BTService)   return "S";
            if (node is BTDecorator) return "D";
            return "";
        }

        static Rect Expand(Rect r, float d) => new Rect(r.x - d, r.y - d, r.width + d * 2, r.height + d * 2);

        static string PropValueString(SerializedProperty prop) => prop.propertyType switch
        {
            SerializedPropertyType.Float           => prop.floatValue.ToString("F2"),
            SerializedPropertyType.Integer         => prop.intValue.ToString(),
            SerializedPropertyType.Boolean         => prop.boolValue.ToString(),
            SerializedPropertyType.String          => prop.stringValue,
            SerializedPropertyType.ObjectReference => prop.objectReferenceValue?.name ?? "None",
            _                                      => "…"
        };

        static void DrawHint(Rect r, string msg)
        {
            var s = new GUIStyle(EditorStyles.boldLabel)
                { alignment = TextAnchor.MiddleCenter, fontSize = 13, normal = { textColor = new Color(0.4f, 0.4f, 0.4f) } };
            GUI.Label(new Rect(0, 0, r.width, r.height), msg, s);
        }

        // ── 에셋 조작 ────────────────────────────────────────────────────
        void LoadAsset(BTAsset asset) { m_Asset = asset; m_Selected = null; RefreshNodes(); }

        void RefreshNodes() { m_Nodes.Clear(); if (m_Asset != null) m_Nodes = m_Asset.GetAllNodes(); }

        void AddNode(Type type, Vector2 pos)
        {
            if (m_Asset == null) return;
            Undo.RecordObject(m_Asset, "Add Node");
            var node = m_Asset.AddNode(type); node.EditorPosition = pos; m_Selected = node;
            AssetDatabase.SaveAssets(); RefreshNodes(); Repaint();
        }

        void DeleteNode(BTNode node)
        {
            if (m_Asset == null || node == null) return;
            Undo.RecordObject(m_Asset, "Delete Node");
            m_Asset.RemoveNode(node); AssetDatabase.SaveAssets(); RefreshNodes(); Repaint();
        }

        void SaveAsset() { if (m_Asset == null) return; EditorUtility.SetDirty(m_Asset); AssetDatabase.SaveAssets(); }

        void CreateNewAsset()
        {
            string path = EditorUtility.SaveFilePanelInProject("New BT Asset", "NewBTAsset", "asset", "저장 위치 선택");
            if (string.IsNullOrEmpty(path)) return;
            var asset = CreateInstance<BTAsset>(); AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets(); LoadAsset(asset);
        }

        void AutoLayout()
        {
            if (m_Asset?.Root == null) return;
            Undo.RecordObject(m_Asset, "Auto Layout");
            LayoutNode(m_Asset.Root, 0, 0, 240f, 130f);
            SaveAsset();
        }

        void LayoutNode(BTNode node, int depth, int sibIndex, float xSpacing, float ySpacing)
        {
            node.EditorPosition = new Vector2(sibIndex * xSpacing, depth * ySpacing);
            EditorUtility.SetDirty(node);

            if (node is BTComposite comp)
                for (int i = 0; i < comp.Children.Count; i++)
                    if (comp.Children[i] != null)
                        LayoutNode(comp.Children[i], depth + 1, i, xSpacing, ySpacing);
            else if (node is BTDecorator dec && dec.Child != null)
                LayoutNode(dec.Child, depth + 1, 0, xSpacing, ySpacing);
        }

        void RefreshRunners()
        {
            m_Runners.Clear();
            m_Runners.AddRange(FindObjectsByType<BTRunner>(FindObjectsInactive.Include));
            if (m_SelRunner != null && !m_Runners.Contains(m_SelRunner)) m_SelRunner = null;
        }
    }
}
#endif
