namespace SpiritMovers.Interaction
{
    /// <summary>Anything a player can aim at and use: items to carry now; bells, closets, ghosts later (TECHNICAL_DESIGN 6).</summary>
    public interface IInteractable
    {
        /// <summary>Localization key of the hint shown while aiming at it (table "UI").</summary>
        string PromptKey { get; }

        bool CanInteract(PlayerCarrier by);

        /// <summary>Called on the interacting player's own machine; the host validates through an RPC.</summary>
        void Interact(PlayerCarrier by);
    }

    /// <summary>A held tool with an action of its own (right mouse button), e.g. the spirit camera.</summary>
    public interface IUsable
    {
        /// <summary>Localization key of the hint shown while holding it.</summary>
        string UsePromptKey { get; }

        /// <summary>Called on the holder's own machine; the host validates through an RPC.</summary>
        void Use(PlayerCarrier by);
    }
}
