namespace web.Repositories.Lectures.Dtos
{
    public class LectureSummaryDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int FileCount { get; set; }
        public DateTime CreatedAtUtc { get; set; }
    }
}
