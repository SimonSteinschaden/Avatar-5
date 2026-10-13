namespace Avatar5.Contracts
{
    /// <summary>Breaking protocol/content changes must be explicitly versioned.</summary>
    public static class VersionContracts
    {
        public const int NetworkProtocolVersion = 1;
        public const int SaveSchemaVersion = 1;
        public const int ContentSchemaVersion = 1;

        public static RequestStatus CheckNetworkProtocol(int remoteProtocolVersion)
        {
            return remoteProtocolVersion == NetworkProtocolVersion
                ? RequestStatus.Accepted
                : RequestStatus.RejectedVersion;
        }
    }
}
