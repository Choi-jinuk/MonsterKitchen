using System.Collections.Generic;
using MonsterKitchen.Core;
using MonsterKitchen.Enemy;
using UnityEditor;
using UnityEngine;

namespace MonsterKitchen.Editor
{
    // ====================================================================
    //  FlockAutomation — 군집 리워크용 Editor 에셋 자동화 (batchmode 실행)
    //
    //  실행:
    //    Unity.exe -projectPath <P> -batchmode -quit
    //      -executeMethod MonsterKitchen.Editor.FlockAutomation.Run
    //      -logFile flock_auto.log
    //
    //  처리:
    //    1. SyncAllSO — CSV(MON_006 포함) → TableData
    //    2. Slime/Orc 프리팹 m_FlockWeights = Default 직렬화 기입 (빌드 깨짐 방지)
    //    3. Orc 프리팹 생성 (Slime 복제 → SlimeMovement→ContinuousMovement)
    //    4. AssetManifest 에 1006 prefab/sprite/bt 등록
    //    5. Layer Collision Matrix: Enemy(8)↔Enemy(8) 해제
    //  메뉴로도 실행 가능: MonsterKitchen/Flock/Run Asset Automation
    // ====================================================================
    public static class FlockAutomation
    {
        const string SlimePrefabPath = "Assets/Prefabs/Enemies/Slime.prefab";
        const string OrcPrefabPath   = "Assets/Prefabs/Enemies/Orc.prefab";
        const string SlimeSpritePath = "Assets/Sprites/Char/Slime.png";
        const string MonsterBtPath   = "Assets/Data/BehaviorTrees/MonsterBT.asset";
        const string ManifestPath    = "Assets/Data/AssetManifest.asset";
        const int    EnemyLayer      = 8;

        [MenuItem("MonsterKitchen/Flock/Run Asset Automation")]
        public static void Run()
        {
            Debug.Log("[FlockAutomation] 시작");

            // ── 1. 데이터 동기화 (MON_006 → TableData) ──────────────────
            DataManagerWindow.SyncAllSO();

            // ── 2. Orc 프리팹 생성 (Slime 복제) ─────────────────────────
            CreateOrcPrefab();

            // ── 3. Slime / Orc 프리팹 FlockWeights 기본값 기입 ──────────
            FixFlockWeights(SlimePrefabPath);
            FixFlockWeights(OrcPrefabPath);

            // ── 4. AssetManifest 등록 ───────────────────────────────────
            RegisterManifest();

            // ── 5. Layer Collision Matrix: Enemy↔Enemy 해제 ────────────
            Physics2D.IgnoreLayerCollision(EnemyLayer, EnemyLayer, true);
            Debug.Log("[FlockAutomation] Layer Collision Matrix: Enemy↔Enemy 충돌 해제");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[FlockAutomation] 완료 ✓");
        }

        // ────────────────────────────────────────────────────────────────

        static void CreateOrcPrefab()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(OrcPrefabPath) != null)
            {
                Debug.Log($"[FlockAutomation] Orc 프리팹 이미 존재 — 스킵: {OrcPrefabPath}");
                return;
            }

            if (!AssetDatabase.CopyAsset(SlimePrefabPath, OrcPrefabPath))
            {
                Debug.LogError($"[FlockAutomation] Slime 프리팹 복제 실패: {SlimePrefabPath} → {OrcPrefabPath}");
                return;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(OrcPrefabPath);
            try
            {
                root.name = "Orc";

                var slimeMove = root.GetComponent<SlimeMovement>();
                if (slimeMove != null) Object.DestroyImmediate(slimeMove, true);

                if (root.GetComponent<ContinuousMovement>() == null)
                    root.AddComponent<ContinuousMovement>();

                PrefabUtility.SaveAsPrefabAsset(root, OrcPrefabPath);
                Debug.Log($"[FlockAutomation] Orc 프리팹 생성 (ContinuousMovement): {OrcPrefabPath}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static void FixFlockWeights(string prefabPath)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var btc = root.GetComponent<BTMonsterController>();
                if (btc == null)
                {
                    Debug.LogWarning($"[FlockAutomation] BTMonsterController 없음 — 스킵: {prefabPath}");
                    return;
                }

                var def = FlockWeights.Default;
                var so  = new SerializedObject(btc);
                SetFloat(so, "m_FlockWeights.SepWeight",     def.SepWeight);
                SetFloat(so, "m_FlockWeights.SeekWeight",    def.SeekWeight);
                SetFloat(so, "m_FlockWeights.ArrivalWeight", def.ArrivalWeight);
                SetFloat(so, "m_FlockWeights.TangentWeight", def.TangentWeight);
                SetFloat(so, "m_FlockWeights.SepRadius",     def.SepRadius);
                SetFloat(so, "m_FlockWeights.SlowRadius",    def.SlowRadius);
                SetFloat(so, "m_FlockWeights.RingBand",      def.RingBand);
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                Debug.Log($"[FlockAutomation] FlockWeights=Default 기입: {prefabPath}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static void SetFloat(SerializedObject so, string path, float value)
        {
            var p = so.FindProperty(path);
            if (p != null) p.floatValue = value;
            else Debug.LogWarning($"[FlockAutomation] 프로퍼티 없음: {path}");
        }

        static void RegisterManifest()
        {
            var manifest = AssetDatabase.LoadMainAssetAtPath(ManifestPath);
            if (manifest == null)
            {
                Debug.LogError($"[FlockAutomation] AssetManifest 로드 실패: {ManifestPath}");
                return;
            }

            var orc    = AssetDatabase.LoadAssetAtPath<GameObject>(OrcPrefabPath);
            var sprite  = AssetDatabase.LoadAssetAtPath<Sprite>(SlimeSpritePath);
            var bt     = AssetDatabase.LoadMainAssetAtPath(MonsterBtPath);

            var so      = new SerializedObject(manifest);
            var entries = so.FindProperty("m_Entries");

            var existing = new HashSet<string>();
            for (int i = 0; i < entries.arraySize; i++)
                existing.Add(entries.GetArrayElementAtIndex(i).FindPropertyRelative("Key").stringValue);

            AddEntry(entries, existing, "prefab/monster/1006", orc);
            AddEntry(entries, existing, "sprite/monster/1006", sprite);
            AddEntry(entries, existing, "bt/monster/1006",     bt);

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(manifest);
            Debug.Log("[FlockAutomation] AssetManifest 1006 등록 완료");
        }

        static void AddEntry(SerializedProperty entries, HashSet<string> existing, string key, Object asset)
        {
            if (existing.Contains(key))
            {
                Debug.Log($"[FlockAutomation] 매니페스트 키 이미 존재 — 스킵: {key}");
                return;
            }
            if (asset == null)
            {
                Debug.LogError($"[FlockAutomation] 에셋 null — 등록 실패: {key}");
                return;
            }

            int idx = entries.arraySize;
            entries.InsertArrayElementAtIndex(idx);
            var e = entries.GetArrayElementAtIndex(idx);
            e.FindPropertyRelative("Key").stringValue           = key;
            e.FindPropertyRelative("Asset").objectReferenceValue = asset;
            existing.Add(key);
            Debug.Log($"[FlockAutomation] 매니페스트 등록: {key} → {asset.name}");
        }
    }
}
