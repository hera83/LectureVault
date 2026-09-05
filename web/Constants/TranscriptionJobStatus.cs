namespace web.Constants
{
    /// <summary>
    /// Lifecycle of a background transcription job (see TranscriptionWorker).
    /// </summary>
    public static class TranscriptionJobStatus
    {
        /// <summary>Queued, not yet picked up by the worker.</summary>
        public const string Pending = "Pending";

        /// <summary>Worker has picked it up and is transcribing files one by one.</summary>
        public const string Running = "Running";

        /// <summary>Finished - a TranscriptionVersion was created (individual files may still have failed; see its segments).</summary>
        public const string Completed = "Completed";

        /// <summary>The job itself failed before a version could be produced; see ErrorMessage.</summary>
        public const string Failed = "Failed";
    }
}
