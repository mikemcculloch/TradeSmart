using Microsoft.Extensions.Logging;
using TradeSmart.Domain.Entities;
using TradeSmart.Domain.Interfaces.Proxies;
using TradeSmart.Domain.Interfaces.Services;

namespace TradeSmart.Domain.Services;

/// <summary>Orchestrates trade analysis by fetching multi-timeframe market data and invoking Claude.</summary>
public sealed class TradeAnalysisService : ITradeAnalysisService
{
	private readonly IClaudeProxy _claudeProxy;
	private readonly IDiscordProxy _discordProxy;
	private readonly ILogger<TradeAnalysisService> _logger;
	private readonly ITradeExecutionService _tradeExecutionService;
	private readonly ITwelveDataProxy _twelveDataProxy;

	public TradeAnalysisService(
		ITwelveDataProxy twelveDataProxy,
		IClaudeProxy claudeProxy,
		IDiscordProxy discordProxy,
		ITradeExecutionService tradeExecutionService,
		ILogger<TradeAnalysisService> logger)
	{
		_twelveDataProxy = twelveDataProxy;
		_claudeProxy = claudeProxy;
		_discordProxy = discordProxy;
		_tradeExecutionService = tradeExecutionService;
		_logger = logger;
	}

	/// <inheritdoc />
	public async Task<ProxyResponse<TradeAnalysis>> AnalyzeAsync(
		TradingViewAlert alert,
		CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(alert.Symbol))
		{
			return ProxyResponse<TradeAnalysis>.CreateError(
				Constants.ErrorCodes.INVALID_INPUT,
				"Symbol is required.");
		}

		_logger.LogInformation("Starting trade analysis for {Symbol} from {Exchange}", alert.Symbol, alert.Exchange);

		// Normalize exchange-specific symbol format for Twelve Data
		var normalizedSymbol = SymbolNormalizer.Normalize(alert.Symbol);

		if (normalizedSymbol != alert.Symbol)
		{
			_logger.LogInformation(
				"Normalized symbol from {OriginalSymbol} to {NormalizedSymbol}",
				alert.Symbol,
				normalizedSymbol);
		}

		// Fetch market data across multiple timeframes in parallel
		var marketData = await FetchMarketDataAsync(normalizedSymbol, cancellationToken).ConfigureAwait(false);

		if (marketData.Count == 0)
		{
			_logger.LogWarning("No market data available for {Symbol}", alert.Symbol);
			return ProxyResponse<TradeAnalysis>.CreateError(
				Constants.ErrorCodes.TWELVE_DATA_API_ERROR,
				$"Unable to fetch market data for {alert.Symbol}.");
		}

		_logger.LogInformation(
			"Fetched market data for {Symbol} across {TimeframeCount} timeframes",
			alert.Symbol,
			marketData.Count);

		// Send to Claude for analysis
		var analysisResponse = await _claudeProxy.AnalyzeTradeAsync(alert, marketData, cancellationToken)
			.ConfigureAwait(false);

		if (analysisResponse.HasErrors)
		{
			_logger.LogError(
				"Claude analysis failed for {Symbol}: {ErrorMessage}",
				alert.Symbol,
				analysisResponse.Error!.Message);

			return analysisResponse;
		}

		_logger.LogInformation(
			"Trade analysis complete for {Symbol}: {Direction} with {Confidence}% confidence",
			alert.Symbol,
			analysisResponse.Result!.Direction,
			analysisResponse.Result.Confidence);

		// Send Discord notification (fire-and-forget — don't fail the response)
		_ = SendDiscordNotificationAsync(alert, analysisResponse.Result, cancellationToken);

		// Evaluate for paper trade execution (fire-and-forget — don't fail the response)
		_ = ExecutePaperTradeAsync(analysisResponse.Result, cancellationToken);

		return analysisResponse;
	}

	private async Task<List<TimeframeData>> FetchMarketDataAsync(
		string symbol,
		CancellationToken cancellationToken)
	{
		var timeframes = Constants.Timeframes.DEFAULT_TIMEFRAMES;
		var tasks = timeframes.Select(tf => FetchSingleTimeframeAsync(symbol, tf, cancellationToken));
		var results = await Task.WhenAll(tasks).ConfigureAwait(false);

		return results
			.Where(r => r is not null)
			.Cast<TimeframeData>()
			.ToList();
	}

	private async Task<TimeframeData?> FetchSingleTimeframeAsync(
		string symbol,
		string timeframe,
		CancellationToken cancellationToken)
	{
		var response = await _twelveDataProxy.GetTimeSeriesAsync(symbol, timeframe, outputSize: 50, cancellationToken)
			.ConfigureAwait(false);

		if (response.HasErrors)
		{
			_logger.LogWarning(
				"Failed to fetch {Timeframe} data for {Symbol}: {ErrorMessage}",
				timeframe,
				symbol,
				response.Error!.Message);

			return null;
		}

		return new TimeframeData
		{
			Timeframe = timeframe,
			Candles = response.Result ?? []
		};
	}

	private async Task ExecutePaperTradeAsync(
		TradeAnalysis analysis,
		CancellationToken cancellationToken)
	{
		try
		{
			var result = await _tradeExecutionService.ExecuteAsync(analysis, cancellationToken)
				.ConfigureAwait(false);

			if (result.HasErrors)
			{
				_logger.LogWarning(
					"Trade execution evaluation failed for {Symbol}: {ErrorMessage}",
					analysis.Symbol,
					result.Error!.Message);
				return;
			}

			if (result.Result!.TradeOpened)
			{
				_logger.LogInformation(
					"Paper trade opened for {Symbol}: {Direction} at {EntryPrice}, Position ID: {PositionId}",
					analysis.Symbol,
					result.Result.Position!.Direction,
					result.Result.Position.EntryPrice,
					result.Result.Position.PositionId);
			}
			else
			{
				_logger.LogInformation(
					"Paper trade not opened for {Symbol}: {RejectionReason}",
					analysis.Symbol,
					result.Result.RejectionReason);
			}
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Unhandled error during paper trade execution for {Symbol}", analysis.Symbol);
		}
	}

	private async Task SendDiscordNotificationAsync(
		TradingViewAlert alert,
		TradeAnalysis analysis,
		CancellationToken cancellationToken)
	{
		try
		{
			var result = await _discordProxy.SendTradeNotificationAsync(alert, analysis, cancellationToken)
				.ConfigureAwait(false);

			if (result.HasErrors)
			{
				_logger.LogWarning(
					"Discord notification failed for {Symbol}: {ErrorMessage}",
					analysis.Symbol,
					result.Error!.Message);
			}
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Unhandled error sending Discord notification for {Symbol}", analysis.Symbol);
		}
	}
}
