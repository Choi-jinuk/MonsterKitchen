// ====================================================================
//  LocalizationSetupTool — Unity Localization 1회성 셋업 + CSV 재임포트
//
//  ▶ MonsterKitchen/Localization/Setup       : 전체 셋업 (멱등 — 재실행 안전)
//  ▶ MonsterKitchen/Localization/Import CSV  : StringData.csv 재임포트만
//
//  ▶ 운영 워크플로
//    Excel 에서 Assets/Data/CSV/StringData.csv 편집
//    → MonsterKitchen/Localization/Import CSV
// ====================================================================

using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Localization;
using UnityEditor.Localization.Plugins.CSV;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Metadata;
using UnityEngine.Localization.Settings;

namespace MonsterKitchen.EditorTools
{
    public static class LocalizationSetupTool
    {
        const string RootDir        = "Assets/Localization";
        const string LocaleDir      = RootDir + "/Locales";
        const string TableDir       = RootDir + "/Strings";
        const string SettingsPath   = RootDir + "/LocalizationSettings.asset";
        const string CollectionName = "Strings";
        const string CsvPath        = "Assets/Data/CSV/StringData.csv";

        [MenuItem("MonsterKitchen/Localization/Setup")]
        public static void Setup()
        {
            Directory.CreateDirectory(LocaleDir);
            Directory.CreateDirectory(TableDir);

            // 1) Locale 에셋 (ko 기본, en)
            var ko = GetOrCreateLocale("ko");
            var en = GetOrCreateLocale("en");

            // en → ko 폴백
            var fallback = en.Metadata.GetMetadata<FallbackLocale>();
            if (fallback == null) en.Metadata.AddMetadata(new FallbackLocale(ko));
            else                  fallback.Locale = ko;
            EditorUtility.SetDirty(en);

            // 2) LocalizationSettings
            var settings = AssetDatabase.LoadAssetAtPath<LocalizationSettings>(SettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<LocalizationSettings>();
                settings.name = "LocalizationSettings";
                AssetDatabase.CreateAsset(settings, SettingsPath);
            }
            LocalizationEditorSettings.ActiveLocalizationSettings = settings;

            // 선택 체인: PlayerPrefs → System → ko
            var selectors = settings.GetStartupLocaleSelectors();
            selectors.Clear();
            selectors.Add(new PlayerPrefLocaleSelector());
            selectors.Add(new SystemLocaleSelector());
            selectors.Add(new SpecificLocaleSelector { LocaleId = ko.Identifier });

            // 미등록 키 → 키 자체 표시 (기존 LocaleManager fallback 동작 보존)
            var db = settings.GetStringDatabase();
            db.NoTranslationFoundMessage = "{key}";
            EditorUtility.SetDirty(settings);

            // 3) String Table Collection
            var collection = LocalizationEditorSettings.GetStringTableCollection(CollectionName)
                          ?? LocalizationEditorSettings.CreateStringTableCollection(CollectionName, TableDir);

            // 프리로드 — GameStartup 동기 접근 보장
            foreach (var table in collection.StringTables)
                LocalizationEditorSettings.SetPreloadTableFlag(table, true);

            // 4) CSV 임포트
            ImportCsv();

            AssetDatabase.SaveAssets();
            Debug.Log("[LocalizationSetupTool] Setup 완료");
        }

        [MenuItem("MonsterKitchen/Localization/Import CSV")]
        public static void ImportCsv()
        {
            var collection = LocalizationEditorSettings.GetStringTableCollection(CollectionName);
            if (collection == null)
            {
                Debug.LogError("[LocalizationSetupTool] Strings 컬렉션 없음 — Setup 먼저 실행");
                return;
            }

            using (var reader = new StreamReader(CsvPath, System.Text.Encoding.UTF8))
                Csv.ImportInto(reader, collection);

            // 빈 En 셀 → 엔트리 제거 (폴백이 ko 로 동작하도록)
            var enTable = collection.StringTables.FirstOrDefault(
                t => t.LocaleIdentifier.Code == "en");
            if (enTable != null)
            {
                var empty = enTable.Values.Where(e => string.IsNullOrEmpty(e.Value))
                                          .Select(e => e.KeyId).ToList();
                foreach (var keyId in empty) enTable.Remove(keyId);
                EditorUtility.SetDirty(enTable);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[LocalizationSetupTool] CSV 임포트 완료 — {collection.SharedData.Entries.Count}개 키");
        }

        static Locale GetOrCreateLocale(string code)
        {
            string path = $"{LocaleDir}/{code}.asset";
            var locale = AssetDatabase.LoadAssetAtPath<Locale>(path);
            if (locale == null)
            {
                locale = Locale.CreateLocale(new LocaleIdentifier(code));
                AssetDatabase.CreateAsset(locale, path);
                LocalizationEditorSettings.AddLocale(locale);
            }
            return locale;
        }
    }
}
