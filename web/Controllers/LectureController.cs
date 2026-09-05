using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using web.Constants;
using web.Data.Entities;
using web.Infrastructure;
using web.Repositories.Lectures.Interfaces;
using web.Repositories.Transcriptions.Interfaces;
using web.Services.AiGateway;
using web.Services.AiGateway.Interfaces;
using web.ViewModels;

namespace web.Controllers
{
    [Authorize]
    public class LectureController : Controller
    {
        // Content types the browser may report for common lecture recording formats.
        private static readonly string[] AllowedAudioContentTypes =
        [
            "audio/mpeg", "audio/mp3", "audio/wav", "audio/x-wav", "audio/wave", "audio/vnd.wave",
            "audio/mp4", "audio/x-m4a", "audio/m4a", "audio/aac", "audio/x-aac",
            "audio/ogg", "audio/vorbis", "audio/opus", "audio/webm", "audio/flac", "audio/x-flac",
            "audio/3gpp", "audio/3gpp2", "audio/amr"
        ];

        // Fallback check by extension, since some browsers report generic content types
        // (e.g. "application/octet-stream") for less common audio formats.
        private static readonly string[] AllowedAudioExtensions =
        [
            ".mp3", ".wav", ".m4a", ".aac", ".ogg", ".oga", ".flac", ".wma", ".webm", ".opus", ".3gp", ".amr"
        ];

        private const long MaxAudioFileBytes = 750L * 1024 * 1024; // 750 MB pr. fil

        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILecturesService _lecturesService;
        private readonly ITranscriptionsService _transcriptionsService;
        private readonly IAiGatewayService _aiGatewayService;
        private readonly IAiGatewayConfigurationProvider _aiGatewayConfigurationProvider;
        private readonly ILogger<LectureController> _logger;

        public LectureController(
            UserManager<ApplicationUser> userManager,
            ILecturesService lecturesService,
            ITranscriptionsService transcriptionsService,
            IAiGatewayService aiGatewayService,
            IAiGatewayConfigurationProvider aiGatewayConfigurationProvider,
            ILogger<LectureController> logger)
        {
            _userManager = userManager;
            _lecturesService = lecturesService;
            _transcriptionsService = transcriptionsService;
            _aiGatewayService = aiGatewayService;
            _aiGatewayConfigurationProvider = aiGatewayConfigurationProvider;
            _logger = logger;
        }

        private static bool IsAudioFile(IFormFile file)
        {
            if (!string.IsNullOrEmpty(file.ContentType) && AllowedAudioContentTypes.Contains(file.ContentType.ToLowerInvariant()))
                return true;

            var ext = Path.GetExtension(file.FileName);
            return !string.IsNullOrEmpty(ext) && AllowedAudioExtensions.Contains(ext.ToLowerInvariant());
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId)) return RedirectToAction("Login", "Account");

            var details = await _lecturesService.GetLectureDetailsAsync(id, userId, HttpContext.RequestAborted);
            if (details is null) return NotFound();

            var vm = new LectureDetailsViewModel
            {
                Id = details.Id,
                Name = details.Name,
                CreatedAtUtc = details.CreatedAtUtc,
                Files = details.Files.Select(f => new LectureFileViewModel
                {
                    Id = f.Id,
                    OriginalFileName = f.OriginalFileName,
                    FileSizeBytes = f.FileSizeBytes,
                    ContentType = f.ContentType,
                    CreatedAtUtc = f.CreatedAtUtc
                }).ToList()
            };
            vm.Transcription = await BuildTranscriptionSectionAsync(id, userId, vm.Files.Count > 0, null, HttpContext.RequestAborted);

            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> TranscriptionSection(int id, int? version)
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var details = await _lecturesService.GetLectureDetailsAsync(id, userId, HttpContext.RequestAborted);
            if (details is null) return NotFound();

