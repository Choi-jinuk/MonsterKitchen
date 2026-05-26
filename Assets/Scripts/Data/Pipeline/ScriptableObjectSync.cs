#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace MonsterKitchen.Data.Pipeline
{
    /// <summary>
    /// CSV ParseResult → TableData 딕셔너리 항목 생성/갱신.
    /// 개별 .asset 파일 대신 TableData 내 SerializedDictionary 에 직접 기록한다.
    /// </summary>
    public static class ScriptableObjectSync
    {
        /// <summary>
        /// CSV rows 를 읽어 DataTable 항목을 생성/갱신한다.
        /// id 컬럼 값이 key, T 인스턴스가 value.
        /// </summary>
        /// <typeparam name="T">데이터 클래스 (new() 제약 필요)</typeparam>
        /// <param name="parseResult">CsvParser.ParseResult</param>
        /// <param name="table">대상 DataTable</param>
        /// <param name="tableData">dirty 마킹용 SO (TableData)</param>
        /// <param name="fieldMapper">컬럼명 → 필드 커스텀 매핑 (null 이면 기본 동일명 매핑)</param>
        /// <returns>동기화된 항목 수</returns>
        public static int Sync<T>(
            CsvParser.ParseResult parseResult,
            DataTable<T> table,
            UnityEngine.Object tableData,
            Action<T, Dictionary<string, string>> fieldMapper = null)
            where T : class, new()
        {
            if (parseResult?.Rows == null)
            {
                Debug.LogError("[SOSync] ParseResult is null or has no rows.");
                return 0;
            }

            Undo.RecordObject(tableData, $"Sync {typeof(T).Name}");

            var dict   = table.Dict;
            int synced = 0;

            foreach (var row in parseResult.Rows)
            {
                if (!row.TryGetValue("id", out string idStr) || !uint.TryParse(idStr, out uint id))
                {
                    Debug.LogWarning($"[SOSync] Row에 유효한 uint 'id' 가 없습니다 (값: '{(row.TryGetValue("id", out var v) ? v : "없음")}') — 건너뜁니다.");
                    continue;
                }

                if (!dict.TryGetValue(id, out T entry) || entry == null)
                    entry = new T();

                // 기본 매핑 먼저(primitive/enum/string), 그 다음 커스텀 매퍼(복합 타입·자동 생성 필드)
                ApplyDefaultMapping(entry, row);
                fieldMapper?.Invoke(entry, row);

                dict[id] = entry;
                synced++;
            }

            EditorUtility.SetDirty(tableData);
            return synced;
        }

        /// <summary>헤더명과 동일한 public 필드에 string 값을 반영 (기본 타입만 지원)</summary>
        static void ApplyDefaultMapping<T>(T target, Dictionary<string, string> row) where T : class
        {
            Type type = target.GetType();
            foreach (var kv in row)
            {
                string colName = kv.Key.Trim();
                if (CsvParser.IsIgnoredColumn(colName)) continue;

                FieldInfo field = type.GetField(colName, BindingFlags.Public | BindingFlags.Instance);
                if (field == null) continue;

                SetFieldFromString(target, field, kv.Value);
            }
        }

        static void SetFieldFromString(object target, FieldInfo field, string value)
        {
            try
            {
                Type t = field.FieldType;
                if      (t == typeof(string))  field.SetValue(target, value);
                else if (t == typeof(int))     field.SetValue(target, int.TryParse(value, out int i) ? i : 0);
                else if (t == typeof(uint))    field.SetValue(target, uint.TryParse(value, out uint u) ? u : 0u);
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
