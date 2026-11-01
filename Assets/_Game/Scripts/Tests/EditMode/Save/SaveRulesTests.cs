using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using SpiritMovers.Core.Save;

namespace SpiritMovers.Tests
{
    public class SaveRulesTests
    {
        [Test]
        public void Credit_AddsAndIgnoresNonPositive()
        {
            var save = new SaveData();
            save.Credit(50);
            Assert.AreEqual(50, save.money);
            save.Credit(0);
            save.Credit(-10);
            Assert.AreEqual(50, save.money);
        }

        [Test]
        public void Credit_DoesNotOverflow()
        {
            var save = new SaveData { money = int.MaxValue - 5 };
            save.Credit(100);
            Assert.AreEqual(int.MaxValue, save.money);
        }

        [Test]
        public void SaveSanitized_FixesNegativeMoneyAndName()
        {
            var save = new SaveData { money = -3, playerName = " \u0007บี|ม  " }.Sanitized();
            Assert.AreEqual(0, save.money);
            Assert.AreEqual("บีม", save.playerName);
        }

        [Test]
        public void PlayerName_TrimsCollapsesAndFilters()
        {
            Assert.AreEqual("ป้า แจ่ม", PlayerName.Clean("  ป้า   แจ่ม  "));
            Assert.AreEqual("abc", PlayerName.Clean("a|b\nc"));
            Assert.AreEqual("ผู้ใหญ่", PlayerName.Clean("😀ผู้ใหญ่"));
            Assert.AreEqual("Mai_1.2-x", PlayerName.Clean("Mai_1.2-x"));
        }

        [Test]
        public void PlayerName_CapsLength()
        {
            Assert.AreEqual(PlayerName.MaxLength, PlayerName.Clean(new string('ก', 200)).Length);
            // Cutting at 20 would leave ท without its vowel and tone mark: the whole letter goes instead.
            var nineteen = new string('ก', 19);
            Assert.AreEqual(nineteen, PlayerName.Clean(nineteen + "ที่"));
            // A leading vowel (เ แ โ ใ ไ) never ends a name without the consonant it belongs to.
            Assert.AreEqual(nineteen, PlayerName.Clean(nineteen + "เก"));
        }

        [Test]
        public void PlayerName_EmptyStaysEmpty()
        {
            Assert.AreEqual("", PlayerName.Clean(""));
            Assert.AreEqual("", PlayerName.Clean("   "));
            Assert.AreEqual("", PlayerName.Clean(null));
            Assert.AreEqual("", PlayerName.Clean("😀"));
            Assert.AreEqual("", PlayerName.Clean("่้")); // tone marks with no letter
        }

        [Test]
        public void PlayerName_DefaultIsEmployeeNumber()
        {
            StringAssert.IsMatch(@"^พนักงาน \d{4}$", PlayerName.Default(1));
            StringAssert.IsMatch(@"^พนักงาน \d{4}$", PlayerName.Default(-77));
            Assert.AreEqual(PlayerName.Default(42), PlayerName.Default(42));
            Assert.AreEqual(PlayerName.Default(42), PlayerName.Clean(PlayerName.Default(42)));
        }

        [Test]
        public void SettingsSanitized_ClampsEverything()
        {
            var settings = new SettingsData
            {
                mouseSensitivity = 9f, fov = 10f, master = -1f, music = 2f, sfx = float.NaN, language = "fr",
            }.Sanitized();
            Assert.AreEqual(3f, settings.mouseSensitivity);
            Assert.AreEqual(60f, settings.fov);
            Assert.AreEqual(0f, settings.master);
            Assert.AreEqual(1f, settings.music);
            Assert.AreEqual(new SettingsData().sfx, settings.sfx);
            Assert.AreEqual("th", settings.language);
            Assert.AreEqual(0.2f, new SettingsData { mouseSensitivity = 0f }.Sanitized().mouseSensitivity);
            Assert.AreEqual(1f, new SettingsData { mouseSensitivity = float.NaN }.Sanitized().mouseSensitivity);
            Assert.AreEqual("en", new SettingsData { language = "en" }.Sanitized().language);
            Assert.AreEqual("", new SettingsData { bindingOverrides = null }.Sanitized().bindingOverrides);
        }

        [Test]
        public void Volume_ReadsAndWritesEachGroup()
        {
            var settings = new SettingsData();
            settings.SetVolume(AudioGroup.Music, 0.3f);
            Assert.AreEqual(0.3f, settings.Volume(AudioGroup.Music));
            foreach (var group in new[] { AudioGroup.Master, AudioGroup.Sfx, AudioGroup.Ambience, AudioGroup.Voice, AudioGroup.Ui })
                Assert.AreEqual(1f, settings.Volume(group), group.ToString());
            settings.SetVolume(AudioGroup.Ui, 4f);
            Assert.AreEqual(1f, settings.Volume(AudioGroup.Ui), "clamped");
        }

        [Test]
        public void Conflicts_FindsSamePathExceptThrowUse()
        {
            var conflicts = BindingRules.Conflicts(new[]
            {
                ("Jump", "<Keyboard>/space"), ("Ready", "<Keyboard>/space"),
                ("Throw", "<Mouse>/rightButton"), ("Use", "<Mouse>/rightButton"),
                ("Wai", "<Keyboard>/T"), ("Confirm", "<Keyboard>/t"),
                ("Crouch", ""), ("Sprint", ""),
            });
            CollectionAssert.AreEquivalent(new[] { ("Jump", "Ready"), ("Wai", "Confirm") }, conflicts.ToArray());
        }
    }
}
