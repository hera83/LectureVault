using web.Repositories.Lectures.Dtos;

namespace web.Repositories.Lectures.Interfaces
{
    public interface ILecturesService
    {
        Task<CreateLectureResponseDto> CreateLectureAsync(CreateLectureRequestDto request, CancellationToken ct = default);
        Task<List<LectureSummaryDto>> GetLecturesAsync(string ownerId, CancellationToken ct = default);
        Task<LectureDetailsDto?> GetLectureDetailsAsync(int lectureId, string ownerId, CancellationToken ct = default);
        Task<LectureFileDto?> AddFileAsync(int lectureId, string ownerId, Stream fileStream, string contentType, string originalFileName, CancellationToken ct = default);
        Task<(string FullPath, string ContentType, string OriginalFileName)?> GetFileForDownloadAsync(int fileId, string ownerId, CancellationToken ct = default);

        /// <summary>Swaps the file with its neighbour one position up (direction &lt; 0) or down (direction &gt; 0) in the lecture's manual order. Returns false if already at that end.</summary>
        Task<bool> MoveFileAsync(int fileId, string ownerId, int direction, CancellationToken ct = default);

        Task<bool> DeleteFileAsync(int fileId, string ownerId, CancellationToken ct = default);
        Task<bool> DeleteLectureAsync(int lectureId, string ownerId, CancellationToken ct = default);
    }
}
