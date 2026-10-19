using DentalLab.Services;

namespace DentalLab.Services
{
    /// <summary>
    /// Runs once a minute and triggers a dated backup when local time matches the configured schedule,
    /// at most once per local calendar day.
    /// </summary>
    public class AutoBackupHostedService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<AutoBackupHostedService> _logger;

        public AutoBackupHostedService(IServiceScopeFactory scopeFactory, ILogger<AutoBackupHostedService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Delay startup a bit so migrations can finish
            try { await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken); }
            catch (OperationCanceledException) { return; }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await MaybeRunAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Auto backup tick failed");
                }

                try { await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); }
                catch (OperationCanceledException) { break; }
            }
        }

        private async Task MaybeRunAsync(CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var backup = scope.ServiceProvider.GetRequiredService<BackupService>();
            var settings = await backup.GetOrCreateSettingsAsync();

            if (!settings.AutoEnabled)
                return;

            var now = DateTime.Now;
            var today = now.ToString("yyyy-MM-dd");
            if (string.Equals(settings.LastAutoBackupDate, today, StringComparison.Ordinal))
                return;

            if (now.Hour != settings.AutoHour || now.Minute != settings.AutoMinute)
                return;

            _logger.LogInformation("Starting scheduled auto backup at {Time}", now);
            var result = await backup.CreateBackupAsync(isAuto: true);
            if (!result.Ok)
                _logger.LogWarning("Auto backup failed: {Message}", result.Message);
        }
    }
}
