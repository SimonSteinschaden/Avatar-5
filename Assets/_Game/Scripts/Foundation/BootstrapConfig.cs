using Avatar5.Contracts;
using UnityEngine;

namespace Avatar5.Foundation
{
    [CreateAssetMenu(menuName = "Avatar5/Bootstrap Configuration", fileName = "BootstrapConfig")]
    public sealed class BootstrapConfig : ScriptableObject
    {
        [SerializeField] private string buildVersion = "0.0.1-dev";
        [SerializeField] private int protocolVersion = VersionContracts.NetworkProtocolVersion;
        [SerializeField] private int contentSchemaVersion = VersionContracts.ContentSchemaVersion;

        public string BuildVersion { get { return buildVersion; } }
        public int ProtocolVersion { get { return protocolVersion; } }
        public int ContentSchemaVersion { get { return contentSchemaVersion; } }

        public bool IsCurrentProtocol { get { return protocolVersion == VersionContracts.NetworkProtocolVersion; } }
    }
}
