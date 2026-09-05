using Microsoft.EntityFrameworkCore;
using web.Constants;
using web.Data;
using web.Data.Entities;
using web.Repositories.Lectures.Dtos;
using web.Repositories.Lectures.Interfaces;

namespace web.Repositories.Lectures
{
    public class LecturesService : ILecturesService
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly IConfiguration _config;
        private readonly ILogger<LecturesService> _logger;

        public LecturesService(
            ApplicationDbContext context,
            IWebHostEnvironment env,
            IConfiguration config,
            ILogger<LecturesService> logger)
        {
            _context = context;
            _env = env;
            _config = config;
            _logger = logger;
        }

        public async Task<CreateLectureResponseDto> CreateLectureAsync(CreateLectureRequestDto request, CancellationToken ct = default)
        {
            var name = request.Name.Trim();
            if (string.IsNullOrWhiteSpace(name))
                return new CreateLectureResponseDto { Success = false, ErrorMessage = "Navn er påkrævet." };

            var lecture = new Lecture
            {
                Name = name,
                OwnerId = request.OwnerId,
                CreatedAtUtc = DateTime.UtcNow
            };

            _context.Lectures.Add(lecture);
            await _context.SaveChangesAsync(ct);

            _logger.LogInformation("User {UserId} created lecture {LectureId} ({Name})", request.OwnerId, lecture.Id, lecture.Name);
            return new CreateLectureResponseDto { Success = true, LectureId = lecture.Id };
        }

        public async Task<List<LectureSummaryDto>> GetLecturesAsync(string ownerId, CancellationToken ct = default)
        {
            return await _context.Lectures
                .AsNoTracking()
                .Where(l => l.OwnerId == ownerId)
                .OrderByDescending(l => l.CreatedAtUtc)
                .Select(l => new LectureSummaryDto
                {
                    Id = l.Id,
                    Name = l.Name,
                    CreatedAtUtc = l.CreatedAtUtc,
                    FileCount = _context.FileMetadata.Count(f => f.LectureId == l.Id && !f.IsDeleted)
                })
                .ToListAsync(ct);
        }

        public async Task<LectureDetailsDto?> GetLectureDetailsAsync(int lectureId, string ownerId, CancellationToken ct = default)
        {
            var lecture = await _context.Lectures
                .AsNoTracking()
                .FirstOrDefaultAsync(l => l.Id == lectureId && l.OwnerId == ownerId, ct);
            if (lecture is null) return null;

            var files = await _context.FileMetadata
                .AsNoTracking()
                .Where(f => f.LectureId == lectureId && !f.IsDeleted)
                .OrderBy(f => f.SortIndex)
                .Select(f => new LectureFileDto
                {
                    Id = f.Id,
                    OriginalFileName = f.OriginalFileName,
                    FileSizeBytes = f.FileSizeBytes,
                    ContentType = f.ContentType,
                    CreatedAtUtc = f.CreatedAtUtc
                })
                .ToListAsync(ct);

            return new LectureDetailsDto
            {
                Id = lecture.Id,
                Name = lecture.Name,
                CreatedAtUtc = lecture.CreatedAtUtc,
                Files = files
            };
        }

        public async Task<LectureFileDto?> AddFileAsync(int lectureId, string ownerId, Stream fileStream, string contentType, string originalFileName, CancellationToken ct = default)
        {
            var lecture = await _context.Lectures
                .FirstOrDefaultAsync(l => l.Id == lectureId && l.OwnerId == ownerId, ct);
            if (lecture is null) return null;

            var filesPath = _config["AppSettings:FilesPath"] ?? "App_files";
            var lectureDir = Path.Combine(_env.ContentRootPath, filesPath, FileCategories.Lectures);
            Directory.CreateDirectory(lectureDir);

            var ext = Path.GetExtension(originalFileName);
            var storedFileName = $"{Guid.NewGuid()}{ext}";
            var storedRelativePath = Path.Combine(filesPath, FileCategories.Lectures, storedFileName);
            var fullPath = Path.Combine(_env.ContentRootPath, storedRelativePath);

            await using (var destination = File.Create(fullPath))
            {
                await fileStream.CopyToAsync(destination, ct);
            }

            var fileInfo = new FileInfo(fullPath);

            // New uploads always go to the end of the lecture's file order, regardless of
            // how the existing files have been manually reordered.
            var nextSortIndex = 1 + await _context.FileMetadata
                .Where(f => f.LectureId == lectureId && !f.IsDeleted)
                .Select(f => (int?)f.SortIndex)
                .MaxAsync(ct) ?? 0;

            var metadata = new FileMetadata
            {
                OriginalFileName = originalFileName,
                StoredFileName = storedFileName,
                StoredPath = storedRelativePath,
                ContentType = contentType,
                FileSizeBytes = fileInfo.Length,
                OwnerId = ownerId,
                Category = FileCategories.Lectures,
                LectureId = lectureId,
                SortIndex = nextSortIndex,
                CreatedAtUtc = DateTime.UtcNow
            };

            _context.FileMetadata.Add(metadata);
            await _context.SaveChangesAsync(ct);

            _logger.LogInformation("User {UserId} uploaded audio file {FileName} to lecture {LectureId}", ownerId, storedFileName, lectureId);

            return new LectureFileDto
            {
                Id = metadata.Id,
                OriginalFileName = metadata.OriginalFileName,
                FileSizeBytes = metadata.FileSizeBytes,
                ContentType = metadata.ContentType,
                CreatedAtUtc = metadata.CreatedAtUtc
            };
        }

