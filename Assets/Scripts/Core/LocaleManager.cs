using MonsterKitchen.Core;
// ====================================================================
//  LocaleManager — 런타임 로컬라이제이션 매니저
//
//  ▶ 순수 C# 클래스 (MonoBehaviour 아님)
//  ▶ GlobalController 에서 생성·초기화
//
//  ▶ 사용법
//    string name = LocaleManager.Get("MON_001_NAME");
//    LocaleManager.Instance.SetLanguage(GameLanguage.En);
//
//  ▶ 캐시 소유
//    StringId → StringData 캐시는 StringTable.RuntimeSetData() 가 소유.
//    LocaleManager 는 DataRegistry.Strings.FindByStringId() 로 조회한다.
//
//  ▶ 새 언어 추가
//    1. GameLanguage 열거 값 추가
//    2. StringData.cs 에 언어 필드 추가 (예: Ja)
//    3. GetText() switch 케이스 추가
//    4. StringData.csv 에 열 추가 후 Sync SO
//
//  ▶ Fallback 체계
//    요청 언어 → Ko → StringId 자체 (키 미등록 시)
// ====================================================================

using System;
using MonsterKitchen.Data;
using UnityEngine;

namespace MonsterKitchen.Core
{
    public enum GameLanguage
    {
        Ko,    // 한국어 (기본)
        En,    // English
        // Ja,    // 日本語
        // ZhCn,  // 中文(简)
    }

    public class LocaleManager
    {
        public static LocaleManager Instance { get; private set; }

        // ── 상태 ─────────────────────────────────────────────────────
        GameLanguage m_Language = GameLanguage.Ko;
        bool         m_Ready;

        public GameLanguage Language => m_Language;
        public bool         IsReady  => m_Ready;

        /// <summary>언어 변경 시 발생. UIPanel 이 구독해 LocalizedLabel 을 갱신한다.</summary>
        public static event Action OnLanguageChanged;

        // ── 초기화 ────────────────────────────────────────────────────

        public void Init() => Instance = this;

        /// <summary>
        /// DataRegistry.Load() — StringTable.RuntimeSetData() 완료 후 호출.
        /// StringTable 이 캐시를 소유하므로 LocaleManager 는 Ready 플래그만 설정.
        /// </summary>
        public void OnDataLoaded()
        {
            var strings = DataRegistry.Instance?.Table?.Strings;
            if (strings == null)
            {
                DebugUtil.LogWarning("[LocaleManager] StringTable 없음 — TableData Sync SO 확인.");
                return;
            }

            m_Ready = true;
            DebugUtil.Log(StringUtil.Format("[LocaleManager] 준비 완료 ({0}개 문자열, 언어: {1})",
                strings.Count, m_Language));
            OnLanguageChanged?.Invoke(); // 데이터 로드 완료 → 대기 중인 UI 갱신
        }

        // ── 언어 변경 ─────────────────────────────────────────────────

        /// <summary>현재 언어를 변경한다. 즉시 적용 — UI 갱신은 OnLanguageChanged 이벤트 수신자 책임.</summary>
        public void SetLanguage(GameLanguage language)
        {
            m_Language = language;
            DebugUtil.Log($"[LocaleManager] 언어 변경: {language}");
            OnLanguageChanged?.Invoke();
        }

        // ── 정적 접근자 ───────────────────────────────────────────────

        /// <summary>
        /// stringId 에 해당하는 현재 언어 텍스트를 반환한다.
        /// - Instance 미초기화 또는 데이터 미로드 → stringId 자체 반환 (빈 문자열 방지)
        /// - 해당 언어 필드 비어 있음 → Ko 반환
        /// </summary>
        public static string Get(string stringId)
        {
            if (string.IsNullOrEmpty(stringId)) return string.Empty;
            if (Instance == null || !Instance.m_Ready) return stringId;
            return Instance.GetText(stringId);
        }

        // ── 내부 ──────────────────────────────────────────────────────

        string GetText(string stringId)
        {
            var sd = DataRegistry.Instance?.Strings?.FindByStringId(stringId);
            if (sd == null) return stringId; // 등록 안 된 키 → 키 자체를 fallback

            string text = m_Language switch
            {
                GameLanguage.Ko => sd.Ko,
                GameLanguage.En => string.IsNullOrEmpty(sd.En) ? sd.Ko : sd.En,
                // GameLanguage.Ja => string.IsNullOrEmpty(sd.Ja) ? sd.Ko : sd.Ja,
                _               => sd.Ko,
            };

            return string.IsNullOrEmpty(text) ? stringId : text;
        }
    }
}
