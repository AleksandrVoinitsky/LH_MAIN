using System;
using System.Threading;
using System.Threading.Tasks;
using FishNet.Broadcast;

namespace LH.Main.Unity.Networking
{
    public sealed class GameServerAuthenticator
    {
        private readonly string _localServerId;
        private readonly Func<MatchAdmissionPayload, CancellationToken, Task<MatchAdmissionResult>> _validateTicketAsync;

        public GameServerAuthenticator(
            string localServerId,
            Func<MatchAdmissionPayload, CancellationToken, Task<MatchAdmissionResult>> validateTicketAsync)
        {
            _localServerId = localServerId ?? string.Empty;
            _validateTicketAsync = validateTicketAsync ?? throw new ArgumentNullException(nameof(validateTicketAsync));
        }

        public GameServerAuthenticator(string localServerId, MatchTicketValidator ticketValidator)
            : this(
                localServerId,
                (ticketValidator ?? throw new ArgumentNullException(nameof(ticketValidator))).ValidateAsync)
        {
        }

        public async Task<MatchAdmissionResult> TryAuthenticateAsync(string payloadJson, CancellationToken cancellationToken)
        {
            if (!MatchAdmissionPayload.TryParse(payloadJson, out MatchAdmissionPayload payload, out string error))
                return MatchAdmissionResult.Rejected(error);

            if (!string.Equals(payload.ServerId, _localServerId, StringComparison.Ordinal))
                return MatchAdmissionResult.Rejected("wrong_server");

            return await _validateTicketAsync(payload, cancellationToken);
        }
    }

    public struct GameServerAdmissionFishNetBroadcast : IBroadcast
    {
        public string PayloadJson;
    }

    public struct GameServerAdmissionResultFishNetBroadcast : IBroadcast
    {
        public bool Accepted;
        public string Reason;
    }

}
