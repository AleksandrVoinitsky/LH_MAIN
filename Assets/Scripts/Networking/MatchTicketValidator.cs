using System;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using LH.Main.Unity.Server;

namespace LH.Main.Unity.Networking
{
    public interface IMatchTicketValidationSender
    {
        Task<MatchTicketValidationResponse> SendAsync(Uri endpoint, string sharedKey, string body, CancellationToken cancellationToken);
    }

    public readonly struct MatchTicketValidationResponse
    {
        public int StatusCode { get; }
        public string Body { get; }

        public MatchTicketValidationResponse(int statusCode, string body)
        {
            StatusCode = statusCode;
            Body = body ?? string.Empty;
        }
    }

    public sealed class MatchTicketValidator
    {
        public const string ValidationPath = "/internal/v1/matches/tickets/validate";
        private readonly string _backendBaseUrl;
        private readonly string _sharedKey;
        private readonly TimeSpan _timeout;
        private readonly IMatchTicketValidationSender _sender;
        private readonly Action<string> _diagnosticSink;

        public MatchTicketValidator(GameServerConfig config)
            : this(config, CreateHttpSender(config))
        {
        }

        public MatchTicketValidator(GameServerConfig config, IMatchTicketValidationSender sender)
            : this(
                RequireConfig(config).BackendBaseUrl,
                config.SharedKey,
                TimeSpan.FromSeconds(config.TicketValidationTimeoutSeconds),
                sender)
        {
        }

        public MatchTicketValidator(string backendBaseUrl, string sharedKey, TimeSpan timeout, IMatchTicketValidationSender sender)
            : this(backendBaseUrl, sharedKey, timeout, sender, null)
        {
        }

        public MatchTicketValidator(string backendBaseUrl, string sharedKey, TimeSpan timeout, IMatchTicketValidationSender sender, Action<string> diagnosticSink)
        {
            _backendBaseUrl = backendBaseUrl ?? string.Empty;
            _sharedKey = sharedKey ?? string.Empty;
            _timeout = timeout;
            _sender = sender ?? throw new ArgumentNullException(nameof(sender));
            _diagnosticSink = diagnosticSink;
        }

        public async Task<MatchAdmissionResult> ValidateAsync(MatchAdmissionPayload payload, CancellationToken cancellationToken)
        {
            using (var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                if (_timeout > TimeSpan.Zero)
                    timeoutSource.CancelAfter(_timeout);

                try
                {
                    MatchTicketValidationResponse response = await _sender.SendAsync(
                        CreateValidationEndpoint(),
                        _sharedKey,
                        payload.ToJson(),
                        timeoutSource.Token);

                    return MapResponse(payload, response);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    EmitDiagnostic(payload, "validation_timeout");
                    return MatchAdmissionResult.Rejected("validation_timeout");
                }
                catch (Exception exception)
                {
                    EmitDiagnostic(payload, "validation_error", exception.GetType().Name);
                    return MatchAdmissionResult.Rejected("validation_error");
                }
            }
        }

        private MatchAdmissionResult MapResponse(MatchAdmissionPayload payload, MatchTicketValidationResponse response)
        {
            if (response.StatusCode == 401)
            {
                EmitDiagnostic(payload, "server_key_rejected");
                return MatchAdmissionResult.Rejected("server_key_rejected");
            }

            if (response.StatusCode < 200 || response.StatusCode >= 300)
            {
                EmitDiagnostic(payload, StatusCategory(response.StatusCode));
                return MatchAdmissionResult.Rejected("validation_failed");
            }

            if (!TryReadBool(response.Body, "valid", out bool valid) || !valid)
                return MatchAdmissionResult.Rejected("ticket_invalid");

            if (!TryReadGuid(response.Body, "matchId", out Guid matchId)
                || !TryReadGuid(response.Body, "playerId", out Guid playerId)
                || matchId != payload.MatchId
                || playerId != payload.PlayerId)
            {
                EmitDiagnostic(payload, "validation_mismatch");
                return MatchAdmissionResult.Rejected("validation_mismatch");
            }

            return MatchAdmissionResult.Accepted(matchId, playerId);
        }

        private void EmitDiagnostic(MatchAdmissionPayload payload, string category, string detail = "")
        {
            if (_diagnosticSink == null)
                return;

            string host = "unknown";
            if (Uri.TryCreate(_backendBaseUrl, UriKind.Absolute, out Uri uri) && !string.IsNullOrWhiteSpace(uri.Host))
                host = uri.Host;

            string message = "ticket validation " + category
                + " backendHost=" + host
                + " matchId=" + payload.MatchId
                + " playerId=" + payload.PlayerId
                + " serverId=" + payload.ServerId;

            if (!string.IsNullOrWhiteSpace(detail))
                message += " detail=" + detail;

            _diagnosticSink(message);
        }

        private Uri CreateValidationEndpoint()
        {
            string endpoint = (_backendBaseUrl ?? string.Empty).TrimEnd('/') + ValidationPath;
            return new Uri(endpoint, UriKind.Absolute);
        }

        private static GameServerConfig RequireConfig(GameServerConfig config)
        {
            return config ?? throw new ArgumentNullException(nameof(config));
        }

        private static IMatchTicketValidationSender CreateHttpSender(GameServerConfig config)
        {
            RequireConfig(config);
            return new HttpClientMatchTicketValidationSender();
        }

        private static string StatusCategory(int statusCode)
        {
            if (statusCode >= 500)
                return "server_error";

            if (statusCode >= 400)
                return "client_error";

            return "unexpected_status";
        }

        private static bool TryReadGuid(string json, string name, out Guid value)
        {
            value = Guid.Empty;
            return TryReadString(json, name, out string text) && Guid.TryParse(text, out value);
        }

        private static bool TryReadBool(string json, string name, out bool value)
        {
            value = false;
            Match match = Regex.Match(json ?? string.Empty, "\\\"" + Regex.Escape(name) + "\\\"\\s*:\\s*(true|false)", RegexOptions.IgnoreCase);
            return match.Success && bool.TryParse(match.Groups[1].Value, out value);
        }

        private static bool TryReadString(string json, string name, out string value)
        {
            value = string.Empty;
            Match match = Regex.Match(json ?? string.Empty, "\\\"" + Regex.Escape(name) + "\\\"\\s*:\\s*\\\"([^\\\"]*)\\\"");
            if (!match.Success)
                return false;

            value = match.Groups[1].Value;
            return true;
        }

        private sealed class HttpClientMatchTicketValidationSender : IMatchTicketValidationSender
        {
            public async Task<MatchTicketValidationResponse> SendAsync(Uri endpoint, string sharedKey, string body, CancellationToken cancellationToken)
            {
                using (var client = new HttpClient())
                using (var request = new HttpRequestMessage(HttpMethod.Post, endpoint))
                {
                    request.Headers.TryAddWithoutValidation("X-Game-Server-Key", sharedKey ?? string.Empty);
                    request.Content = new StringContent(body ?? string.Empty, Encoding.UTF8, "application/json");

                    using (HttpResponseMessage response = await client.SendAsync(request, cancellationToken))
                    {
                        string responseBody = await response.Content.ReadAsStringAsync();
                        return new MatchTicketValidationResponse((int)response.StatusCode, responseBody);
                    }
                }
            }
        }
    }
}
