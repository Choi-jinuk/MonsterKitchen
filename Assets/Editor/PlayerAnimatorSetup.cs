using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

public static class PlayerAnimatorSetup
{
    const string ANIM_BASE   = "Assets/COPY SPRIGHT/Comic Battle Royale/2D Character - Astronaut/Variant A/Animations/";
    const string CTRL_PATH   = "Assets/Animations/PlayerAnimator.controller";
    const string OVL_PATH    = "Assets/Animations/PlayerOverlayAnimator.controller";
    const string PREFAB_PATH = "Assets/Prefabs/Player/Player.prefab";
    const string URP_MAT_GUID = "a97c105638bdf8b4a8650670310a4cd3";

    [MenuItem("MonsterKitchen/Setup Player Animator (Variant A)")]
    public static void Run()
    {
        // ── Body 클립 ──────────────────────────────────────────────
        AnimationClip idleDown = LoadClip("idle_down.anim");
        AnimationClip idleSide = LoadClip("idle_side.anim");
        AnimationClip idleUp   = LoadClip("idle_up.anim");
        AnimationClip walkDown = LoadClip("walk_down.anim");
        AnimationClip walkSide = LoadClip("walk_side.anim");
        AnimationClip walkUp   = LoadClip("walk_up.anim");
        AnimationClip atkSide  = LoadClip("sword_attack_side.anim");
        AnimationClip atkDown  = LoadClip("sword_attack_down.anim");
        AnimationClip atkUp    = LoadClip("sword_attack_up.anim");
        AnimationClip dashClip = LoadClip("jump_side.anim");

        // ── Overlay 클립 (sword_attack_up-overlay 없으므로 side 대체)
        AnimationClip ovlSide = LoadClip("sword_attack_side-overlay.anim");
        AnimationClip ovlDown = LoadClip("sword_attack_down-overlay.anim");

        Debug.Log($"[Setup] idle={idleDown} walk={walkSide} atk={atkSide} dash={dashClip} ovlS={ovlSide} ovlD={ovlDown}");

        if (!Check(idleDown, "idle_down") || !Check(idleSide, "idle_side") || !Check(idleUp, "idle_up")
         || !Check(walkDown, "walk_down") || !Check(walkSide, "walk_side") || !Check(walkUp, "walk_up")
         || !Check(atkSide,  "sword_attack_side")  || !Check(atkDown, "sword_attack_down") || !Check(atkUp,   "sword_attack_up")
         || !Check(dashClip, "jump_side")
         || !Check(ovlSide,  "sword_attack_side-overlay") || !Check(ovlDown, "sword_attack_down-overlay"))
            return;

        // ─────────────────────────────────────────────────────────
        // Body AnimatorController
        // ─────────────────────────────────────────────────────────
        DeleteAndCreate(CTRL_PATH, out AnimatorController ctrl);
        ctrl.AddParameter("MoveX",     AnimatorControllerParameterType.Float);
        ctrl.AddParameter("MoveY",     AnimatorControllerParameterType.Float);
        ctrl.AddParameter("Speed",     AnimatorControllerParameterType.Float);
        ctrl.AddParameter("Attack",    AnimatorControllerParameterType.Trigger);
        ctrl.AddParameter("Dash",      AnimatorControllerParameterType.Trigger);
        ctrl.AddParameter("ComboStep", AnimatorControllerParameterType.Int);

        AnimatorStateMachine sm = ctrl.layers[0].stateMachine;

        BlendTree idleTree = MakeDir2D(ctrl, "IdleTree",
            new AnimationClip[] { idleDown, idleSide, idleSide, idleUp },
            new Vector2[]       { Vector2.down, Vector2.right, Vector2.left, Vector2.up });

        BlendTree walkTree = MakeDir2D(ctrl, "WalkTree",
            new AnimationClip[] { walkDown, walkSide, walkSide, walkUp },
            new Vector2[]       { Vector2.down, Vector2.right, Vector2.left, Vector2.up });

        BlendTree atkTree = MakeDir2D(ctrl, "AttackTree",
            new AnimationClip[] { atkDown, atkSide, atkSide, atkUp },
            new Vector2[]       { Vector2.down, Vector2.right, Vector2.left, Vector2.up });

        AnimatorState idleState = sm.AddState("Idle",   new Vector3(250,  0));
        AnimatorState walkState = sm.AddState("Walk",   new Vector3(250, 80));
        AnimatorState atkState  = sm.AddState("Attack", new Vector3(500,  0));
        AnimatorState dashState = sm.AddState("Dash",   new Vector3(500, 80));

        idleState.motion = idleTree;
        walkState.motion = walkTree;
        atkState.motion  = atkTree;
        dashState.motion = dashClip;
        sm.defaultState  = idleState;

        // Idle <-> Walk
        AnimatorStateTransition t1 = idleState.AddTransition(walkState);
        t1.hasExitTime = false; t1.duration = 0.1f;
        t1.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");

        AnimatorStateTransition t2 = walkState.AddTransition(idleState);
        t2.hasExitTime = false; t2.duration = 0.1f;
        t2.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");

        // AnyState -> Attack
        AnimatorStateTransition anyAtk = sm.AddAnyStateTransition(atkState);
        anyAtk.AddCondition(AnimatorConditionMode.If, 0, "Attack");
        anyAtk.hasExitTime = false; anyAtk.duration = 0.05f; anyAtk.canTransitionToSelf = false;

        // Attack -> Idle
        AnimatorStateTransition t3 = atkState.AddTransition(idleState);
        t3.hasExitTime = true; t3.exitTime = 0.9f; t3.duration = 0.1f;

        // AnyState -> Dash
        AnimatorStateTransition anyDash = sm.AddAnyStateTransition(dashState);
        anyDash.AddCondition(AnimatorConditionMode.If, 0, "Dash");
        anyDash.hasExitTime = false; anyDash.duration = 0.05f; anyDash.canTransitionToSelf = false;

        // Dash -> Idle
        AnimatorStateTransition t4 = dashState.AddTransition(idleState);
        t4.hasExitTime = true; t4.exitTime = 0.9f; t4.duration = 0.1f;

        SaveCtrl(ctrl);

        // ─────────────────────────────────────────────────────────
        // Overlay AnimatorController
        // ─────────────────────────────────────────────────────────
        DeleteAndCreate(OVL_PATH, out AnimatorController ovlCtrl);
        ovlCtrl.AddParameter("MoveX",     AnimatorControllerParameterType.Float);
        ovlCtrl.AddParameter("MoveY",     AnimatorControllerParameterType.Float);
        ovlCtrl.AddParameter("Attack",    AnimatorControllerParameterType.Trigger);
        ovlCtrl.AddParameter("ComboStep", AnimatorControllerParameterType.Int);

        AnimatorStateMachine osm = ovlCtrl.layers[0].stateMachine;

        BlendTree ovlTree = MakeDir2D(ovlCtrl, "OvlAttackTree",
            new AnimationClip[] { ovlDown, ovlSide, ovlSide, ovlSide },
            new Vector2[]       { Vector2.down, Vector2.right, Vector2.left, Vector2.up });

        AnimatorState ovlIdle = osm.AddState("Idle",   new Vector3(250,  0));
        AnimatorState ovlAtk  = osm.AddState("Attack", new Vector3(500,  0));
        osm.defaultState = ovlIdle;

        ovlAtk.motion = ovlTree;

        AnimatorStateTransition ovlAnyAtk = osm.AddAnyStateTransition(ovlAtk);
        ovlAnyAtk.AddCondition(AnimatorConditionMode.If, 0, "Attack");
        ovlAnyAtk.AddCondition(AnimatorConditionMode.Less, 0.5f, "MoveY");  // 위쪽 공격 시 오버레이 비활성
        ovlAnyAtk.hasExitTime = false; ovlAnyAtk.duration = 0.05f; ovlAnyAtk.canTransitionToSelf = false;

        AnimatorStateTransition ovlExit = ovlAtk.AddTransition(ovlIdle);
        ovlExit.hasExitTime = true; ovlExit.exitTime = 0.9f; ovlExit.duration = 0.1f;

        SaveCtrl(ovlCtrl);

        // ─────────────────────────────────────────────────────────
        // Player 프리팹: Overlay 자식 추가
        // ─────────────────────────────────────────────────────────
        SetupPrefabOverlay(ovlCtrl);

        Debug.Log("[PlayerAnimatorSetup] Body + Overlay 컨트롤러 빌드 및 프리팹 설정 완료!");
    }

