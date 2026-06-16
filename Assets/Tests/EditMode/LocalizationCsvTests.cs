// ====================================================================
//  LocalizationCsvTests — StringData.csv (Unity Localization CSV 포맷) 무결성
// ====================================================================

using System.IO;
using System.Linq;
using NUnit.Framework;

namespace MonsterKitchen.Tests
{
    public class LocalizationCsvTests
    {
        const string CsvPath = "Assets/Data/CSV/StringData.csv";

        [Test]
        public void Csv_HeaderMatchesPackageFormat()
        {
            string header = File.ReadLines(CsvPath).First();
            Assert.AreEqual("Key,Id,Shared Comments,Korean(ko),English(en)", header);
        }

        [Test]
        public void Csv_KeysAreUnique()
        {
            var keys = File.ReadLines(CsvPath).Skip(1)
                           .Where(l => !string.IsNullOrWhiteSpace(l))
                           .Select(l => l.Split(',')[0])
                           .ToList();
            Assert.Greater(keys.Count, 0);
            CollectionAssert.AllItemsAreUnique(keys);
        }

        [Test]
        public void Csv_EveryRowHasKoreanText()
        {
            var rows = File.ReadLines(CsvPath).Skip(1)
                           .Where(l => !string.IsNullOrWhiteSpace(l));
            foreach (var row in rows)
            {
                var f = System.Text.RegularExpressions.Regex.Split(
                    row, ",(?=(?:[^\"]*\"[^\"]*\")*[^\"]*$)");
                Assert.IsFalse(string.IsNullOrEmpty(f[3]),
                    $"Ko 비어 있음: {f[0]}");
            }
        }
    }
}
