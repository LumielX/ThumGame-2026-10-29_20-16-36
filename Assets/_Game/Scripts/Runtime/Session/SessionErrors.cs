using System;
using SpiritMovers.Core.Session;
using SpiritMovers.Net;

namespace SpiritMovers.Session
{
    /// <summary>Turns session failures into the localization keys the menu shows (table "UI").</summary>
    public static class SessionErrors
    {
        public const string Version = "error.version";
        public const string Code = "error.code";
        public const string Join = "error.join";
        public const string InJob = "error.in_job";
        public const string Full = "error.full";

        public static string KeyFor(Exception e) => e is VersionMismatchException ? Version : UgsSessionService.FailureKey(e) ?? Join;

        /// <summary>Values for the message's {0}, {1}: both versions for a mismatch, nothing otherwise.</summary>
        public static object[] ArgsFor(Exception e) =>
            e is VersionMismatchException mismatch ? new object[] { mismatch.LocalVersion.ToString(), mismatch.HostVersion } : Array.Empty<object>();

        public static string KeyFor(JoinRefusal refusal) => refusal switch
        {
            JoinRefusal.VersionMismatch => Version,
            JoinRefusal.JobInProgress => InJob,
            JoinRefusal.Full => Full,
            _ => null,
        };
    }
}
