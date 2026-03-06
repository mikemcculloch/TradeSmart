namespace TradeSmart.Domain.Entities;

/// <summary>Request payload for placing an order on Bitunix futures.</summary>
public sealed record BitunixOrderRequest
{
	/// <summary>Trading pair symbol (e.g., "BTCUSDT").</summary>
	public string Symbol { get; init; } = string.Empty;

	/// <summary>Quantity to trade.</summary>
	public decimal Qty { get; init; }

	/// <summary>Order side: "BUY" or "SELL".</summary>
	public string Side { get; init; } = string.Empty;

	/// <summary>Trade side: "OPEN" or "CLOSE".</summary>
	public string TradeSide { get; init; } = string.Empty;

	/// <summary>Order type: "MARKET" or "LIMIT".</summary>
	public string OrderType { get; init; } = "MARKET";

	/// <summary>Limit price (required for LIMIT orders).</summary>
	public decimal? Price { get; init; }

	/// <summary>Take-profit price (optional).</summary>
	public decimal? TpPrice { get; init; }

	/// <summary>Stop-loss price (optional).</summary>
	public decimal? SlPrice { get; init; }
}
