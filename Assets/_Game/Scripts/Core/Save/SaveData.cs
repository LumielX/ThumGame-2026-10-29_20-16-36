using System;

namespace SpiritMovers.Core.Save
{
    /// <summary>
    /// What a player keeps between games, on their own machine (TECHNICAL_DESIGN 9, GDD 12): the money earned and the
    /// name they chose. Public fields so JsonUtility can write it; no encryption or anti-cheat (MASTER_PROMPT 0).
    /// </summary>
    [Serializable]
    public sealed class SaveData
    {
        public const int CurrentSchema = 1;

        public int schema = CurrentSchema;
        public int money;
        public string playerName = "";

        /// <summary>Add pay; nothing for zero or less, and the total stops at int.MaxValue.</summary>
        public void Credit(int amount)
        {
            if (amount <= 0)
                return;
            money = (int)Math.Min(int.MaxValue, (long)money + amount);
        }

        /// <summary>A copy with values a hand-edited or damaged file could break put back in range.</summary>
        public SaveData Sanitized() => new()
        {
            schema = schema,
            money = Math.Max(0, money),
            playerName = PlayerName.Clean(playerName),
        };
    }
}
