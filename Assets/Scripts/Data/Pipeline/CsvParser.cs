using System;
using System.Collections.Generic;
using System.Text;

namespace MonsterKitchen.Data.Pipeline
{
    /// <summary>
    /// CSV 파싱 유틸리티.
    /// 포맷 규칙:
    ///   - ';' 로 시작하는 행 = 주석 (무시)
    ///   - 첫 번째 비주석 행 = 헤더
    ///   - '#TYPE' 행 = 타입 힌트 (파싱 후 TypeHints 딕셔너리에 저장)
    ///   - '_' 접두사 컬럼 = 무시 컬럼 (데이터 보존, 파싱 건너뜀)
    ///   - 값 내 배열 = '|' 구분자
    /// </summary>
    public static class CsvParser
    {
        public const char CommentChar    = ';';
        public const char IgnorePrefix   = '_';
        public const char ArraySeparator = '|';
        public const string TypeHintTag  = "#TYPE";

        public class ParseResult
        {
            public string[]                    Headers   { get; set; }
            public string[]                    TypeHints { get; set; }    // null if not present
            public List<Dictionary<string, string>> Rows { get; set; }
            public List<string>                RawLines  { get; set; }    // 원본 줄 보존
        }

        public static ParseResult Parse(string csvText)
        {
            var result = new ParseResult
            {
                Rows     = new List<Dictionary<string, string>>(),
                RawLines = new List<string>(),
            };

            var lines = csvText.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');

            string[] headers   = null;
            string[] typeHints = null;

            foreach (var rawLine in lines)
            {
                result.RawLines.Add(rawLine);
                var line = rawLine.TrimEnd();

                // 빈 줄
                if (string.IsNullOrWhiteSpace(line)) continue;

                // 주석 행
                if (line.StartsWith(CommentChar.ToString())) continue;

                var fields = SplitLine(line);

                // 타입 힌트 행
                if (fields.Length > 0 && fields[0].Trim() == TypeHintTag)
                {
                    typeHints = fields;
                    continue;
                }

                // 헤더 행
                if (headers == null)
                {
                    headers = fields;
                    result.Headers   = headers;
                    result.TypeHints = typeHints;
                    continue;
                }

                // 데이터 행
                var row = new Dictionary<string, string>(StringComparer.Ordinal);
                for (int i = 0; i < headers.Length; i++)
                {
                    string header = headers[i].Trim();
                    string value  = i < fields.Length ? fields[i] : string.Empty;

                    // '_' 접두사 무시 컬럼은 키에 그대로 보존 (값도 저장하되 런타임 조회 시 건너뜀)
                    row[header] = value;
                }
                result.Rows.Add(row);
            }

            return result;
        }

        /// <summary>특정 컬럼이 무시 컬럼인지 확인</summary>
        public static bool IsIgnoredColumn(string header)
            => !string.IsNullOrEmpty(header) && header.TrimStart().StartsWith(IgnorePrefix.ToString());

        /// <summary>값을 '|' 구분 배열로 분리</summary>
        public static string[] SplitArray(string value)
        {
            if (string.IsNullOrEmpty(value)) return Array.Empty<string>();
            return value.Split(ArraySeparator);
        }

        // ---- 내부 헬퍼 ----

        static string[] SplitLine(string line)
        {
            var fields = new List<string>();
            var sb     = new StringBuilder();
            bool inQuote = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (c == '"')
                {
                    if (inQuote && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        sb.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuote = !inQuote;
                    }
                }
                else if (c == ',' && !inQuote)
                {
                    fields.Add(sb.ToString());
                    sb.Clear();
                }
                else
                {
                    sb.Append(c);
                }
            }
            fields.Add(sb.ToString());
            return fields.ToArray();
        }
    }
}
