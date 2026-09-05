namespace web.Repositories.Lectures.Dtos
{
    public class CreateLectureRequestDto
    {
        public string OwnerId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }
}
