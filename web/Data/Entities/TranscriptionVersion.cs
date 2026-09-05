namespace web.Data.Entities
{
    /// <summary>
    /// One transcription run for a lecture. Re-running transcription creates a new version
    /// rather than overwriting the previous one, so older results stay available.
    /// </summary>
    public class TranscriptionVersion
    {
        /// <summary>
        /// Primary key
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// The lecture this transcription run belongs to
        /// </summary>
        public int LectureId { get; set; }

        /// <summary>
        /// 1-based, incrementing per lecture (1, 2, 3, ...). Highest number is the newest version.
        /// </summary>
        public int VersionNumber { get; set; }

        /// <summary>
        /// Speaches model id used for this run (e.g. "Systran/faster-whisper-small")
        /// </summary>
        public string Model { get; set; } = string.Empty;

        /// <summary>
        /// ISO 639-1 language code used as a hint for the run, or null for auto-detect
        /// </summary>
        public string? Language { get; set; }

        /// <summary>
        /// UTC timestamp when this run was started
        /// </summary>
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
