namespace web.Data.Entities
{
    /// <summary>
    /// A lecture (Lektion) that audio recordings are uploaded to and transcribed under.
    /// The audio files themselves are stored as FileMetadata rows linked via LectureId.
    /// </summary>
    public class Lecture
    {
        /// <summary>
        /// Primary key
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Display name of the lecture, e.g. "Jura"
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// User ID of the lecture owner (ApplicationUser.Id)
        /// </summary>
        public string OwnerId { get; set; } = string.Empty;

        /// <summary>
        /// UTC timestamp when the lecture was created
        /// </summary>
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// UTC timestamp when the lecture was last updated
        /// </summary>
        public DateTime? UpdatedAtUtc { get; set; }
    }
}
