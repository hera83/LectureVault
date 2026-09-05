using System.Text;
using web.ViewModels;

namespace web.Infrastructure
{
    public static class TranscriptFormatter
    {
        /// <summary>
        /// Joins a transcription version's per-file segments into one readable text block,
        /// with a filename header per segment so multi-file lectures stay easy to follow.
        /// </summary>
        public static string CombineSegments(List<TranscriptionSegmentViewModel> segments)
        {
            if (segments.Count == 0) return string.Empty;

            var sb = new StringBuilder();
            for (var i = 0; i < segments.Count; i++)
            {
                var segment = segments[i];
                if (i > 0) sb.AppendLine().AppendLine();

                sb.Append("=== ").Append(segment.OriginalFileName).AppendLine(" ===");
                sb.Append(segment.Success
                    ? segment.Text
                    : $"[Fejl: {segment.ErrorMessage ?? "kunne ikke transskribere filen"}]");
            }

            return sb.ToString();
        }
    }
}
