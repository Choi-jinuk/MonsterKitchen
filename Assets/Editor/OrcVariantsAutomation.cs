using System.Collections.Generic;
using MonsterKitchen.EditorTools;
using UnityEditor;
using UnityEngine;

namespace MonsterKitchen.Editor
{
    // ====================================================================
    //  OrcVariantsAutomation — 오크 변종(1007/1008) 데이터·에셋 자동화 (batchmode)
    //
    //  실행:
    //    Unity.exe -projectPath <P> -batchmode -quit
    //      -executeMethod MonsterKitchen.Editor.OrcVariantsAutomation.Run
    //      -logFile orc_variants.log
    //
    //  처리:
    //    1. SyncAllSO        — CSV(MON_007/008, SGD_106/107, SKL_M06/07) → TableData
    //    2. RegisterManifest — 1007/1008 의 prefab/sprite/bt 키 등록
    //                          (기존 Orc 프리팹·Slime 스프라이트·공용 MonsterBT 재사용)
    //    3. ImportCsv        — StringData.csv → Localization Strings 재임포트
    //  메뉴로도 실행 가능: MonsterKitchen/Flock/Run Orc Variants Automation
    // ====================================================================
    public static class OrcVariantsAutomation
    {
        const string OrcPrefabPath   = "Assets/Prefabs/Enemies/Orc.prefab";
        const string SlimeSpritePath = "Assets/Sprites/Char/Slime.png";
        const string MonsterBtPath   = "Assets/Data/BehaviorTrees/MonsterBT.asset";
        const string ManifestPath    = "Assets/Data/AssetManifest.asset";

        static readonly uint[] s_NewIds = { 1007, 1008 };

        [MenuItem("MonsterKitchen/Flock/Run Orc Variants Automation")]
        public static void Run()
        {
            Debug.Log("[OrcVariantsAutomation] 시작");

            // ── 1. 데이터 동기화 (CSV → TableData) ─────────────────────
            DataManagerWindow.SyncAllSO();

            // ── 2. AssetManifest 등록 (공용 에셋 재사용) ───────────────
            RegisterManifest();

            // ── 3. Localization Strings 재임포트 ───────────────────────
            LocalizationSetupTool.ImportCsv();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[OrcVariantsAutomation] 완료 ✓");
        }

        static void RegisterManifest()
        {
            var manifest = AssetDatabase.LoadMainAssetAtPath(ManifestPath);
            if (manifest == null)
            {
                Debug.LogError($"[OrcVariantsAutomation] AssetManifest 로드 실패: {ManifestPath}");
                return;
            }

            var orc    = AssetDatabase.LoadAssetAtPath<GameObject>(OrcPrefabPath);
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SlimeSpritePath);
            var bt     = AssetDatabase.LoadMainAssetAtPath(MonsterBtPath);

            if (orc == null)    Debug.LogError($"[OrcVariantsAutomation] Orc 프리팹 로드 실패: {OrcPrefabPath}");
            if (sprite == null) Debug.LogError($"[OrcVariantsAutomation] Slime 스프라이트 로드 실패: {SlimeSpritePath}");
            if (bt == null)     Debug.LogError($"[OrcVariantsAutomation] MonsterBT 로드 실패: {MonsterBtPath}");

            var so      = new SerializedObject(manifest);
            var entries = so.FindProperty("m_Entries");

            var existing = new HashSet<string>();
            for (int i = 0; i < entries.arraySize; i++)
                existing.Add(entries.GetArrayElementAtIndex(i).FindPropertyRelative("Key").stringValue);

            foreach (var id in s_NewIds)
            {
                AddEntry(entries, existing, $"prefab/monster/{id}", orc);
                AddEntry(entries, existing, $"sprite/monster/{id}", sprite);
                AddEntry(entries, existing, $"bt/monster/{id}",     bt);
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(manifest);
            Debug.Log("[OrcVariantsAutomation] AssetManifest 1007/1008 등록 완료");
        }

        static void AddEntry(SerializedProperty entries, HashSet<string> existing, string key, Object asset)
        {
            if (existing.Contains(key))
            {
                Debug.Log($"[OrcVariantsAutomation] 매니페스트 키 이미 존재 — 스킵: {key}");
                return;
            }
            if (asset == null)
            {
                Debug.LogError($"[OrcVariantsAutomation] 에셋 null — 등록 실패: {key}");
                return;
            }

            int idx = entries.arraySize;
            entries.InsertArrayElementAtIndex(idx);
            var e = entries.GetArrayElementAtIndex(idx);
            e.FindPropertyRelative("Key").stringValue            = key;
            e.FindPropertyRelative("Asset").objectReferenceValue = asset;
            existing.Add(key);
            Debug.Log($"[OrcVariantsAutomation] 매니페스트 등록: {key} → {asset.name}");
        }
    }
}
