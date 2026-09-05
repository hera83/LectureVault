namespace web.Repositories.Transcriptions.Dtos
{
    public class TranscriptionSegmentDto
    {
        public string OriginalFileName { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public bool Success { get; set; } = true;
        public string? ErrorMessage { get; set; }
    }
}
