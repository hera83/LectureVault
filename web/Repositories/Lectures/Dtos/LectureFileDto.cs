namespace web.Repositories.Lectures.Dtos
{
    public class LectureFileDto
    {
        public int Id { get; set; }
        public string OriginalFileName { get; set; } = string.Empty;
        public long FileSizeBytes { get; set; }
        public string ContentType { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; }
    }
}
