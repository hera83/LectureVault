namespace web.Constants
{
    /// <summary>
    /// Curated list of ISO 639-1 language codes offered as a transcription hint to Speaches/Whisper.
    /// The gateway requires an ISO code (e.g. "da"), not a display name - see the comment on
    /// LectureController's transcription form for why a free-text field is not used here.
    /// </summary>
    public static class TranscriptionLanguages
    {
        public static readonly IReadOnlyList<(string Code, string Label)> All =
        [
            ("", "Auto-detekt"),
            ("da", "Dansk"),
            ("en", "Engelsk"),
            ("de", "Tysk"),
            ("sv", "Svensk"),
            ("no", "Norsk"),
            ("fr", "Fransk"),
            ("es", "Spansk"),
            ("it", "Italiensk"),
            ("nl", "Nederlandsk"),
            ("pl", "Polsk"),
            ("fi", "Finsk")
        ];

        public static bool IsKnownCode(string? code) =>
            string.IsNullOrEmpty(code) || All.Any(l => l.Code == code);
    }
}
