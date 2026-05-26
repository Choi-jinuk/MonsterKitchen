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
    ///   - 우클릭 → Add Node (노드 추가)
    ///   - 노드 드래그 → 위치 이동
    ///   - Alt + 드래그 → 연결선 연결
    ///   - 우클릭 노드 → Set as Root / Delete
    ///   - 우측 Inspector 에서 파라미터 편집
    ///   - 우측 하단 Blackboard 패널에서 기본값 키-값 설정
    ///
    /// ▶ 플레이 모드
    ///   - 좌측 BTRunners 패널에서 실행 중인 에이전트 선택
    ///   - 노드 하단 컬러 바로 실행 상태 표시 (초록=Success, 빨강=Failure, 노랑=Running)
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
        const float PORT_R         = 6f;
        const float STATUS_BAR_H   = 5f;

        // ── 색상 ──────────────────────────────────────────────────────────
        static readonly Color ColCanvas    = new Color(0.14f, 0.14f, 0.14f);
        static readonly Color ColGrid      = new Color(0.20f, 0.20f, 0.20f);
        static readonly Color ColSelector  = new Color(0.35f, 0.60f, 1.00f);
        static readonly Color ColSequence  = new Color(0.35f, 0.85f, 0.45f);
        static readonly Color ColCondition = new Color(1.00f, 0.85f, 0.25f);
        static readonly Color ColAction    = new Color(0.55f, 0.90f, 0.95f);
        static readonly Color ColDefault   = new Color(0.60f, 0.60f, 0.60f);
        static readonly Color ColSelected  = new Color(1.00f, 0.70f, 0.10f);
        static readonly Color ColRoot      = new Color(1.00f, 0.40f, 0.40f);
        static readonly Color ColWire      = new Color(0.75f, 0.75f, 0.75f);
        static readonly Color ColBody      = new Color(0.22f, 0.22f, 0.22f);

        static readonly Color ColStatusRunning = new Color(1.00f, 0.85f, 0.00f);
        static readonly Color ColStatusSuccess = new Color(0.20f, 0.90f, 0.30f);
        static readonly Color ColStatusFailure = new Color(0.90f, 0.20f, 0.20f);

        // ── 에셋 / 노드 상태 ──────────────────────────────────────────────
        BTAsset      _asset;
        List<BTNode> _nodes = new();
        BTNode       _selected;

        Vector2 _offset  = new Vector2(80, 80);
        bool    _draggingCanvas;
        Vector2 _dragCanvasStart;

        bool    _draggingNode;
        BTNode  _dragNode;
        Vector2 _dragNodeStart;
        Vector2 _dragMouseStart;

        bool    _connecting;
        BTNode  _connectFrom;
        Vector2 _connectMouse;

        // ── 런타임 ────────────────────────────────────────────────────────
        List<BTRunner> _runners    = new();
        BTRunner       _selRunner;
        Vector2        _runtimeScroll;
        double         _nextRefresh;

        // ── 인스펙터 / 블랙보드 스크롤 ───────────────────────────────────
        Vector2 _inspectorScroll;
        Vector2 _bbScroll;
        bool    _bbFoldout = true;

        // ── 메뉴 ──────────────────────────────────────────────────────────
        [MenuItem("MonsterKitchen/BehaviorTree Editor")]
        public static void Open()
        {
            var w = GetWindow<BTGraphEditor>("BT Editor");
            w.minSize = new Vector2(860, 520);
        }

        // ── Unity 콜백 ────────────────────────────────────────────────────
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
            if (EditorApplication.timeSinceStartup < _nextRefresh) return;
            _nextRefresh = EditorApplication.timeSinceStartup + 0.25; // 4fps refresh
            RefreshRunners();
            Repaint();
        }

        void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode ||
                state == PlayModeStateChange.EnteredEditMode)
            {
                _selRunner = null;
                RefreshRunners();
                Repaint();
            }
        }

        // ── 드로우 ────────────────────────────────────────────────────────
        void OnGUI()
        {
            DrawToolbar();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (Application.isPlaying) DrawRuntimePanel();
                DrawGraphCanvas();
                DrawRightPanel();
            }

            if (_connecting) Repaint();
            HandleHotkeys();
        }

        // ── 툴바 ─────────────────────────────────────────────────────────
        void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                var newAsset = (BTAsset)EditorGUILayout.ObjectField(
                    _asset, typeof(BTAsset), false, GUILayout.Width(220));
                if (newAsset != _asset) LoadAsset(newAsset);

                GUILayout.Space(6);

                if (GUILayout.Button("New Asset", EditorStyles.toolbarButton, GUILayout.Width(76)))
                    CreateNewAsset();

                if (_asset != null)
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

        // ── 런타임 패널 (좌측, Play 모드 전용) ───────────────────────────
        void DrawRuntimePanel()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(RUNTIME_PANEL_W)))
            {
                EditorGUILayout.LabelField("BTRunners", EditorStyles.boldLabel);
                _runtimeScroll = EditorGUILayout.BeginScrollView(_runtimeScroll,
                    GUILayout.ExpandHeight(true));

                foreach (var r in _runners)
                {
                    if (r == null) continue;
                    bool sel   = r == _selRunner;
                    var  style = sel ? GUI.skin.button : EditorStyles.miniButton;
                    if (GUILayout.Button(r.name, style))
                    {
                        _selRunner = r;
                        Selection.activeGameObject = r.gameObject;
                        if (r.EditorAsset != null) LoadAsset(r.EditorAsset);
                    }
                }
                EditorGUILayout.EndScrollView();

                if (_selRunner != null)
                {
                    EditorGUILayout.Space(2);
                    var statusStyle = new GUIStyle(EditorStyles.miniLabel);
                    if (_selRunner.EditorAborted)
                    {
                        statusStyle.normal.textColor = new Color(1f, 0.4f, 0.4f);
                        EditorGUILayout.LabelField("⛔ Aborted", statusStyle);
                    }
                    else if (_selRunner.EditorRoot == null)
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

                // 컬러 범례
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Node Status", EditorStyles.boldLabel);
                DrawLegend(ColStatusRunning, "Running");
                DrawLegend(ColStatusSuccess, "Success");
                DrawLegend(ColStatusFailure, "Failure");
            }

            var div = EditorGUILayout.GetControlRect(false,
                GUILayout.Width(1), GUILayout.ExpandHeight(true));
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

            EditorGUI.DrawRect(cr, ColCanvas);
            DrawGrid(cr);

            GUI.BeginClip(cr);

            if (_asset != null)
            {
                DrawWires();
                DrawNodes();

                if (_connecting && _connectFrom != null)
                {
                    Rect fr = NodeRect(_connectFrom);
                    DrawBezier(new Vector2(fr.center.x, fr.yMax),
                               _connectMouse - cr.position, ColSelected);
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
            Handles.color = ColGrid;
            float ox = _offset.x % sp, oy = _offset.y % sp;
            int   cols = Mathf.CeilToInt(r.width  / sp) + 1;
            int   rows = Mathf.CeilToInt(r.height / sp) + 1;
            for (int i = 0; i <= cols; i++)
                Handles.DrawLine(new Vector3(ox + i * sp, 0), new Vector3(ox + i * sp, r.height));
            for (int i = 0; i <= rows; i++)
                Handles.DrawLine(new Vector3(0, oy + i * sp), new Vector3(r.width, oy + i * sp));
        }

        // ── 노드 ─────────────────────────────────────────────────────────
        void DrawNodes()
        {
            foreach (var node in _nodes)
                if (node != null) DrawNode(node);
        }

        void DrawNode(BTNode node)
        {
            Rect  nr    = NodeRect(node);
            Color bg    = NodeColor(node);
            bool  isRoot = _asset != null && _asset.root == node;
            bool  isSel  = node == _selected;

            // 선택/루트 테두리
            if (isRoot)      EditorGUI.DrawRect(Expand(nr, 3), ColRoot);
            else if (isSel)  EditorGUI.DrawRect(Expand(nr, 2), ColSelected);

            EditorGUI.DrawRect(nr, ColBody);

            // 헤더
            var hr = new Rect(nr.x, nr.y, nr.width, HEADER_H);
            EditorGUI.DrawRect(hr, bg);

            string title = (isRoot ? "[R] " : "") + node.name;
            var    hs    = new GUIStyle(EditorStyles.boldLabel)
                           { alignment = TextAnchor.MiddleCenter, fontSize = 11 };
            hs.normal.textColor = Color.black;
            GUI.Label(hr, title, hs);

            // 필드 값 표시
            var so   = new SerializedObject(node);
            var prop = so.GetIterator();
            float py = nr.y + HEADER_H + 3f;
            var ps   = new GUIStyle(EditorStyles.miniLabel)
                       { normal = { textColor = new Color(0.8f, 0.8f, 0.8f) } };

            prop.NextVisible(true);
            while (prop.NextVisible(false))
            {
                if (prop.name == "editorPosition") continue;
                string val = PropValueString(prop);
                GUI.Label(new Rect(nr.x + 6, py, nr.width - 12, FIELD_H),
                          $"{prop.displayName}: {val}", ps);
                py += FIELD_H;
            }

            // ── 런타임 상태 바 ────────────────────────────────────────────
            if (Application.isPlaying && _selRunner?.EditorContext != null)
            {
                Color statusCol = Color.clear;
                if (_selRunner.EditorContext.TryGetLastStatus(node, out var st))
                {
                    statusCol = st switch
                    {
                        BTStatus.Running => ColStatusRunning,
                        BTStatus.Success => ColStatusSuccess,
                        BTStatus.Failure => ColStatusFailure,
                        _                => Color.clear
                    };
                }

                // 상태 바 (노드 하단)
                var statusBar = new Rect(nr.x, nr.yMax - STATUS_BAR_H, nr.width, STATUS_BAR_H);
                EditorGUI.DrawRect(statusBar, statusCol != Color.clear
                    ? statusCol : new Color(0.15f, 0.15f, 0.15f));

                // 상태 텍스트 (헤더 우측)
                if (statusCol != Color.clear)
                {
                    string stText = st == BTStatus.Running ? "▶" :
                                   st == BTStatus.Success  ? "✓" : "✕";
                    var stStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 10 };
                    stStyle.normal.textColor = statusCol;
                    GUI.Label(new Rect(nr.xMax - 18, hr.y, 16, HEADER_H), stText, stStyle);
                }
            }

            // 포트 (하단 중앙)
            var port = new Rect(nr.center.x - PORT_R, nr.yMax - PORT_R - STATUS_BAR_H,
                                PORT_R * 2, PORT_R * 2);
            EditorGUI.DrawRect(port, new Color(0.75f, 0.75f, 0.75f));
        }

        // ── 와이어 ───────────────────────────────────────────────────────
        void DrawWires()
        {
            foreach (var node in _nodes)
            {
                if (node is not BTComposite composite) continue;
                Rect    pr   = NodeRect(node);
                Vector2 from = new Vector2(pr.center.x, pr.yMax);
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

        // ── 우측 패널 (Inspector + Blackboard) ───────────────────────────
        void DrawRightPanel()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(INSPECTOR_W)))
            {
                _inspectorScroll = EditorGUILayout.BeginScrollView(_inspectorScroll,
                    GUILayout.ExpandHeight(true));

                DrawInspector();
                DrawBlackboardPanel();

                EditorGUILayout.EndScrollView();
            }
        }

        // ── 노드 인스펙터 ─────────────────────────────────────────────────
        void DrawInspector()
        {
            EditorGUILayout.LabelField("Inspector", EditorStyles.boldLabel);

            if (_asset == null)
            {
                EditorGUILayout.HelpBox("BTAsset 을 선택하세요.", MessageType.None);
                return;
            }

            if (_selected == null)
            {
                EditorGUILayout.HelpBox("노드를 클릭해 선택하세요.", MessageType.None);
            }
            else
            {
                // 루트 설정
                bool isRoot    = _asset.root == _selected;
                bool newIsRoot = EditorGUILayout.Toggle("Root Node", isRoot);
                if (newIsRoot != isRoot)
                {
                    Undo.RecordObject(_asset, "Set Root");
                    _asset.root = newIsRoot ? _selected : null;
                    EditorUtility.SetDirty(_asset);
                }

                EditorGUILayout.Space(4);

                // 필드 편집
                var so   = new SerializedObject(_selected);
                so.Update();
                var prop = so.GetIterator();
                prop.NextVisible(true);

                EditorGUI.BeginChangeCheck();
                while (prop.NextVisible(false))
                {
                    if (prop.name == "editorPosition") continue;
                    EditorGUILayout.PropertyField(prop, true);
                }
                if (EditorGUI.EndChangeCheck()) so.ApplyModifiedProperties();

                EditorGUILayout.Space(6);

                // 자식 목록 (Composite 전용)
                if (_selected is BTComposite composite)
                {
                    EditorGUILayout.LabelField("Children", EditorStyles.boldLabel);
                    for (int i = 0; i < composite.Children.Count; i++)
                    {
                        var child = composite.Children[i];
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            EditorGUILayout.LabelField($"[{i}] {child?.name ?? "null"}",
                                GUILayout.ExpandWidth(true));
                            if (GUILayout.Button("↑", GUILayout.Width(22)) && i > 0)
                            { Undo.RecordObject(_selected, "Reorder"); composite.ReorderChild(i, i - 1); }
                            if (GUILayout.Button("↓", GUILayout.Width(22)) && i < composite.Children.Count - 1)
                            { Undo.RecordObject(_selected, "Reorder"); composite.ReorderChild(i, i + 1); }
                            if (GUILayout.Button("×", GUILayout.Width(22)))
                            { Undo.RecordObject(_selected, "Remove Child"); composite.RemoveChild(child); break; }
                        }
                    }
                }

                EditorGUILayout.Space(8);
                GUI.color = new Color(1f, 0.5f, 0.5f);
                if (GUILayout.Button("Delete Node"))
                {
                    DeleteNode(_selected);
                    _selected = null;
                }
                GUI.color = Color.white;
            }

            EditorGUILayout.Space(8);
            DrawSeparator();
        }

        // ── 블랙보드 패널 ─────────────────────────────────────────────────
        void DrawBlackboardPanel()
        {
            _bbFoldout = EditorGUILayout.Foldout(_bbFoldout, "Blackboard", true, EditorStyles.foldoutHeader);
            if (!_bbFoldout) return;

            EditorGUI.indentLevel++;

            if (Application.isPlaying)
                DrawBlackboardLive();
            else
                DrawBlackboardEdit();

            EditorGUI.indentLevel--;
        }

        /// <summary>플레이 모드: 선택된 Runner 의 라이브 BB 값 표시</summary>
        void DrawBlackboardLive()
        {
            if (_selRunner == null)
            {
                EditorGUILayout.HelpBox("좌측에서 BTRunner 를 선택하세요.", MessageType.None);
                return;
            }

            var bb = _selRunner.EditorContext?.Blackboard;
            if (bb == null)
            {
                EditorGUILayout.HelpBox("블랙보드가 초기화되지 않았습니다.", MessageType.None);
                return;
            }

            var headerStyle = new GUIStyle(EditorStyles.miniLabel)
                { normal = { textColor = new Color(0.6f, 0.9f, 1f) } };
            EditorGUILayout.LabelField($"Agent: {_selRunner.name}", headerStyle);

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
                    EditorGUILayout.LabelField(kv.Key,   GUILayout.Width(INSPECTOR_W * 0.45f));
                    EditorGUILayout.LabelField(valStr,   EditorStyles.miniLabel);
                }
            }
            EditorGUI.EndDisabledGroup();
        }

        /// <summary>에디트 모드: BTAsset.blackboardDefaults 편집</summary>
        void DrawBlackboardEdit()
        {
            if (_asset == null)
            {
                EditorGUILayout.HelpBox("BTAsset 을 선택하세요.", MessageType.None);
                return;
            }

            EditorGUILayout.HelpBox(
                "여기서 설정한 값은 BTRunner.Start() 시 블랙보드에 먼저 적용됩니다.\n" +
                "IBTBlackboardInitializer 에서 같은 키를 덮어쓸 수 있습니다.",
                MessageType.Info);

            var so = new SerializedObject(_asset);
            so.Update();

            var listProp = so.FindProperty("blackboardDefaults");

            EditorGUI.BeginChangeCheck();

            for (int i = 0; i < listProp.arraySize; i++)
            {
                var entry = listProp.GetArrayElementAtIndex(i);
                var keyProp   = entry.FindPropertyRelative("key");
                var typeProp  = entry.FindPropertyRelative("valueType");

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.PropertyField(keyProp,
                            GUIContent.none, GUILayout.ExpandWidth(true));
                        EditorGUILayout.PropertyField(typeProp,
                            GUIContent.none, GUILayout.Width(70));
                        if (GUILayout.Button("−", GUILayout.Width(20)))
                        {
                            listProp.DeleteArrayElementAtIndex(i);
                            break;
                        }
                    }

                    // 타입에 맞는 값 필드
                    var bbType = (BBValueType)typeProp.enumValueIndex;
                    string valRelName = bbType switch
                    {
                        BBValueType.Float   => "floatValue",
                        BBValueType.Int     => "intValue",
                        BBValueType.Bool    => "boolValue",
                        BBValueType.String  => "stringValue",
                        BBValueType.Vector2 => "vector2Value",
                        BBValueType.Vector3 => "vector3Value",
                        _                   => "floatValue"
                    };
                    var valProp = entry.FindPropertyRelative(valRelName);
                    EditorGUILayout.PropertyField(valProp, new GUIContent("Value"));
                }
            }

            if (GUILayout.Button("＋ Add Entry"))
                listProp.arraySize++;

            if (EditorGUI.EndChangeCheck())
            {
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(_asset);
            }
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
            if (_asset == null) return;

            var e = Event.current;
            if (!cr.Contains(e.mousePosition)) return;

            Vector2 local = e.mousePosition - cr.position;

            switch (e.type)
            {
                case EventType.MouseDown when e.button == 0:
                {
                    BTNode hit = HitTest(local);
                    if (e.alt && hit != null)
                    {
                        _connecting   = true;
                        _connectFrom  = hit;
                        _connectMouse = e.mousePosition;
                        e.Use();
                    }
                    else if (hit != null)
                    {
                        _selected       = hit;
                        _draggingNode   = true;
                        _dragNode       = hit;
                        _dragNodeStart  = hit.editorPosition;
                        _dragMouseStart = local;
                        e.Use();
                        Repaint();
                    }
                    else
                    {
                        _selected        = null;
                        _draggingCanvas  = true;
                        _dragCanvasStart = local;
                        e.Use();
                        Repaint();
                    }
                    break;
                }

                case EventType.MouseDrag when e.button == 0:
                    if (_draggingNode && _dragNode != null)
                    {
                        Undo.RecordObject(_dragNode, "Move Node");
                        _dragNode.editorPosition = _dragNodeStart + (local - _dragMouseStart);
                        EditorUtility.SetDirty(_dragNode);
                        e.Use();
                        Repaint();
                    }
                    else if (_draggingCanvas)
                    {
                        _offset += local - _dragCanvasStart;
                        _dragCanvasStart = local;
                        e.Use();
                        Repaint();
                    }
                    else if (_connecting)
                    {
                        _connectMouse = e.mousePosition;
                        e.Use();
                    }
                    break;

                case EventType.MouseUp when e.button == 0:
                    if (_draggingNode)   { _draggingNode  = false; _dragNode = null; }
                    if (_draggingCanvas) _draggingCanvas  = false;
                    if (_connecting)
                    {
                        _connecting = false;
                        BTNode target = HitTest(local);
                        if (target != null && target != _connectFrom &&
                            _connectFrom is BTComposite comp)
                        {
                            Undo.RecordObject(_connectFrom, "Add Connection");
                            comp.AddChild(target);
                        }
                        _connectFrom = null;
                        Repaint();
                    }
                    break;

                case EventType.MouseDown when e.button == 1:
                    ShowContextMenu(local, HitTest(local));
                    e.Use();
                    break;

                case EventType.ScrollWheel:
                    _offset -= e.delta * 2f;
                    e.Use();
                    Repaint();
                    break;
            }
        }

        void HandleHotkeys()
        {
            var e = Event.current;
            if (e.type == EventType.KeyDown &&
                e.keyCode == KeyCode.Delete &&
                _selected != null && _asset != null)
            {
                DeleteNode(_selected);
                _selected = null;
                e.Use();
                Repaint();
            }
        }

        // ── 컨텍스트 메뉴 ────────────────────────────────────────────────
        void ShowContextMenu(Vector2 local, BTNode hit)
        {
            var menu = new GenericMenu();

            foreach (var kv in BTNodeRegistry.AllByPath.OrderBy(x => x.Key))
            {
                var type     = kv.Value;
                var menuPath = "Add Node/" + kv.Key;
                var pos      = local - _offset;
                menu.AddItem(new GUIContent(menuPath), false, () => AddNode(type, pos));
            }

            if (hit != null)
            {
                menu.AddSeparator("");

                menu.AddItem(new GUIContent("Set as Root"), false, () =>
                {
                    Undo.RecordObject(_asset, "Set Root");
                    _asset.root = hit;
                    EditorUtility.SetDirty(_asset);
                    Repaint();
                });

                if (_selected != null && _selected != hit && _selected is BTComposite selComp)
                {
                    menu.AddItem(new GUIContent("Connect Selected → This"), false, () =>
                    {
                        Undo.RecordObject(_selected, "Add Connection");
                        selComp.AddChild(hit);
                        Repaint();
                    });
                }

                menu.AddSeparator("");
                var captured = hit;
                menu.AddItem(new GUIContent("Delete"), false, () =>
                {
                    DeleteNode(captured);
                    if (_selected == captured) _selected = null;
                    Repaint();
                });
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
                if (prop.name != "editorPosition") fieldCount++;

            float h = HEADER_H + fieldCount * FIELD_H + STATUS_BAR_H + 8f;
            return new Rect(_offset.x + node.editorPosition.x,
                            _offset.y + node.editorPosition.y,
                            NODE_W, h);
        }

        BTNode HitTest(Vector2 local)
        {
            if (_nodes == null) return null;
            for (int i = _nodes.Count - 1; i >= 0; i--)
                if (_nodes[i] != null && NodeRect(_nodes[i]).Contains(local))
                    return _nodes[i];
            return null;
        }

        static Color NodeColor(BTNode node)
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

        static string PropValueString(SerializedProperty prop)
        {
            return prop.propertyType switch
            {
                SerializedPropertyType.Float         => prop.floatValue.ToString("F2"),
                SerializedPropertyType.Integer       => prop.intValue.ToString(),
                SerializedPropertyType.Boolean       => prop.boolValue.ToString(),
                SerializedPropertyType.String        => prop.stringValue,
                SerializedPropertyType.ObjectReference
                                                     => prop.objectReferenceValue?.name ?? "None",
                _                                    => "…"
            };
        }

        static void DrawHint(Rect r, string msg)
        {
            var s = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize  = 13,
                normal    = { textColor = new Color(0.4f, 0.4f, 0.4f) }
            };
            GUI.Label(new Rect(0, 0, r.width, r.height), msg, s);
        }

        // ── 에셋 조작 ────────────────────────────────────────────────────
        void LoadAsset(BTAsset asset)
        {
            _asset    = asset;
            _selected = null;
            RefreshNodes();
        }

        void RefreshNodes()
        {
            _nodes.Clear();
            if (_asset != null)
                _nodes = _asset.GetAllNodes();
        }

        void AddNode(Type type, Vector2 pos)
        {
            if (_asset == null) return;
            Undo.RecordObject(_asset, "Add Node");
            var node = _asset.AddNode(type);
            node.editorPosition = pos;
            _selected = node;
            AssetDatabase.SaveAssets();
            RefreshNodes();
            Repaint();
        }

        void DeleteNode(BTNode node)
        {
            if (_asset == null || node == null) return;
            Undo.RecordObject(_asset, "Delete Node");
            _asset.RemoveNode(node);
            AssetDatabase.SaveAssets();
            RefreshNodes();
            Repaint();
        }

        void SaveAsset()
        {
            if (_asset == null) return;
            EditorUtility.SetDirty(_asset);
            AssetDatabase.SaveAssets();
        }

        void CreateNewAsset()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "New BT Asset", "NewBTAsset", "asset", "저장 위치 선택");
            if (string.IsNullOrEmpty(path)) return;
            var asset = CreateInstance<BTAsset>();
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            LoadAsset(asset);
        }

        void AutoLayout()
        {
            if (_asset?.root == null) return;
            Undo.RecordObject(_asset, "Auto Layout");
            LayoutNode(_asset.root, 0, 0, 240f, 130f);
            SaveAsset();
        }

        void LayoutNode(BTNode node, int depth, int sibIndex, float xSpacing, float ySpacing)
        {
            node.editorPosition = new Vector2(sibIndex * xSpacing, depth * ySpacing);
            EditorUtility.SetDirty(node);
            if (node is BTComposite comp)
                for (int i = 0; i < comp.Children.Count; i++)
                    if (comp.Children[i] != null)
                        LayoutNode(comp.Children[i], depth + 1, i, xSpacing, ySpacing);
        }

        void RefreshRunners()
        {
            _runners.Clear();
            _runners.AddRange(FindObjectsByType<BTRunner>(FindObjectsInactive.Include));
            if (_selRunner != null && !_runners.Contains(_selRunner))
                _selRunner = null;
        }
    }
}
#endif
