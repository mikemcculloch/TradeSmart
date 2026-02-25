using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TradeSmart.Domain;
using TradeSmart.Domain.Entities;
using TradeSmart.Domain.Interfaces.Proxies;

namespace TradeSmart.Infrastructure.Proxy;

/// <summary>Proxy for fetching OHLCV market data from the Twelve Data API.</summary>
public sealed class TwelveDataProxy : ITwelveDataProxy
{
	private readonly IConfiguration _configuration;
	private readonly IHttpClientFactory _httpClientFactory;
	private readonly ILogger<TwelveDataProxy> _logger;

	public TwelveDataProxy(
		IHttpClientFactory httpClientFactory,
		IConfiguration configuration,
		ILogger<TwelveDataProxy> logger)
	{
		_httpClientFactory = httpClientFactory;
		_configuration = configuration;
		_logger = logger;
	}

	/// <inheritdoc />
	public async Task<ProxyResponse<IReadOnlyList<OhlcvCandle>>> GetTimeSeriesAsync(
		string symbol,
		string interval,
		int outputSize = 50,
		CancellationToken cancellationToken = default)
	{
		var client = _httpClientFactory.CreateClient(Constants.TWELVE_DATA_HTTP_CLIENT_NAME);
		var apiKey = _configuration.GetTwelveDataApiKey();
		var baseUrl = _configuration.GetTwelveDataBaseUrl();
		var url = $"{baseUrl}/time_series?symbol={symbol}&interval={interval}&outputsize={outputSize}&apikey={apiKey}";

		try
		{
			var httpResponse = await client.GetAsync(url, cancellationToken).ConfigureAwait(false);

			if (!httpResponse.IsSuccessStatusCode)
			{
				_logger.LogError(
					"Twelve Data API returned {StatusCode} for {Symbol} {Interval}",
					httpResponse.StatusCode,
					symbol,
					interval);

				return ProxyResponse<IReadOnlyList<OhlcvCandle>>.CreateError(
					Constants.ErrorCodes.TWELVE_DATA_API_ERROR,
					$"Twelve Data API returned {httpResponse.StatusCode}.");
			}

			var json = await httpResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
			var root = JObject.Parse(json);

			// Check for API-level errors (Twelve Data returns 200 with error status)
			if (root["status"]?.ToString() == "error")
			{
				var errorMessage = root["message"]?.ToString() ?? "Unknown Twelve Data error";
				_logger.LogError("Twelve Data API error for {Symbol} {Interval}: {ErrorMessage}", symbol, interval, errorMessage);

				return ProxyResponse<IReadOnlyList<OhlcvCandle>>.CreateError(
					Constants.ErrorCodes.TWELVE_DATA_API_ERROR,
					errorMessage);
			}

			var valuesArray = root["values"];
			if (valuesArray is null)
			{
				_logger.LogWarning("No values returned from Twelve Data for {Symbol} {Interval}", symbol, interval);
				return ProxyResponse<IReadOnlyList<OhlcvCandle>>.Success([]);
			}

			var candles = new List<OhlcvCandle>();
			foreach (var item in valuesArray)
			{
				candles.Add(new OhlcvCandle
				{
					Datetime = DateTimeOffset.Parse(item["datetime"]?.ToString() ?? string.Empty),
					Open = decimal.Parse(item["open"]?.ToString() ?? "0"),
					High = decimal.Parse(item["high"]?.ToString() ?? "0"),
					Low = decimal.Parse(item["low"]?.ToString() ?? "0"),
					Close = decimal.Parse(item["close"]?.ToString() ?? "0"),
					Volume = long.Parse(item["volume"]?.ToString() ?? "0")
				});
			}

			return ProxyResponse<IReadOnlyList<OhlcvCandle>>.Success(candles);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Exception calling Twelve Data API for {Symbol} {Interval}", symbol, interval);

			return ProxyResponse<IReadOnlyList<OhlcvCandle>>.CreateError(
				Constants.ErrorCodes.TWELVE_DATA_API_ERROR,
				$"Failed to fetch market data: {ex.Message}");
		}
	}
}
