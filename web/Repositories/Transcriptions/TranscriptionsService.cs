using Microsoft.EntityFrameworkCore;
using web.Constants;
using web.Data;
using web.Data.Entities;
using web.Infrastructure;
using web.Repositories.Transcriptions.Dtos;
using web.Repositories.Transcriptions.Interfaces;
using web.Services.AiGateway;
using web.Services.AiGateway.Dtos.Speaches;
using web.Services.AiGateway.Interfaces;

namespace web.Repositories.Transcriptions
{
    public class TranscriptionsService : ITranscriptionsService
    {
        private readonly ApplicationDbContext _context;
        private readonly IAiGatewayService _aiGatewayService;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<TranscriptionsService> _logger;

        public TranscriptionsService(
            ApplicationDbContext context,
            IAiGatewayService aiGatewayService,
            IWebHostEnvironment env,
            ILogger<TranscriptionsService> logger)
        {
            _context = context;
            _aiGatewayService = aiGatewayService;
            _env = env;
            _logger = logger;
        }

        public async Task<List<TranscriptionVersionSummaryDto>> GetVersionSummariesAsync(int lectureId, string ownerId, CancellationToken ct = default)
        {
            var ownsLecture = await _context.Lectures.AsNoTracking().AnyAsync(l => l.Id == lectureId && l.OwnerId == ownerId, ct);
            if (!ownsLecture) return [];

            return await _context.TranscriptionVersions
                .AsNoTracking()
                .Where(v => v.LectureId == lectureId)
                .OrderByDescending(v => v.VersionNumber)
                .Select(v => new TranscriptionVersionSummaryDto
                {
                    VersionNumber = v.VersionNumber,
                    CreatedAtUtc = v.CreatedAtUtc
                })
                .ToListAsync(ct);
        }

        public async Task<TranscriptionVersionDto?> GetVersionAsync(int lectureId, string ownerId, int? versionNumber, CancellationToken ct = default)
        {
            var ownsLecture = await _context.Lectures.AsNoTracking().AnyAsync(l => l.Id == lectureId && l.OwnerId == ownerId, ct);
            if (!ownsLecture) return null;

            var query = _context.TranscriptionVersions
                .AsNoTracking()
                .Where(v => v.LectureId == lectureId);

            var version = versionNumber.HasValue
                ? await query.FirstOrDefaultAsync(v => v.VersionNumber == versionNumber.Value, ct)
                : await query.OrderByDescending(v => v.VersionNumber).FirstOrDefaultAsync(ct);

            if (version is null) return null;

            var segments = await _context.TranscriptionSegments
                .AsNoTracking()
                .Where(s => s.TranscriptionVersionId == version.Id)
                .OrderBy(s => s.Id)
                .Select(s => new Dtos.TranscriptionSegmentDto
                {
                    OriginalFileName = s.OriginalFileName,
                    Text = s.Text,
                    Success = s.Success,
                    ErrorMessage = s.ErrorMessage
                })
                .ToListAsync(ct);

            return new TranscriptionVersionDto
            {
                VersionNumber = version.VersionNumber,
                Model = version.Model,
                Language = version.Language,
                CreatedAtUtc = version.CreatedAtUtc,
                Segments = segments
            };
        }

        public async Task<TranscriptionJobDto?> EnqueueJobAsync(int lectureId, string ownerId, string model, string? language, CancellationToken ct = default)
        {
            var lecture = await _context.Lectures
                .AsNoTracking()
                .FirstOrDefaultAsync(l => l.Id == lectureId && l.OwnerId == ownerId, ct);
            if (lecture is null) return null;

            var activeJob = await _context.TranscriptionJobs
                .Where(j => j.LectureId == lectureId && (j.Status == TranscriptionJobStatus.Pending || j.Status == TranscriptionJobStatus.Running))
                .FirstOrDefaultAsync(ct);
            if (activeJob is not null) return ToDto(activeJob);

            var hasFiles = await _context.FileMetadata.AnyAsync(f => f.LectureId == lectureId && !f.IsDeleted, ct);
            if (!hasFiles) return null;

            var job = new TranscriptionJob
            {
                LectureId = lectureId,
                OwnerId = ownerId,
                Model = model,
                Language = string.IsNullOrWhiteSpace(language) ? null : language,
                Status = TranscriptionJobStatus.Pending,
                CreatedAtUtc = DateTime.UtcNow
            };
            _context.TranscriptionJobs.Add(job);
            await _context.SaveChangesAsync(ct);

            _logger.LogInformation("User {UserId} queued transcription job {JobId} for lecture {LectureId} (model {Model})", ownerId, job.Id, lectureId, model);
            return ToDto(job);
        }