            var vm = await BuildTranscriptionSectionAsync(id, userId, details.Files.Count > 0, version, HttpContext.RequestAborted);
            return PartialView("_TranscriptionSection", vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> StartTranscription(int id, string model, string? language)
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            if (string.IsNullOrWhiteSpace(model))
                return this.ToastErrorJson("Vælg en model at transskribere med.");

            if (!TranscriptionLanguages.IsKnownCode(language))
                return this.ToastErrorJson("Ugyldigt sprog valgt.");

            var job = await _transcriptionsService.EnqueueJobAsync(id, userId, model, language, HttpContext.RequestAborted);
            if (job is null)
                return this.ToastErrorJson("Lektionen har ingen filer at transskribere, eller findes ikke.");

            return this.ToastSuccessJson("Transskription er startet i baggrunden. Du kan roligt lukke siden - resultatet er her, når du kommer tilbage.");
        }

        [HttpGet]
        public async Task<IActionResult> JobStatus(int jobId)
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var job = await _transcriptionsService.GetJobStatusAsync(jobId, userId, HttpContext.RequestAborted);
            if (job is null) return NotFound();

            return Json(new
            {
                status = job.Status,
                active = job.Status is TranscriptionJobStatus.Pending or TranscriptionJobStatus.Running,
                resultVersionNumber = job.ResultVersionNumber,
                errorMessage = job.ErrorMessage
            });
        }

        private async Task<TranscriptionSectionViewModel> BuildTranscriptionSectionAsync(int lectureId, string ownerId, bool hasFiles, int? versionNumber, CancellationToken ct)
        {
            var vm = new TranscriptionSectionViewModel
            {
                LectureId = lectureId,
                HasFiles = hasFiles
            };

            const string SpeechToTextTask = "automatic-speech-recognition";

            try
            {
                var models = (await _aiGatewayService.SpeachesListModelsAsync(SpeechToTextTask, ct)).Data ?? [];
                vm.AvailableModels = models
                    .Where(m => !string.IsNullOrEmpty(m.Id) && string.Equals(m.Task, SpeechToTextTask, StringComparison.OrdinalIgnoreCase))
                    .GroupBy(m => m.Id!)
                    .Select(g => new TranscriptionModelOptionViewModel
                    {
                        Id = g.Key,
                        LanguageCodes = g.SelectMany(m => m.Language ?? [])
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .OrderBy(l => l)
                            .ToList()
                    })
                    .OrderBy(m => m.Id)
                    .ToList();
            }
            catch (Exception ex) when (ex is AiGatewayException or HttpRequestException or TaskCanceledException)
            {
                vm.AiGatewayOnline = false;
                _logger.LogWarning(ex, "Kunne ikke hente Speaches STT-modeller fra AiGateway for lektion {LectureId}", lectureId);
            }

            var activeJob = await _transcriptionsService.GetActiveJobAsync(lectureId, ownerId, ct);
            vm.ActiveJobId = activeJob?.Id;

            var summaries = await _transcriptionsService.GetVersionSummariesAsync(lectureId, ownerId, ct);
            vm.VersionSummaries = summaries.Select(s => new TranscriptionVersionSummaryViewModel
            {
                VersionNumber = s.VersionNumber,
                CreatedAtUtc = s.CreatedAtUtc
            }).ToList();

            var version = await _transcriptionsService.GetVersionAsync(lectureId, ownerId, versionNumber, ct);
            if (version is not null)
            {
                vm.CurrentVersion = new TranscriptionVersionViewModel
                {
                    VersionNumber = version.VersionNumber,
                    Model = version.Model,
                    Language = version.Language,
                    CreatedAtUtc = version.CreatedAtUtc,
                    Segments = version.Segments.Select(s => new TranscriptionSegmentViewModel
                    {
                        OriginalFileName = s.OriginalFileName,
                        Text = s.Text,
                        Success = s.Success,
                        ErrorMessage = s.ErrorMessage
                    }).ToList()
                };
                vm.SelectedModel = version.Model;
                vm.SelectedLanguage = version.Language ?? "";
            }
            else
            {
                var config = await _aiGatewayConfigurationProvider.GetActiveConfigurationAsync(ct);
                vm.SelectedModel = !string.IsNullOrWhiteSpace(config.DefaultSttModel) && vm.AvailableModels.Any(m => m.Id == config.DefaultSttModel)
                    ? config.DefaultSttModel
                    : vm.AvailableModels.FirstOrDefault()?.Id;

                var supportedLanguages = vm.AvailableModels.FirstOrDefault(m => m.Id == vm.SelectedModel)?.LanguageCodes ?? [];
                vm.SelectedLanguage = supportedLanguages.Contains("da", StringComparer.OrdinalIgnoreCase) ? "da" : "";
            }

            return vm;
        }

        [HttpGet]
        public async Task<IActionResult> FilesTable(int id)
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var details = await _lecturesService.GetLectureDetailsAsync(id, userId, HttpContext.RequestAborted);
            if (details is null) return NotFound();

            var vm = new LectureDetailsViewModel
            {
                Id = details.Id,
                Name = details.Name,
                CreatedAtUtc = details.CreatedAtUtc,
                Files = details.Files.Select(f => new LectureFileViewModel
                {
                    Id = f.Id,
                    OriginalFileName = f.OriginalFileName,
                    FileSizeBytes = f.FileSizeBytes,
                    ContentType = f.ContentType,
                    CreatedAtUtc = f.CreatedAtUtc
                }).ToList()
            };

            return PartialView("_LectureFilesTableBody", vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(2_000_000_000)]
        [RequestFormLimits(MultipartBodyLengthLimit = 2_000_000_000)]
        public async Task<IActionResult> Upload(int id, List<IFormFile> files)
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            if (files is null || files.Count == 0)
                return this.ToastErrorJson("Ingen filer modtaget.");

            var acceptedCount = 0;
            var rejectedNames = new List<string>();

            foreach (var file in files)
            {
                if (file.Length == 0) continue;

                if (!IsAudioFile(file))
                {
                    rejectedNames.Add(file.FileName);
                    continue;
                }

                if (file.Length > MaxAudioFileBytes)
                {
                    rejectedNames.Add($"{file.FileName} (for stor)");
                    continue;
                }

                await using var stream = file.OpenReadStream();
                var saved = await _lecturesService.AddFileAsync(id, userId, stream, file.ContentType, file.FileName, HttpContext.RequestAborted);
                if (saved is null)
                {
                    // Lecture not found, or not owned by this user — no point trying the rest.
                    return NotFound();
                }

                acceptedCount++;
            }

            if (acceptedCount == 0)
            {
                return this.ToastErrorJson("Ingen filer blev uploadet. Kun lydfiler accepteres til transskription.");
            }

            var message = acceptedCount == 1 ? "1 fil uploadet." : $"{acceptedCount} filer uploadet.";
            if (rejectedNames.Count > 0)
            {
                message += $" {rejectedNames.Count} sprunget over (ikke en lydfil): {string.Join(", ", rejectedNames)}.";
                return this.ToastWarningJson(message);
            }

            return this.ToastSuccessJson(message);
        }

        [HttpGet]
        public async Task<IActionResult> Download(int fileId)
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var result = await _lecturesService.GetFileForDownloadAsync(fileId, userId, HttpContext.RequestAborted);
            if (result is null) return NotFound();

            var contentType = string.IsNullOrWhiteSpace(result.Value.ContentType) ? "application/octet-stream" : result.Value.ContentType;
            return PhysicalFile(result.Value.FullPath, contentType, result.Value.OriginalFileName);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MoveFileUp(int fileId)
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            await _lecturesService.MoveFileAsync(fileId, userId, -1, HttpContext.RequestAborted);
            return Json(new { success = true }); // no toast for a simple reorder click; already-at-top is a harmless no-op
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MoveFileDown(int fileId)
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            await _lecturesService.MoveFileAsync(fileId, userId, 1, HttpContext.RequestAborted);
            return Json(new { success = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteFile(int fileId)
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var deleted = await _lecturesService.DeleteFileAsync(fileId, userId, HttpContext.RequestAborted);
            return deleted
                ? this.ToastSuccessJson("Filen er slettet.")
                : this.ToastErrorJson("Filen kunne ikke slettes.");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId)) return RedirectToAction("Login", "Account");

            var deleted = await _lecturesService.DeleteLectureAsync(id, userId, HttpContext.RequestAborted);
            if (!deleted)
            {
                this.ToastError("Lektionen kunne ikke slettes.");
                return RedirectToAction("Details", new { id });
            }

            this.ToastSuccess("Lektionen er slettet.");
            return RedirectToAction("Index", "Home");
        }
    }
}
