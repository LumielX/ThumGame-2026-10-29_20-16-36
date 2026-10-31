using System.Runtime.CompilerServices;

// Test-only hooks (e.g. Carryable.FollowEnabled) are internal, not part of the game API.
[assembly: InternalsVisibleTo("SpiritMovers.Tests.PlayMode")]
