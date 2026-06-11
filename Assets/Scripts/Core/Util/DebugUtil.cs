using System.Diagnostics;
using UnityEngine;

namespace MonsterKitchen.Core
{
    // ====================================================================
    //  DebugUtil — 빌드 조건부 로그 래퍼
    //
    //  [Conditional] 속성으로 릴리즈 빌드 시 호출부 자체가 제거된다.
    //  (메서드 본문이 아닌 호출부가 IL에서 삭제 → 런타임 오버헤드 0)
    //
    //  활성 조건
    //    UNITY_EDITOR      — Editor 실행 (Play Mode 포함)
    //    DEVELOPMENT_BUILD — Unity Build Settings > Development Build 체크
    //
    //  릴리즈 빌드(Development Build 미체크) → 모든 호출 제거
    // ====================================================================

    public static class DebugUtil
    {
        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void Log(string msg)
            => UnityEngine.Debug.Log(msg);

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void Log(string msg, Object context)
            => UnityEngine.Debug.Log(msg, context);

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void LogWarning(string msg)
            => UnityEngine.Debug.LogWarning(msg);

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void LogWarning(string msg, Object context)
            => UnityEngine.Debug.LogWarning(msg, context);

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void LogError(string msg)
            => UnityEngine.Debug.LogError(msg);

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void LogError(string msg, Object context)
            => UnityEngine.Debug.LogError(msg, context);
    }
}
