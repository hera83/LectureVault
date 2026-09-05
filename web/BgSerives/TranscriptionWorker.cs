using Microsoft.EntityFrameworkCore;
using web.Constants;
using web.Data;
using web.Repositories.Transcriptions.Interfaces;

namespace web.BgSerives
{
    /// <summary>
    /// Picks up queued TranscriptionJob rows and runs them via <see cref="ITranscriptionsService.ProcessJobAsync"/>,
    /// so starting a transcription doesn't tie up the request and keeps running even if the
    /// browser tab that started it is closed. Polls the DB rather than an in-memory queue, so a
    /// job survives an app restart instead of being silently dropped.
    /// </summary>
    public class TranscriptionWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<TranscriptionWorker> _logger;
        private readonly TimeSpan _interval;

        public TranscriptionWorker(IServiceScopeFactory scopeFactory, IConfiguration configuration, ILogger<TranscriptionWorker> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;

            var intervalSeconds = configuration.GetValue<int?>("Transcription:WorkerIntervalSeconds") ?? 3;
            _interval = TimeSpan.FromSeconds(intervalSeconds > 0 ? intervalSeconds : 3);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // A job stuck in "Running" at startup means the previous process was killed mid-job
            // (crash, deploy, manual stop) - requeue it rather than leaving it stranded forever
            // with no worker ever able to pick it up again.
            await RequeueOrphanedJobsAsync(stoppingToken);

            using var timer = new PeriodicTimer(_interval);

            while (true)
            {
                try
                {
                    await ProcessPendingJobsAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "TranscriptionWorker iteration failed");
                }

                try
                {
                    await timer.WaitForNextTickAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }

        private async Task RequeueOrphanedJobsAsync(CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var orphaned = await dbContext.TranscriptionJobs
                .Where(j => j.Status == TranscriptionJobStatus.Running)
                .ToListAsync(ct);
            if (orphaned.Count == 0) return;

            foreach (var job in orphaned)
            {
                job.Status = TranscriptionJobStatus.Pending;
                job.StartedAtUtc = null;
            }
            await dbContext.SaveChangesAsync(ct);

            _logger.LogWarning("Genkøede {Count} transskriptionsjob, der stod fast som 'Running' fra en tidligere proces", orphaned.Count);
        }

        private async Task ProcessPendingJobsAsync(CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var pendingJobIds = await dbContext.TranscriptionJobs
                .Where(j => j.Status == TranscriptionJobStatus.Pending)
                .OrderBy(j => j.CreatedAtUtc)
                .Select(j => j.Id)
                .ToListAsync(ct);
            if (pendingJobIds.Count == 0) return;

            // One at a time - sequential processing keeps this from hammering AiGateway with
            // several large uploads in parallel, and matches how a single "Start" run already
            // transcribes its own files one by one.
            foreach (var jobId in pendingJobIds)
            {
                using var jobScope = _scopeFactory.CreateScope();
                var transcriptionsService = jobScope.ServiceProvider.GetRequiredService<ITranscriptionsService>();

                try
                {
                    await transcriptionsService.ProcessJobAsync(jobId, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Uventet fejl under behandling af transskriptionsjob {JobId}", jobId);
                }
            }
        }
    }
}