        public async Task<TranscriptionJobDto?> GetActiveJobAsync(int lectureId, string ownerId, CancellationToken ct = default)
        {
            var job = await _context.TranscriptionJobs
                .AsNoTracking()
                .Where(j => j.LectureId == lectureId && j.OwnerId == ownerId
                    && (j.Status == TranscriptionJobStatus.Pending || j.Status == TranscriptionJobStatus.Running))
                .FirstOrDefaultAsync(ct);
            return job is null ? null : ToDto(job);
        }

        public async Task<TranscriptionJobDto?> GetJobStatusAsync(int jobId, string ownerId, CancellationToken ct = default)
        {
            var job = await _context.TranscriptionJobs
                .AsNoTracking()
                .FirstOrDefaultAsync(j => j.Id == jobId && j.OwnerId == ownerId, ct);
            return job is null ? null : ToDto(job);
        }

        public async Task ProcessJobAsync(int jobId, CancellationToken ct = default)
        {
            var job = await _context.TranscriptionJobs.FirstOrDefaultAsync(j => j.Id == jobId, ct);
            if (job is null || job.Status != TranscriptionJobStatus.Pending) return;

            job.Status = TranscriptionJobStatus.Running;
            job.StartedAtUtc = DateTime.UtcNow;
            await _context.SaveChangesAsync(ct);

            try
            {
                var files = await _context.FileMetadata
                    .AsNoTracking()
                    .Where(f => f.LectureId == job.LectureId && !f.IsDeleted)
                    .OrderBy(f => f.SortIndex)
                    .ToListAsync(ct);

                var nextVersionNumber = 1 + await _context.TranscriptionVersions
                    .Where(v => v.LectureId == job.LectureId)
                    .Select(v => (int?)v.VersionNumber)
                    .MaxAsync(ct) ?? 1;

                var version = new TranscriptionVersion
                {
                    LectureId = job.LectureId,
                    VersionNumber = nextVersionNumber,
                    Model = job.Model,
                    Language = job.Language,
                    CreatedAtUtc = DateTime.UtcNow
                };
                _context.TranscriptionVersions.Add(version);
                await _context.SaveChangesAsync(ct);

                var segments = new List<TranscriptionSegment>();
                foreach (var file in files)
                {
                    var segment = new TranscriptionSegment
                    {
                        TranscriptionVersionId = version.Id,
                        FileMetadataId = file.Id,
                        OriginalFileName = file.OriginalFileName,
                        CreatedAtUtc = DateTime.UtcNow
                    };

                    try
                    {
                        segment.Text = await TranscribeSingleFileAsync(file, job.Model, job.Language, ct);
                        segment.Success = true;
                    }
                    catch (AiGatewayException ex)
                    {
                        // ex.Message alone is often just the ASP.NET Core boilerplate title
                        // ("One or more validation errors occurred") - the actually useful detail
                        // is per-field, in ex.Errors, so that has to be folded in explicitly or it's
                        // silently lost and the user is left staring at a meaningless message.
                        segment.Success = false;
                        segment.ErrorMessage = FormatAiGatewayError(ex);
                        _logger.LogWarning(ex, "Transskription fejlede for fil {FileId} ({FileName}) i lektion {LectureId}: {ErrorDetails}",
                            file.Id, file.OriginalFileName, job.LectureId, segment.ErrorMessage);
                    }
                    catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
                    {
                        segment.Success = false;
                        segment.ErrorMessage = "Kunne ikke kontakte AiGateway.";
                        _logger.LogWarning(ex, "Kunne ikke kontakte AiGateway for fil {FileId} ({FileName}) i lektion {LectureId}", file.Id, file.OriginalFileName, job.LectureId);
                    }
                    catch (FileNotFoundException)
                    {
                        segment.Success = false;
                        segment.ErrorMessage = "Filen findes ikke længere på disk.";
                    }

                    segments.Add(segment);
                }

                _context.TranscriptionSegments.AddRange(segments);

                job.Status = TranscriptionJobStatus.Completed;
                job.ResultVersionNumber = version.VersionNumber;
                job.CompletedAtUtc = DateTime.UtcNow;
                await _context.SaveChangesAsync(ct);

                _logger.LogInformation(
                    "Job {JobId}: transskriberede lektion {LectureId} til version {VersionNumber}, {FileCount} fil(er), model {Model}",
                    job.Id, job.LectureId, version.VersionNumber, files.Count, job.Model);
            }
            catch (Exception ex)
            {
                // Anything that escapes the per-file try/catch above is a job-level failure (e.g.
                // a DB error) rather than a single file being untranscribable - make sure the job
                // still ends up in a terminal state so the UI (and the poller) don't wait forever.
                job.Status = TranscriptionJobStatus.Failed;
                job.ErrorMessage = ex.Message;
                job.CompletedAtUtc = DateTime.UtcNow;
                await _context.SaveChangesAsync(ct);

                _logger.LogError(ex, "Transskriptionsjob {JobId} for lektion {LectureId} fejlede", job.Id, job.LectureId);
            }
        }

