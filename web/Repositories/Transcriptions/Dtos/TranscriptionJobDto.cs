namespace web.Repositories.Transcriptions.Dtos
{
    public class TranscriptionJobDto
    {
        public int Id { get; set; }
        public int LectureId { get; set; }
        public string Status { get; set; } = string.Empty;
        public int? ResultVersionNumber { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
