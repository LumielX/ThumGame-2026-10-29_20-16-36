namespace SpiritMovers.Core.Save
{
    /// <summary>What happened when a save or settings file was read (the menu tells the player about the last three).</summary>
    public enum LoadOutcome
    {
        Loaded,
        Fresh,
        RestoredFromBackup,
        ResetCorrupt,
        NewerVersion,
        /// <summary>The file exists but could not be read or repaired right now (locked, no permission): nothing is written.</summary>
        Unreadable,
    }
}
