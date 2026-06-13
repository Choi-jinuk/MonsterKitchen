using UnityEditor;
using UnityEngine;

namespace MonsterKitchen.Editor
{
    // ====================================================================
    //  CompanionAutomation — 동료 파티용 Editor 에셋 자동화 (batchmode)
    //
    //  실행:
    //    Unity.exe -projectPath <P> -batchmode -quit
    //      -executeMethod MonsterKitchen.Editor.CompanionAutomation.Run -logFile out.log
    //
    //  처리:
    //    1. SyncAllSO — CSV(PLR_004/005) → TableData
    //    2. CompanionBT 에셋 생성 (PlayerBT 복제) — 트리는 Editor 에서 수동 구성
    //    3. AssetManifest: bt/companion/9001 → CompanionBT 등록
    // ====================================================================
    public static class CompanionAutomation
    {
        const string PlayerBtPath = "Assets/Data/BehaviorTrees/PlayerBT.asset";
        const string CompanionBt  = "Assets/Data/BehaviorTrees/CompanionBT.asset";
        const string ManifestPath = "Assets/Data/AssetManifest.asset";

        [MenuItem("MonsterKitchen/Companion/Run Asset Automation")]
        public static void Run()
        {
            Debug.Log("[CompanionAutomation] 시작");

            // 1. 데이터 동기화 (PLR_004/005)
            DataManagerWindow.SyncAllSO();

            // 2. CompanionBT 에셋 — PlayerBT 복제 (트리는 수동 구성 안내)
            if (AssetDatabase.LoadMainAssetAtPath(CompanionBt) == null)
            {
                if (AssetDatabase.CopyAsset(PlayerBtPath, CompanionBt))
                    Debug.Log($"[CompanionAutomation] CompanionBT 생성: {CompanionBt} — 루트를 Selector[Companion/Action/Engage, Companion/Action/Follow] 로 수동 구성 필요");
                else
                    Debug.LogError($"[CompanionAutomation] PlayerBT 복제 실패: {PlayerBtPath} → {CompanionBt}");
            }
            else
            {
                Debug.Log("[CompanionAutomation] CompanionBT 이미 존재 — 스킵");
            }

            // 3. AssetManifest 등록
            RegisterManifest("bt/companion/9001", CompanionBt);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[CompanionAutomation] 완료 ✓");
        }

        static void RegisterManifest(string key, string assetPath)
        {
            var manifest = AssetDatabase.LoadMainAssetAtPath(ManifestPath);
            var asset    = AssetDatabase.LoadMainAssetAtPath(assetPath);
            if (manifest == null || asset == null)
            {
                Debug.LogError($"[CompanionAutomation] 매니페스트/에셋 로드 실패: {key}");
                return;
            }

            var so      = new SerializedObject(manifest);
            var entries = so.FindProperty("m_Entries");
            for (int i = 0; i < entries.arraySize; i++)
            {
                if (entries.GetArrayElementAtIndex(i).FindPropertyRelative("Key").stringValue == key)
                {
                    Debug.Log($"[CompanionAutomation] 매니페스트 키 이미 존재 — 스킵: {key}");
                    return;
                }
            }

            int idx = entries.arraySize;
            entries.InsertArrayElementAtIndex(idx);
            var e = entries.GetArrayElementAtIndex(idx);
            e.FindPropertyRelative("Key").stringValue            = key;
            e.FindPropertyRelative("Asset").objectReferenceValue = asset;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(manifest);
            Debug.Log($"[CompanionAutomation] 매니페스트 등록: {key} → {asset.name}");
        }
    }
}
