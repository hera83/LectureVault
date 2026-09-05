namespace web.Repositories.Lectures.Dtos
{
    public class CreateLectureResponseDto
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public int LectureId { get; set; }
    }
}