    // ── 프리팹 Overlay 자식 설정 ───────────────────────────────────

    static void SetupPrefabOverlay(AnimatorController ovlCtrl)
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PREFAB_PATH);

        Transform old = prefabRoot.transform.Find("Overlay");
        if (old != null) Object.DestroyImmediate(old.gameObject);

        GameObject overlayGO = new GameObject("Overlay");
        overlayGO.transform.SetParent(prefabRoot.transform, false);
        overlayGO.transform.localPosition = Vector3.zero;

        SpriteRenderer sr = overlayGO.AddComponent<SpriteRenderer>();
        sr.sortingOrder = 2;

        Material mat = AssetDatabase.LoadAssetAtPath<Material>(
            AssetDatabase.GUIDToAssetPath(URP_MAT_GUID));
        if (mat != null) sr.material = mat;

        Animator anim = overlayGO.AddComponent<Animator>();
        anim.runtimeAnimatorController = ovlCtrl;

        // PlayerController._sprites 갱신 (루트 SR + Overlay SR)
        MonsterKitchen.Player.PlayerController pc =
            prefabRoot.GetComponent<MonsterKitchen.Player.PlayerController>();

        SerializedObject so = new SerializedObject(pc);
        SerializedProperty spritesArr = so.FindProperty("_sprites");
        spritesArr.ClearArray();
        spritesArr.InsertArrayElementAtIndex(0);
        spritesArr.GetArrayElementAtIndex(0).objectReferenceValue =
            prefabRoot.GetComponent<SpriteRenderer>();
        spritesArr.InsertArrayElementAtIndex(1);
        spritesArr.GetArrayElementAtIndex(1).objectReferenceValue = sr;
        so.ApplyModifiedProperties();

        // _overlayAnim 연결
        SerializedObject so2 = new SerializedObject(pc);
        SerializedProperty overlayProp = so2.FindProperty("_overlayAnim");
        if (overlayProp != null)
        {
            overlayProp.objectReferenceValue = anim;
            so2.ApplyModifiedProperties();
        }

        PrefabUtility.SaveAsPrefabAsset(prefabRoot, PREFAB_PATH);
        PrefabUtility.UnloadPrefabContents(prefabRoot);
        Debug.Log("[PlayerAnimatorSetup] 프리팹 Overlay 자식 추가 완료.");
    }

    // ── 헬퍼 ──────────────────────────────────────────────────────

    static AnimationClip LoadClip(string file) =>
        AssetDatabase.LoadAssetAtPath<AnimationClip>(ANIM_BASE + file);

    static bool Check(AnimationClip clip, string name)
    {
        if (clip != null) return true;
        Debug.LogError($"[PlayerAnimatorSetup] 클립 없음: {name}");
        return false;
    }

    static void DeleteAndCreate(string path, out AnimatorController ctrl)
    {
        AnimatorController ex = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        if (ex != null) AssetDatabase.DeleteAsset(path);
        ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
    }

    static BlendTree MakeDir2D(AnimatorController ctrl, string treeName,
        AnimationClip[] clips, Vector2[] positions)
    {
        BlendTree tree = new BlendTree();
        tree.name            = treeName;
        tree.blendType       = BlendTreeType.SimpleDirectional2D;
        tree.blendParameter  = "MoveX";
        tree.blendParameterY = "MoveY";
        tree.hideFlags       = HideFlags.HideInHierarchy;
        AssetDatabase.AddObjectToAsset(tree, ctrl);

        for (int i = 0; i < clips.Length; i++)
            if (clips[i] != null)
                tree.AddChild(clips[i], positions[i]);

        return tree;
    }

    static void SaveCtrl(AnimatorController ctrl)
    {
        EditorUtility.SetDirty(ctrl);
        AssetDatabase.SaveAssets();
    }
}
