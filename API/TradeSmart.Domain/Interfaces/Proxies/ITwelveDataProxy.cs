using TradeSmart.Domain.Entities;

namespace TradeSmart.Domain.Interfaces.Proxies;

/// <summary>Proxy for fetching market data from Twelve Data API.</summary>
public interface ITwelveDataProxy
{
	/// <summary>Fetches OHLCV time series data for a symbol and timeframe.</summary>
	/// <param name="symbol">The ticker symbol.</param>
	/// <param name="interval">The timeframe interval (e.g., "1min", "5min").</param>
	/// <param name="outputSize">Number of data points to return.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A response containing the list of OHLCV candles.</returns>
	Task<ProxyResponse<IReadOnlyList<OhlcvCandle>>> GetTimeSeriesAsync(
		string symbol,
		string interval,
		int outputSize = 50,
		CancellationToken cancellationToken = default);
}
