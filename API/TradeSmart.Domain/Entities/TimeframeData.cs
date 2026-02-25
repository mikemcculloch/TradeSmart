namespace TradeSmart.Domain.Entities;

/// <summary>Market data for a specific timeframe.</summary>
public sealed record TimeframeData
{
	/// <summary>The timeframe interval (e.g., "1min", "5min", "1h").</summary>
	public string Timeframe { get; init; } = string.Empty;

	/// <summary>The OHLCV candles in this timeframe.</summary>
	public IReadOnlyList<OhlcvCandle> Candles { get; init; } = [];
}
