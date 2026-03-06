namespace TradeSmart.Api.HostedServices;

/// <summary>
/// Self-pings the /health endpoint every 4 minutes to prevent the API from going cold
/// on hosting platforms (Azure App Service free tier, Railway, Render, etc.).
/// </summary>
public sealed class KeepAliveService : BackgroundService
{
	private static readonly TimeSpan PingInterval = TimeSpan.FromMinutes(4);

	private readonly IHttpClientFactory _httpClientFactory;
	private readonly ILogger<KeepAliveService> _logger;
	private readonly IConfiguration _configuration;

	public KeepAliveService(
		IHttpClientFactory httpClientFactory,
		IConfiguration configuration,
		ILogger<KeepAliveService> logger)
	{
		_httpClientFactory = httpClientFactory;
		_configuration = configuration;
		_logger = logger;
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		// Wait a bit for the app to fully start
		await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken).ConfigureAwait(false);

		var baseUrl = _configuration["KeepAlive:BaseUrl"];
		if (string.IsNullOrWhiteSpace(baseUrl))
		{
			// Try to auto-detect from Kestrel URLs
			baseUrl = _configuration["ASPNETCORE_URLS"]?.Split(';').FirstOrDefault()
					  ?? _configuration["urls"]?.Split(';').FirstOrDefault()
					  ?? "http://localhost:5000";
		}

		var healthUrl = $"{baseUrl.TrimEnd('/')}/health";
		_logger.LogInformation("KeepAlive service started — pinging {HealthUrl} every {Interval}",
			healthUrl, PingInterval);

		while (!stoppingToken.IsCancellationRequested)
		{
			try
			{
				var client = _httpClientFactory.CreateClient();
				client.Timeout = TimeSpan.FromSeconds(10);

				var response = await client.GetAsync(healthUrl, stoppingToken).ConfigureAwait(false);
				_logger.LogDebug("KeepAlive ping: {StatusCode}", (int)response.StatusCode);
			}
			catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
			{
				break;
			}
			catch (Exception ex)
			{
				_logger.LogWarning(ex, "KeepAlive ping failed — will retry next cycle");
			}

			try
			{
				await Task.Delay(PingInterval, stoppingToken).ConfigureAwait(false);
			}
			catch (OperationCanceledException)
			{
				break;
			}
		}

		_logger.LogInformation("KeepAlive service stopped");
	}
}
