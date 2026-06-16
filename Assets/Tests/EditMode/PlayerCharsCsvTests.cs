// ====================================================================
//  PlayerCharsCsvTests — Players.csv (캐릭터 시스템 기반 컬럼) 무결성
// ====================================================================

using System.IO;
using System.Linq;
using NUnit.Framework;
using MonsterKitchen.Data;

namespace MonsterKitchen.Tests
{
    public class PlayerCharsCsvTests
    {
        const string CsvPath = "Assets/Data/CSV/Players.csv";

        static string[] Header() =>
            File.ReadLines(CsvPath)
                .First(l => !string.IsNullOrWhiteSpace(l)
                         && !l.StartsWith(";") && !l.StartsWith("#TYPE"))
                .Split(',');

        static string[][] DataRows() =>
            File.ReadLines(CsvPath)
                .Where(l => !string.IsNullOrWhiteSpace(l)
                         && !l.StartsWith(";") && !l.StartsWith("#TYPE"))
                .Skip(1)
                .Select(l => l.Split(','))
                .ToArray();

        static int Col(string name)
        {
            int idx = System.Array.IndexOf(Header(), name);
            Assert.GreaterOrEqual(idx, 0, $"컬럼 없음: {name}");
            return idx;
        }

        [Test]
        public void NatalStars_InRange1To3()
        {
            int col = Col("NatalStars");
            foreach (var row in DataRows())
            {
                int stars = int.Parse(row[col]);
                Assert.That(stars, Is.InRange(1, 3), $"{row[0]}: NatalStars={stars}");
            }
        }

        [Test]
        public void CharClass_IsValidWeaponType()
        {
            int col = Col("CharClass");
            foreach (var row in DataRows())
                Assert.IsTrue(System.Enum.TryParse<WeaponType>(row[col], out _),
                    $"{row[0]}: CharClass='{row[col]}' 무효");
        }

        [Test]
        public void WorkSpeeds_ArePositive()
        {
            int cook  = Col("CookSpeed");
            int serve = Col("ServeSpeed");
            foreach (var row in DataRows())
            {
                Assert.Greater(float.Parse(row[cook],
                    System.Globalization.CultureInfo.InvariantCulture), 0f, $"{row[0]} CookSpeed");
                Assert.Greater(float.Parse(row[serve],
                    System.Globalization.CultureInfo.InvariantCulture), 0f, $"{row[0]} ServeSpeed");
            }
        }
    }
}
