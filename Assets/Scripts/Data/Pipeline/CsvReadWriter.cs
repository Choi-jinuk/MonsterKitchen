using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace MonsterKitchen.Data.Pipeline
{
    /// <summary>
    /// CSV 파일 읽기/쓰기. 주석·무시컬럼·타입힌트 행을 보존한다.
    /// </summary>
    public static class CsvReadWriter
    {
        /// <summary>Assets 상대 경로로 CSV 읽기 (e.g. "Data/Monsters.csv")</summary>
        public static CsvParser.ParseResult ReadFromAssets(string assetRelativePath)
        {
            string fullPath = Path.Combine(Application.dataPath, assetRelativePath);
            return ReadFromFullPath(fullPath);
        }

        public static CsvParser.ParseResult ReadFromFullPath(string fullPath)
        {
            if (!File.Exists(fullPath))
            {
                Debug.LogWarning($"[CsvReadWriter] File not found: {fullPath}");
                return null;
            }
            string text = File.ReadAllText(fullPath, Encoding.UTF8);
            return CsvParser.Parse(text);
        }

        /// <summary>
        /// 파싱 결과를 CSV 파일로 저장.
        /// 원본의 주석 행·타입힌트 행 순서를 최대한 유지한다.
        /// </summary>
        public static void Write(string fullPath, string[] headers, string[] typeHints,
                                 List<Dictionary<string, string>> rows, List<string> originalRawLines = null)
        {
            var sb = new StringBuilder();

            // 헤더 앞 주석을 원본에서 재구성
            if (originalRawLines != null)
            {
                foreach (var raw in originalRawLines)
                {
                    string trimmed = raw.TrimEnd();
                    if (string.IsNullOrWhiteSpace(trimmed)) continue;
                    if (trimmed.StartsWith(CsvParser.CommentChar.ToString()))
                        sb.AppendLine(trimmed);
                    else
                        break;  // 헤더(또는 타입힌트) 도달 → 중단
                }
            }

            // 타입 힌트 행
            if (typeHints != null)
                sb.AppendLine(JoinLine(typeHints));

            // 헤더 행
            sb.AppendLine(JoinLine(headers));

            // 데이터 행
            foreach (var row in rows)
            {
                var fields = new string[headers.Length];
                for (int i = 0; i < headers.Length; i++)
                {
                    string h = headers[i];
                    fields[i] = row.TryGetValue(h, out var v) ? v ?? string.Empty : string.Empty;
                }
                sb.AppendLine(JoinLine(fields));
            }

            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllText(fullPath, sb.ToString(), Encoding.UTF8);
        }

        // ---- 헬퍼 ----

        static string JoinLine(string[] fields)
        {
            var parts = new string[fields.Length];
            for (int i = 0; i < fields.Length; i++)
                parts[i] = EscapeField(fields[i]);
            return string.Join(",", parts);
        }

        static string EscapeField(string value)
        {
            if (value == null) return string.Empty;
            if (value.Contains(",") || value.Contains("\"") || value.Contains("\n"))
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            return value;
        }
    }
}
