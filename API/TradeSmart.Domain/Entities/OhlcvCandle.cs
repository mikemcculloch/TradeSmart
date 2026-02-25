namespace TradeSmart.Domain.Entities;

/// <summary>A single OHLCV candle from market data.</summary>
public sealed record OhlcvCandle
{
	/// <summary>Candle open time.</summary>
	public DateTimeOffset Datetime { get; init; }

	/// <summary>Open price.</summary>
	public decimal Open { get; init; }

	/// <summary>High price.</summary>
	public decimal High { get; init; }

	/// <summary>Low price.</summary>
	public decimal Low { get; init; }

	/// <summary>Close price.</summary>
	public decimal Close { get; init; }

	/// <summary>Volume traded during the candle period.</summary>
	public long Volume { get; init; }
}
