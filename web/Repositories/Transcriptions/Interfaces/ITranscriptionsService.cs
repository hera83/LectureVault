using web.Repositories.Transcriptions.Dtos;

namespace web.Repositories.Transcriptions.Interfaces
{
    public interface ITranscriptionsService
    {
        Task<List<TranscriptionVersionSummaryDto>> GetVersionSummariesAsync(int lectureId, string ownerId, CancellationToken ct = default);
        Task<TranscriptionVersionDto?> GetVersionAsync(int lectureId, string ownerId, int? versionNumber, CancellationToken ct = default);

        /// <summary>
        /// Queues a background transcription run over every non-deleted audio file currently on
        /// the lecture. Returns null if the lecture doesn't exist / isn't owned by ownerId, or has
        /// no files. If a job is already Pending/Running for this lecture, returns that job instead
        /// of queuing a second one.
        /// </summary>
        Task<TranscriptionJobDto?> EnqueueJobAsync(int lectureId, string ownerId, string model, string? language, CancellationToken ct = default);

        /// <summary>The lecture's current Pending/Running job, if any.</summary>
        Task<TranscriptionJobDto?> GetActiveJobAsync(int lectureId, string ownerId, CancellationToken ct = default);

        /// <summary>Status of a single job, owner-checked - used by the client-side poller.</summary>
        Task<TranscriptionJobDto?> GetJobStatusAsync(int jobId, string ownerId, CancellationToken ct = default);

        /// <summary>
        /// Does the actual transcription work for a queued job (called by TranscriptionWorker, not
        /// directly by a controller) and records the outcome back onto the job row.
        /// </summary>
        Task ProcessJobAsync(int jobId, CancellationToken ct = default);
    }
}
