using NUnit.Framework;
using SpiritMovers.Editor;
using UnityEditor.Localization;
using UnityEngine.Localization.Tables;

namespace SpiritMovers.Tests
{
    public class LocalizationTableTests
    {
        [Test]
        public void UiTable_HasThaiAndEnglishTextForEveryKey()
        {
            var collection = LocalizationEditorSettings.GetStringTableCollection(LocalizationSetup.UiTable);
            Assert.IsNotNull(collection, "Run Spirit Movers/Localization/Create Settings and UI Table");
            Assert.IsNotEmpty(collection.SharedData.Entries);

            foreach (var code in LocalizationSetup.LocaleCodes)
            {
                var table = (StringTable)collection.GetTable(code);
                Assert.IsNotNull(table, $"missing {code} table");
                foreach (var shared in collection.SharedData.Entries)
                    Assert.IsFalse(string.IsNullOrWhiteSpace(table.GetEntry(shared.Id)?.Value),
                        $"'{shared.Key}' has no {code} text");
            }
        }
    }
}
