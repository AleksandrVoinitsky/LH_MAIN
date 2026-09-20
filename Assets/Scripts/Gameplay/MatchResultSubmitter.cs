using System;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace LH.Main.Unity.Gameplay
{
    public interface IMatchResultSender
    {
        Task<MatchResultSubmissionResponse> SendAsync(Uri endpoint, string sharedKey, string body, CancellationToken cancellationToken);
    }

    public sealed class MatchResultSubmitter
    {
        public const string SubmissionPath = "/internal/v1/matches/results";

        private readonly string _backendBaseUrl;
        private readonly string _sharedKey;
        private readonly TimeSpan _timeout;
        private readonly IMatchResultSender _sender;
        private readonly Action<string> _diagnosticSink;

        public MatchResultSubmitter(string backendBaseUrl, string sharedKey, TimeSpan timeout, IMatchResultSender sender)
            : this(backendBaseUrl, sharedKey, timeout, sender, null)
        {
        }

        public MatchResultSubmitter(string backendBaseUrl, string sharedKey, TimeSpan timeout, IMatchResultSender sender, Action<string> diagnosticSink)
        {
            _backendBaseUrl = backendBaseUrl ?? string.Empty;
            _sharedKey = sharedKey ?? string.Empty;
            _timeout = timeout;
            _sender = sender ?? throw new ArgumentNullException(nameof(sender));
            _diagnosticSink = diagnosticSink;
        }

        public async Task<MatchResultSubmissionOutcome> SubmitAsync(MatchResultPayload payload, CancellationToken cancellationToken)
        {
            if (payload == null)
                throw new ArgumentNullException(nameof(payload));

            using (var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                if (_timeout > TimeSpan.Zero)
                    timeoutSource.CancelAfter(_timeout);

                try
                {
                    MatchResultSubmissionResponse response = await _sender.SendAsync(
                        CreateSubmissionEndpoint(),
                        _sharedKey,
                        payload.ToJson(),
                        timeoutSource.Token);

                    return MapResponse(payload, response);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    EmitDiagnostic(payload, "submission_timeout");
                    return MatchResultSubmissionOutcome.Rejected("submission_timeout");
                }
                catch (Exception exception)
                {
                    EmitDiagnostic(payload, "submission_error", exception.GetType().Name);
                    return MatchResultSubmissionOutcome.Rejected("submission_error");
                }
            }
        }

        private MatchResultSubmissionOutcome MapResponse(MatchResultPayload payload, MatchResultSubmissionResponse response)
        {
            if (response.StatusCode == 401)
            {
                EmitDiagnostic(payload, "server_key_rejected");
                return MatchResultSubmissionOutcome.Rejected("server_key_rejected");
            }

            if (response.StatusCode < 200 || response.StatusCode >= 300)
            {
                EmitDiagnostic(payload, StatusCategory(response.StatusCode));
                return MatchResultSubmissionOutcome.Rejected("submission_failed");
            }

            if (!TryReadBool(response.Body, "accepted", out bool accepted) || !accepted)
                return MatchResultSubmissionOutcome.Rejected("result_rejected");

            if (!TryReadGuid(response.Body, "resultId", out Guid resultId) || resultId != payload.ResultId)
            {
                EmitDiagnostic(payload, "submission_mismatch");
                return MatchResultSubmissionOutcome.Rejected("submission_mismatch");
            }

            TryReadInt(response.Body, "newRewardTransactions", out int newRewardTransactions);
            TryReadBool(response.Body, "duplicate", out bool duplicate);
            return MatchResultSubmissionOutcome.AcceptedResult(resultId, newRewardTransactions, duplicate);
        }

        private Uri CreateSubmissionEndpoint()
        {
            string endpoint = (_backendBaseUrl ?? string.Empty).TrimEnd('/') + SubmissionPath;
            return new Uri(endpoint, UriKind.Absolute);
        }

        private void EmitDiagnostic(MatchResultPayload payload, string category, string detail = "")
        {
            if (_diagnosticSink == null)
                return;

            string host = "unknown";
            if (Uri.TryCreate(_backendBaseUrl, UriKind.Absolute, out Uri uri) && !string.IsNullOrWhiteSpace(uri.Host))
                host = uri.Host;

            string message = "match result " + category
                + " backendHost=" + host
                + " matchId=" + payload.MatchId
                + " resultId=" + payload.ResultId
                + " serverId=" + payload.ServerId;

            if (!string.IsNullOrWhiteSpace(detail))
                message += " detail=" + detail;

            _diagnosticSink(message);
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

        private static bool TryReadInt(string json, string name, out int value)
        {
            value = 0;
            Match match = Regex.Match(json ?? string.Empty, "\\\"" + Regex.Escape(name) + "\\\"\\s*:\\s*(-?\\d+)");
            return match.Success && int.TryParse(match.Groups[1].Value, out value);
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

        private sealed class HttpClientMatchResultSender : IMatchResultSender
        {
            public async Task<MatchResultSubmissionResponse> SendAsync(Uri endpoint, string sharedKey, string body, CancellationToken cancellationToken)
            {
                using (var client = new HttpClient())
                using (var request = new HttpRequestMessage(HttpMethod.Post, endpoint))
                {
                    request.Headers.TryAddWithoutValidation("X-Game-Server-Key", sharedKey ?? string.Empty);
                    request.Content = new StringContent(body ?? string.Empty, Encoding.UTF8, "application/json");

                    using (HttpResponseMessage response = await client.SendAsync(request, cancellationToken))
                    {
                        string responseBody = await response.Content.ReadAsStringAsync();
                        return new MatchResultSubmissionResponse((int)response.StatusCode, responseBody);
                    }
                }
            }
        }
    }
}
