using BPOPlatform.Domain.Interfaces;

namespace BPOPlatform.Infrastructure.Services;

/// <summary>
/// Development/test fallback implementation of <see cref="ISpeechTranscriptionService"/>.
/// Returns a representative mock transcript without calling Azure AI Speech Services.
/// Registered automatically when no Speech Services endpoint/key is configured.
/// </summary>
public class MockSpeechTranscriptionService : ISpeechTranscriptionService
{
    public Task<string> TranscribeAsync(Stream audioStream, string contentType, CancellationToken ct = default)
    {
        const string mockTranscript =
            "[Mock Speech Transcription]\n\n" +
            "Interviewer: Can you walk me through how the accounts payable process works today?\n\n" +
            "Respondent: Sure. When an invoice arrives, we first log it manually in SAP. " +
            "That takes about 10 minutes per invoice. Then we check against the purchase order — " +
            "if the amounts match we approve it, if not it goes to the manager. " +
            "We process roughly 200 invoices a day and about 15 percent need manual intervention. " +
            "The biggest pain point is finding the right approver when someone is on leave. " +
            "Sometimes invoices sit for three days waiting for approval.\n\n" +
            "Interviewer: Is there anything that could be automated?\n\n" +
            "Respondent: The matching step is very rule-based — I think 80 percent of invoices " +
            "could be handled automatically. The exceptions are the tricky part.";

        return Task.FromResult(mockTranscript);
    }
}
