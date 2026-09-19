using System;

namespace LH.Main.Unity.Server
{
    public sealed class GameServerConfig
    {
        public const string ServerIdVariable = "GAME_SERVER_ID";
        public const string HttpPortVariable = "GAME_SERVER_HTTP_PORT";
        public const string NetworkPortVariable = "GAME_SERVER_NETWORK_PORT";
        public const string PublicHostVariable = "GAME_SERVER_PUBLIC_HOST";
        public const string PublicNetworkPortVariable = "GAME_SERVER_PUBLIC_NETWORK_PORT";

        public string ServerId { get; }
        public ushort HttpPort { get; }
        public ushort NetworkPort { get; }
        public string PublicHost { get; }
        public ushort PublicNetworkPort { get; }

        public GameServerConfig(string serverId, ushort httpPort, ushort networkPort, string publicHost, ushort publicNetworkPort)
        {
            ServerId = serverId;
            HttpPort = httpPort;
            NetworkPort = networkPort;
            PublicHost = publicHost;
            PublicNetworkPort = publicNetworkPort;
        }

        public static GameServerConfig ReadFromEnvironment()
        {
            return new GameServerConfig(
                Environment.GetEnvironmentVariable(ServerIdVariable) ?? string.Empty,
                ReadPort(HttpPortVariable),
                ReadPort(NetworkPortVariable),
                Environment.GetEnvironmentVariable(PublicHostVariable) ?? string.Empty,
                ReadPort(PublicNetworkPortVariable));
        }

        public bool Validate(out string error)
        {
            if (string.IsNullOrWhiteSpace(ServerId))
            {
                error = $"{ServerIdVariable} is required.";
                return false;
            }

            if (HttpPort == 0)
            {
                error = $"{HttpPortVariable} must be a valid TCP port from 1 to 65535.";
                return false;
            }

            if (NetworkPort == 0)
            {
                error = $"{NetworkPortVariable} must be a valid UDP port from 1 to 65535.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(PublicHost))
            {
                error = $"{PublicHostVariable} is required.";
                return false;
            }

            if (PublicNetworkPort == 0)
            {
                error = $"{PublicNetworkPortVariable} must be a valid UDP port from 1 to 65535.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static ushort ReadPort(string variableName)
        {
            string? raw = Environment.GetEnvironmentVariable(variableName);
            if (!ushort.TryParse(raw, out ushort port))
                return 0;

            return port;
        }
    }
}
