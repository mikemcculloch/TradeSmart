using TradeSmart.Domain.Entities;
using TradeSmart.Domain.Interfaces.Proxies;
using TradeSmart.Domain.Interfaces.Services;

namespace TradeSmart.Api.HostedServices;

/// <summary>
/// Background service that polls current prices for open paper positions
/// and closes them when stop-loss or take-profit is hit.
/// </summary>
public sealed class TradeMonitorService : BackgroundService
{
	private readonly IConfiguration _configuration;
	private readonly ILogger<TradeMonitorService> _logger;
	private readonly IPaperTradingService _paperTradingService;
	private readonly IServiceScopeFactory _scopeFactory;

	public TradeMonitorService(
		IServiceScopeFactory scopeFactory,
		IPaperTradingService paperTradingService,
		IConfiguration configuration,
		ILogger<TradeMonitorService> logger)
	{
		_scopeFactory = scopeFactory;
		_paperTradingService = paperTradingService;
		_configuration = configuration;
		_logger = logger;
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		if (!_configuration.GetPaperTradingEnabled())
		{
			_logger.LogInformation("Paper trading is disabled — trade monitor will not start");
			return;
		}

		var intervalSeconds = _configuration.GetPaperTradingMonitorIntervalSeconds();
		_logger.LogInformation("Trade monitor started, polling every {IntervalSeconds}s", intervalSeconds);

		while (!stoppingToken.IsCancellationRequested)
		{
			try
			{
				await MonitorPositionsAsync(stoppingToken).ConfigureAwait(false);
			}
			catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
			{
				_logger.LogInformation("Trade monitor stopping due to application shutdown");
				break;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Unhandled error in trade monitor loop — will retry next cycle");
			}

			try
			{
				await Task.Delay(
					TimeSpan.FromSeconds(intervalSeconds),
					stoppingToken).ConfigureAwait(false);
			}
			catch (OperationCanceledException)
			{
				break;
			}
		}
	}

	private async Task MonitorPositionsAsync(CancellationToken stoppingToken)
	{
		var openPositions = _paperTradingService.GetOpenPositions();

		if (openPositions.Count == 0)
		{
			_logger.LogDebug("No open positions to monitor");
			return;
		}

		_logger.LogDebug("Monitoring {PositionCount} open position(s)", openPositions.Count);

		using var scope = _scopeFactory.CreateScope();
		var twelveDataProxy = scope.ServiceProvider.GetRequiredService<ITwelveDataProxy>();
		var discordProxy = scope.ServiceProvider.GetRequiredService<IDiscordProxy>();

		foreach (var position in openPositions)
		{
			try
			{
				await EvaluatePositionAsync(position, twelveDataProxy, discordProxy, stoppingToken)
					.ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error evaluating position {PositionId} for {Symbol}",
					position.PositionId, position.Symbol);
			}
		}
	}

	private async Task EvaluatePositionAsync(
		PaperPosition position,
		ITwelveDataProxy twelveDataProxy,
		IDiscordProxy discordProxy,
		CancellationToken stoppingToken)
	{
		// Fetch latest price
		var priceResponse = await twelveDataProxy.GetTimeSeriesAsync(
			position.Symbol, "1min", outputSize: 1, stoppingToken).ConfigureAwait(false);

		if (priceResponse.HasErrors || priceResponse.Result is null || priceResponse.Result.Count == 0)
		{
			_logger.LogWarning(
				"Could not fetch price for {Symbol}: {Error}. Skipping this cycle.",
				position.Symbol,
				priceResponse.Error?.Message ?? "No data returned");
			return;
		}

		var latestCandle = priceResponse.Result[0];
		var currentPrice = latestCandle.Close;

		// Check for stale data
		var staleness = DateTimeOffset.UtcNow - latestCandle.Datetime;
		if (staleness > TimeSpan.FromMinutes(5))
		{
			_logger.LogWarning(
				"Price data for {Symbol} is {StaleMinutes:F1} minutes old — market may be closed",
				position.Symbol,
				staleness.TotalMinutes);
		}

		// Evaluate SL/TP
		string? closeReason = null;

		if (position.Direction == TradeDirection.Long)
		{
			if (currentPrice <= position.StopLoss)
				closeReason = CloseReason.StopLoss;
			else if (currentPrice >= position.TakeProfit)
				closeReason = CloseReason.TakeProfit;
		}
		else // Short
		{
			if (currentPrice >= position.StopLoss)
				closeReason = CloseReason.StopLoss;
			else if (currentPrice <= position.TakeProfit)
				closeReason = CloseReason.TakeProfit;
		}

		if (closeReason is null)
		{
			return;
		}

		_logger.LogInformation(
			"Position {PositionId} for {Symbol} hit {CloseReason} at {CurrentPrice}",
			position.PositionId,
			position.Symbol,
			closeReason,
			currentPrice);

		var closeResult = await _paperTradingService.ClosePositionAsync(
			position.PositionId, currentPrice, closeReason, stoppingToken).ConfigureAwait(false);

		if (closeResult.HasErrors)
		{
			_logger.LogError(
				"Failed to close position {PositionId} for {Symbol}: {ErrorMessage}",
				position.PositionId,
				position.Symbol,
				closeResult.Error!.Message);
			return;
		}

		// Send Discord notification (fire-and-forget style within this iteration)
		try
		{
			await discordProxy.SendTradeClosedNotificationAsync(
				closeResult.Result!.ClosedPosition,
				closeResult.Result.UpdatedWallet,
				stoppingToken).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Failed to send trade-closed Discord notification for {Symbol}", position.Symbol);
		}
	}
}
