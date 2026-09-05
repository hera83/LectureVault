namespace web.Data.Entities
{
    /// <summary>
    /// A background transcription run, processed by TranscriptionWorker so that starting one
    /// doesn't tie up the request - and keeps going even if the browser tab is closed.
    /// </summary>
    public class TranscriptionJob
    {
        /// <summary>
        /// Primary key
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// The lecture this job transcribes
        /// </summary>
        public int LectureId { get; set; }

        /// <summary>
        /// User ID of whoever started the job (for ownership checks on status polling)
        /// </summary>
        public string OwnerId { get; set; } = string.Empty;

        /// <summary>
        /// Speaches model id to transcribe with
        /// </summary>
        public string Model { get; set; } = string.Empty;

        /// <summary>
        /// ISO 639-1 language hint, or null for auto-detect
        /// </summary>
        public string? Language { get; set; }

        /// <summary>
        /// See Constants.TranscriptionJobStatus
        /// </summary>
        public string Status { get; set; } = Constants.TranscriptionJobStatus.Pending;

        /// <summary>
        /// Set once Completed: the version number of the TranscriptionVersion this job produced
        /// </summary>
        public int? ResultVersionNumber { get; set; }

        /// <summary>
        /// Set if Status is Failed - the job itself failed, not an individual file
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// UTC timestamp when the job was queued
        /// </summary>
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// UTC timestamp when the worker picked it up
        /// </summary>
        public DateTime? StartedAtUtc { get; set; }

        /// <summary>
        /// UTC timestamp when it finished (Completed or Failed)
        /// </summary>
        public DateTime? CompletedAtUtc { get; set; }
    }
}
