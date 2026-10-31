using System.Runtime.CompilerServices;

// Test-only hooks (e.g. Carryable.FollowEnabled) are internal, not part of the game API.
[assembly: InternalsVisibleTo("SpiritMovers.Tests.PlayMode")]
// The dev bots (Development builds only) drive the menus through the same hooks the tests use.
[assembly: InternalsVisibleTo("SpiritMovers.Dev")]