        public async Task<(string FullPath, string ContentType, string OriginalFileName)?> GetFileForDownloadAsync(int fileId, string ownerId, CancellationToken ct = default)
        {
            var metadata = await _context.FileMetadata
                .AsNoTracking()
                .FirstOrDefaultAsync(f => f.Id == fileId && f.OwnerId == ownerId && f.Category == FileCategories.Lectures && !f.IsDeleted, ct);
            if (metadata is null) return null;

            var fullPath = Path.Combine(_env.ContentRootPath, metadata.StoredPath);
            if (!File.Exists(fullPath)) return null;

            return (fullPath, metadata.ContentType, metadata.OriginalFileName);
        }

        public async Task<bool> MoveFileAsync(int fileId, string ownerId, int direction, CancellationToken ct = default)
        {
            var target = await _context.FileMetadata
                .FirstOrDefaultAsync(f => f.Id == fileId && f.OwnerId == ownerId && f.Category == FileCategories.Lectures && !f.IsDeleted, ct);
            if (target?.LectureId is null) return false;

            var orderedFiles = await _context.FileMetadata
                .Where(f => f.LectureId == target.LectureId && !f.IsDeleted)
                .OrderBy(f => f.SortIndex)
                .ToListAsync(ct);

            var currentPosition = orderedFiles.FindIndex(f => f.Id == fileId);
            var swapPosition = currentPosition + (direction < 0 ? -1 : 1);
            if (currentPosition < 0 || swapPosition < 0 || swapPosition >= orderedFiles.Count) return false;

            // Swap the two files' SortIndex values - working from their position in the
            // freshly-ordered list (rather than assuming contiguous index values) keeps this
            // correct even when past deletions have left gaps in the numbering.
            var neighbor = orderedFiles[swapPosition];
            (target.SortIndex, neighbor.SortIndex) = (neighbor.SortIndex, target.SortIndex);

            await _context.SaveChangesAsync(ct);
            return true;
        }

        public async Task<bool> DeleteFileAsync(int fileId, string ownerId, CancellationToken ct = default)
        {
            var metadata = await _context.FileMetadata
                .FirstOrDefaultAsync(f => f.Id == fileId && f.OwnerId == ownerId && f.Category == FileCategories.Lectures && !f.IsDeleted, ct);
            if (metadata is null) return false;

            var fullPath = Path.Combine(_env.ContentRootPath, metadata.StoredPath);
            if (File.Exists(fullPath))
                File.Delete(fullPath);

            metadata.IsDeleted = true;
            metadata.DeletedAtUtc = DateTime.UtcNow;
            metadata.UpdatedAtUtc = DateTime.UtcNow;
            _context.FileMetadata.Update(metadata);
            await _context.SaveChangesAsync(ct);

            _logger.LogInformation("User {UserId} deleted lecture file {FileId}", ownerId, fileId);
            return true;
        }

        public async Task<bool> DeleteLectureAsync(int lectureId, string ownerId, CancellationToken ct = default)
        {
            var lecture = await _context.Lectures
                .FirstOrDefaultAsync(l => l.Id == lectureId && l.OwnerId == ownerId, ct);
            if (lecture is null) return false;

            var files = await _context.FileMetadata
                .Where(f => f.LectureId == lectureId)
                .ToListAsync(ct);

            foreach (var file in files)
            {
                var fullPath = Path.Combine(_env.ContentRootPath, file.StoredPath);
                if (File.Exists(fullPath))
                    File.Delete(fullPath);
            }

            _context.FileMetadata.RemoveRange(files);
            _context.Lectures.Remove(lecture);
            await _context.SaveChangesAsync(ct);

            _logger.LogInformation("User {UserId} deleted lecture {LectureId} and {FileCount} file(s)", ownerId, lectureId, files.Count);
            return true;
        }
    }
}
