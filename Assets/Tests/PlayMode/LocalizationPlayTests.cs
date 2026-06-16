// ====================================================================
//  LocalizationPlayTests — 로케일 전환 / 미등록 키 / En→Ko 폴백 검증
// ====================================================================

using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.TestTools;

namespace MonsterKitchen.Tests.PlayMode
{
    public class LocalizationPlayTests
    {
        Locale m_Original;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return LocalizationSettings.InitializationOperation;
            m_Original = LocalizationSettings.SelectedLocale;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            LocalizationSettings.SelectedLocale = m_Original;
            yield return null;
        }

        static Locale FindLocale(string code) =>
            LocalizationSettings.AvailableLocales.Locales
                .First(l => l.Identifier.Code == code);

        [UnityTest]
        public IEnumerator LocaleSwitch_ChangesString()
        {
            var ls = new LocalizedString("Strings", "MON_001_NAME");

            LocalizationSettings.SelectedLocale = FindLocale("ko");
            yield return null;
            Assert.AreEqual("초록 슬라임", ls.GetLocalizedString());

            LocalizationSettings.SelectedLocale = FindLocale("en");
            yield return null;
            Assert.AreEqual("Green Slime", ls.GetLocalizedString());
        }

        [UnityTest]
        public IEnumerator MissingKey_ReturnsKeyItself()
        {
            yield return null;
            var ls = new LocalizedString("Strings", "NOT_A_REAL_KEY_999");
            Assert.AreEqual("NOT_A_REAL_KEY_999", ls.GetLocalizedString());
        }

        [UnityTest]
        public IEnumerator EmptyEnglish_FallsBackToKorean()
        {
            // 전제: En 빈 키 최소 1개 존재. CSV 에서 En 빈 첫 키를 찾아 검증.
            string key = System.IO.File.ReadLines("Assets/Data/CSV/StringData.csv")
                .Skip(1).Where(l => !string.IsNullOrWhiteSpace(l))
                .Select(l => System.Text.RegularExpressions.Regex.Split(
                    l, ",(?=(?:[^\"]*\"[^\"]*\")*[^\"]*$)"))
                .Where(f => f.Length > 4 && string.IsNullOrEmpty(f[4]))
                .Select(f => f[0]).FirstOrDefault();

            if (key == null)
                Assert.Ignore("En 빈 키 없음 — 폴백 케이스 데이터 없음");

            LocalizationSettings.SelectedLocale = FindLocale("en");
            yield return null;

            var ls = new LocalizedString("Strings", key);
            string result = ls.GetLocalizedString();
            Assert.IsFalse(string.IsNullOrEmpty(result), "폴백 실패 — 빈 문자열");
            Assert.AreNotEqual(key, result, "폴백 실패 — 키 자체 반환");
        }
    }
}
