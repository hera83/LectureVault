using System.ComponentModel.DataAnnotations;

namespace web.ViewModels
{
    public class LectureListViewModel
    {
        public List<LectureCardViewModel> Lectures { get; set; } = new();
    }

    public class LectureCardViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int FileCount { get; set; }
        public DateTime CreatedAtUtc { get; set; }
    }

    public class CreateLectureViewModel
    {
        [Required(ErrorMessage = "Navn er påkrævet")]
        [StringLength(200, ErrorMessage = "Navn må ikke være længere end 200 tegn")]
        [Display(Name = "Navn")]
        public string Name { get; set; } = string.Empty;
    }

    public class LectureDetailsViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; }
        public List<LectureFileViewModel> Files { get; set; } = new();
        public TranscriptionSectionViewModel Transcription { get; set; } = new();
    }

    public class LectureFileViewModel
    {
        public int Id { get; set; }
        public string OriginalFileName { get; set; } = string.Empty;
        public long FileSizeBytes { get; set; }
        public string ContentType { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; }
    }
}
