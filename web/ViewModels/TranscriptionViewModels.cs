namespace web.ViewModels
{
    public class TranscriptionSectionViewModel
    {
        public int LectureId { get; set; }
        public bool HasFiles { get; set; }
        public bool AiGatewayOnline { get; set; } = true;
        public List<TranscriptionModelOptionViewModel> AvailableModels { get; set; } = new();
        public string? SelectedModel { get; set; }
        public string SelectedLanguage { get; set; } = "da";
        public List<TranscriptionVersionSummaryViewModel> VersionSummaries { get; set; } = new();
        public TranscriptionVersionViewModel? CurrentVersion { get; set; }

        /// <summary>Set when a background job is Pending/Running for this lecture - drives the "in progress" UI and client-side polling.</summary>
        public int? ActiveJobId { get; set; }
    }

    public class TranscriptionModelOptionViewModel
    {
        public string Id { get; set; } = string.Empty;

        /// <summary>ISO 639-1 codes this model supports, per Speaches' ListModels "language" field.</summary>
        public List<string> LanguageCodes { get; set; } = new();
    }

    public class TranscriptionVersionSummaryViewModel
    {
        public int VersionNumber { get; set; }
        public DateTime CreatedAtUtc { get; set; }
    }

    public class TranscriptionVersionViewModel
    {
        public int VersionNumber { get; set; }
        public string Model { get; set; } = string.Empty;
        public string? Language { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public List<TranscriptionSegmentViewModel> Segments { get; set; } = new();
    }

    public class TranscriptionSegmentViewModel
    {
        public string OriginalFileName { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public bool Success { get; set; } = true;
        public string? ErrorMessage { get; set; }
    }
}
