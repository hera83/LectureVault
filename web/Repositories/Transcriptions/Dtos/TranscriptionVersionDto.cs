namespace web.Repositories.Transcriptions.Dtos
{
    public class TranscriptionVersionDto
    {
        public int VersionNumber { get; set; }
        public string Model { get; set; } = string.Empty;
        public string? Language { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public List<TranscriptionSegmentDto> Segments { get; set; } = new();
    }
}
