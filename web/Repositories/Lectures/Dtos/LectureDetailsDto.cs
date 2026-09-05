namespace web.Repositories.Lectures.Dtos
{
    public class LectureDetailsDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; }
        public List<LectureFileDto> Files { get; set; } = new();
    }
}
