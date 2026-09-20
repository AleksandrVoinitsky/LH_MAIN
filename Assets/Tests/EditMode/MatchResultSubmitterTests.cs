using System;
using System.Threading;
using System.Threading.Tasks;
using LH.Main.Unity.Gameplay;
using NUnit.Framework;

public sealed class MatchResultSubmitterTests
{
    [Test]
    public async Task SubmitAsyncTreatsDuplicateResponseAsAccepted()
    {
        var payload = MatchResultPayload.EmptyForTests(Guid.Parse("11111111-1111-1111-1111-111111111111"), "game-server-1");
        var sender = new RecordingMatchResultSender(200, "{\"accepted\":true,\"resultId\":\"" + payload.ResultId + "\",\"newRewardTransactions\":0,\"duplicate\":true}");
        var submitter = new MatchResultSubmitter("http://backend:8080", "shared-key", TimeSpan.FromSeconds(3), sender);

        MatchResultSubmissionOutcome outcome = await submitter.SubmitAsync(payload, CancellationToken.None);

        Assert.That(outcome.Accepted, Is.True);
        Assert.That(outcome.Duplicate, Is.True);
        Assert.That(sender.Path, Is.EqualTo("/internal/v1/matches/results"));
        Assert.That(sender.SharedKeyHeader, Is.EqualTo("shared-key"));
    }

    private sealed class RecordingMatchResultSender : IMatchResultSender
    {
        private readonly int _statusCode;
        private readonly string _body;

        public RecordingMatchResultSender(int statusCode, string body)
        {
            _statusCode = statusCode;
            _body = body;
        }

        public string Path { get; private set; }
        public string SharedKeyHeader { get; private set; }

        public Task<MatchResultSubmissionResponse> SendAsync(Uri endpoint, string sharedKey, string body, CancellationToken cancellationToken)
        {
            Path = endpoint.AbsolutePath;
            SharedKeyHeader = sharedKey;
            return Task.FromResult(new MatchResultSubmissionResponse(_statusCode, _body));
        }
    }
}
