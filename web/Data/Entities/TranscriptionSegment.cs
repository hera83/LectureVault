namespace web.Data.Entities
{
    /// <summary>
    /// The transcribed text for a single audio file within one TranscriptionVersion run.
    /// OriginalFileName is a snapshot, so the segment stays readable even if the source
    /// file is later deleted from the lecture's file registration.
    /// </summary>
    public class TranscriptionSegment
    {
        /// <summary>
        /// Primary key
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// The transcription run this segment belongs to
        /// </summary>
        public int TranscriptionVersionId { get; set; }

        /// <summary>
        /// Optional: the source FileMetadata row. Null if the file has since been deleted.
        /// </summary>
        public int? FileMetadataId { get; set; }

        /// <summary>
        /// Snapshot of the source file's original filename at the time of transcription
        /// </summary>
        public string OriginalFileName { get; set; } = string.Empty;

        /// <summary>
        /// The transcribed text, empty if transcription failed for this file
        /// </summary>
        public string Text { get; set; } = string.Empty;

        /// <summary>
        /// Whether transcription succeeded for this file
        /// </summary>
        public bool Success { get; set; } = true;

        /// <summary>
        /// Set when Success is false
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// UTC timestamp when this segment was created
        /// </summary>
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
