#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace MonsterKitchen.Data.Pipeline
{
    /// <summary>
    /// CSV ParseResult → ScriptableObject 생성/갱신/삭제.
    /// 'id' 필드를 기준으로 SO를 식별한다.
    /// </summary>
    public static class ScriptableObjectSync
    {
        /// <summary>
        /// CSV rows를 읽어 SO를 생성/갱신한다.
        /// </summary>
        /// <typeparam name="T">대상 ScriptableObject 타입</typeparam>
        /// <param name="parseResult">CsvParser.ParseResult</param>
        /// <param name="outputFolder">Assets 상대 폴더 (e.g. "Assets/Data/Monsters")</param>
        /// <param name="fieldMapper">컬럼명 → SO 필드 매핑 함수 (null 이면 동일 이름 매핑)</param>
        /// <returns>생성/갱신된 SO 목록</returns>
        public static List<T> Sync<T>(CsvParser.ParseResult parseResult,
                                       string outputFolder,
                                       Action<T, Dictionary<string, string>> fieldMapper = null)
            where T : ScriptableObject
        {
            if (parseResult == null || parseResult.Headers == null)
            {
                Debug.LogError("[SOSync] ParseResult is null or has no headers.");
                return null;
            }

            if (!Directory.Exists(outputFolder))
                Directory.CreateDirectory(outputFolder);

            var synced = new List<T>();

            foreach (var row in parseResult.Rows)
            {
                if (!row.TryGetValue("id", out string id) || string.IsNullOrEmpty(id))
                {
                    Debug.LogWarning("[SOSync] Row has no 'id' — skipping.");
                    continue;
                }

                string assetPath = $"{outputFolder}/{id}.asset";
                T so = AssetDatabase.LoadAssetAtPath<T>(assetPath);

                if (so == null)
                {
                    so = ScriptableObject.CreateInstance<T>();
                    AssetDatabase.CreateAsset(so, assetPath);
                }

                Undo.RecordObject(so, $"Sync {id}");

                if (fieldMapper != null)
                {
                    fieldMapper(so, row);
                }
                else
                {
                    ApplyDefaultMapping(so, row);
                }

                EditorUtility.SetDirty(so);
                synced.Add(so);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[SOSync] Synced {synced.Count} {typeof(T).Name} assets to {outputFolder}");
            return synced;
        }

        /// <summary>헤더명과 동일한 public 필드에 string 값을 반영 (기본 타입만 지원)</summary>
        static void ApplyDefaultMapping<T>(T so, Dictionary<string, string> row) where T : ScriptableObject
        {
            Type type = so.GetType();
            foreach (var kv in row)
            {
                string colName = kv.Key.Trim();
                if (CsvParser.IsIgnoredColumn(colName)) continue;

                FieldInfo field = type.GetField(colName, BindingFlags.Public | BindingFlags.Instance);
                if (field == null) continue;

                SetFieldFromString(so, field, kv.Value);
            }
        }

        static void SetFieldFromString(object target, FieldInfo field, string value)
        {
            try
            {
                Type t = field.FieldType;
                if      (t == typeof(string))  field.SetValue(target, value);
                else if (t == typeof(int))     field.SetValue(target, int.TryParse(value, out int i) ? i : 0);
                else if (t == typeof(float))   field.SetValue(target, float.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float f) ? f : 0f);
                else if (t == typeof(bool))    field.SetValue(target, value.Trim().ToLower() is "true" or "1" or "yes");
                else if (t.IsEnum)             field.SetValue(target, Enum.TryParse(t, value.Trim(), true, out object e) ? e : Activator.CreateInstance(t));
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SOSync] Failed to set {field.Name} = '{value}': {ex.Message}");
            }
        }
    }
}
#endif