        private static TranscriptionJobDto ToDto(TranscriptionJob job) => new()
        {
            Id = job.Id,
            LectureId = job.LectureId,
            Status = job.Status,
            ResultVersionNumber = job.ResultVersionNumber,
            ErrorMessage = job.ErrorMessage
        };

        private static string FormatAiGatewayError(AiGatewayException ex)
        {
            string combined;
            if (ex.Errors is { Count: > 0 })
            {
                // ASP.NET Core reports request-level (not field-specific) errors under an empty
                // key - skip the "<key>: " prefix in that case, it would just read ": <message>".
                var fieldDetails = string.Join("; ", ex.Errors.Select(kv =>
                    string.IsNullOrEmpty(kv.Key) ? string.Join(", ", kv.Value) : $"{kv.Key}: {string.Join(", ", kv.Value)}"));
                combined = $"{ex.Message} ({fieldDetails})";
            }
            else
            {
                combined = ex.Message;
            }

            // This is Kestrel's own request body size cap on the AiGateway server (currently
            // configured there as ~300 MB), not anything configurable from this app. Give a
            // plain-language explanation instead of the raw ASP.NET Core wording.
            if (combined.Contains("request body too large", StringComparison.OrdinalIgnoreCase))
            {
                return "Filen er for stor til AiGateway (grænsen for én transskriptionsanmodning er typisk omkring 300 MB på serveren). " +
                       "Prøv en komprimeret lydfil (mp3, m4a, ogg) i stedet, eller få grænsen hævet yderligere på AiGateway-serveren.";
            }

            return combined;
        }

        private async Task<string> TranscribeSingleFileAsync(FileMetadata file, string model, string? language, CancellationToken ct)
        {
            var fullPath = Path.Combine(_env.ContentRootPath, file.StoredPath);
            if (!File.Exists(fullPath))
                throw new FileNotFoundException(fullPath);

            await using var stream = File.OpenRead(fullPath);
            return await TranscribeStreamAsync(stream, file.OriginalFileName, file.ContentType, model, language, ct);
        }

        private async Task<string> TranscribeStreamAsync(Stream stream, string fileName, string contentType, string model, string? language, CancellationToken ct)
        {
            var result = await _aiGatewayService.SpeachesTranscribeAsync(new TranscribeRequestDto
            {
                Model = model,
                FileContent = stream,
                FileName = fileName,
                ContentType = contentType,
                Language = string.IsNullOrWhiteSpace(language) ? null : language
            }, ct);

            return result.Text?.Trim() ?? string.Empty;
        }
    }
}
