// ====================================================================
//  Loc — Unity Localization 헬퍼
//
//  ▶ 테이블 클래스 프로퍼티 (동기):
//      [NonSerialized] LocalizedString m_NameLs;
//      public string DisplayName => Loc.Get(ref m_NameLs, NameKey);
//
//  ▶ UI 자가 갱신 (StringChanged):
//      m_Ls = Loc.Create("UI_INTERACT");
//      m_Ls.StringChanged += OnTextChanged;
// ====================================================================

using UnityEngine.Localization;

namespace MonsterKitchen.Core
{
    public static class Loc
    {
        /// <summary>String Table Collection 이름.</summary>
        public const string Table = "Strings";

        /// <summary>Strings 테이블 참조 LocalizedString 생성.</summary>
        public static LocalizedString Create(string key) => new(Table, key);

        /// <summary>
        /// 캐시 생성 + 동기 조회. 테이블 클래스 프로퍼티용.
        /// key 비어 있음 → string.Empty. 미등록 키 → 키 자체 반환
        /// (LocalizationSettings.NoTranslationFoundMessage = "{key}").
        /// </summary>
        public static string Get(ref LocalizedString cache, string key)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;
            cache ??= Create(key);
            return cache.GetLocalizedString();
        }
    }
}
