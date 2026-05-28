using Cysharp.Text;

namespace MonsterKitchen.Core
{
    // ====================================================================
    //  StringUtil — 문자열 처리 유틸리티 래퍼
    //
    //  ▶ 목적
    //    ZString 을 직접 참조하는 대신 이 클래스를 경유한다.
    //    내부 구현을 교체할 때 호출부 수정 없이 이 파일만 변경하면 된다.
    //    (예: ZString → Unity 내장 / 서드파티 교체)
    //
    //  ▶ Format — 포맷 문자열 + 인자 → string 반환
    //    ZString.Format 은 value type boxing 없이 string 을 생성한다.
    //    $"..." 보다 GC Alloc 이 적어 빈번한 호출 경로에서 유리하다.
    //
    //  ▶ CreateBuilder — 비할당 문자열 빌더
    //    using var sb = StringUtil.CreateBuilder();
    //    sb.Append("prefix"); sb.AppendFormat("{0}", value);
    //    tmp.SetText(sb);        // TextMeshPro — zero alloc
    //    string s = sb.ToString(); // 최종 string 필요 시
    //
    //  ▶ 구현 교체 방법
    //    ZString → 다른 구현체로 교체 시:
    //    1. using Cysharp.Text; 를 변경
    //    2. ZString.Format / ZString.CreateStringBuilder 를 대응 API 로 교체
    //    3. 호출부(Format / CreateBuilder 사용처)는 수정 불필요
    // ====================================================================

    public static class StringUtil
    {
        // ── Format ────────────────────────────────────────────────────────

        /// <summary>인자 1개. value type boxing 없음.</summary>
        public static string Format<T0>(string format, T0 arg0)
            => ZString.Format(format, arg0);

        /// <summary>인자 2개. value type boxing 없음.</summary>
        public static string Format<T0, T1>(string format, T0 arg0, T1 arg1)
            => ZString.Format(format, arg0, arg1);

        /// <summary>인자 3개. value type boxing 없음.</summary>
        public static string Format<T0, T1, T2>(string format, T0 arg0, T1 arg1, T2 arg2)
            => ZString.Format(format, arg0, arg1, arg2);

        /// <summary>인자 4개. value type boxing 없음.</summary>
        public static string Format<T0, T1, T2, T3>(string format, T0 arg0, T1 arg1, T2 arg2, T3 arg3)
            => ZString.Format(format, arg0, arg1, arg2, arg3);

        // ── Builder ───────────────────────────────────────────────────────

        /// <summary>
        /// 비할당 문자열 빌더를 생성한다.
        /// using 패턴으로 사용해 반드시 Dispose 를 호출해야 한다.
        ///
        /// 사용 예:
        ///   using var sb = StringUtil.CreateBuilder();
        ///   sb.Append("Gold: ");
        ///   sb.Append(gold);
        ///   goldText.SetText(sb);   // TextMeshPro zero-alloc
        /// </summary>
        public static Utf16ValueStringBuilder CreateBuilder()
            => ZString.CreateStringBuilder();
    }
}
