using SpiritMovers.Core;
using UnityEngine;

namespace SpiritMovers
{
    /// <summary>
    /// First object in the Boot scene. Reports the build version so logs from
    /// playtests can be matched to a release (TECHNICAL_DESIGN 15).
    /// </summary>
    public sealed class Bootstrap : MonoBehaviour
    {
        public static GameVersion Version { get; private set; }

        private void Awake()
        {
            if (!GameVersion.TryParse(Application.version, out var version))
            {
                Debug.LogError($"[Boot] Invalid application version '{Application.version}'. Expected major.minor.patch.");
                return;
            }

            Version = version;
            Debug.Log($"[Boot] {Application.productName} v{Version} ({Application.platform})");
        }
    }
}
