using System.ComponentModel.DataAnnotations;

namespace TradeSmart.Api.Dto;

/// <summary>Incoming webhook payload from TradingView.</summary>
public sealed record TradingViewAlertRequestDto
{
	/// <summary>The ticker symbol (e.g., "AAPL", "BTCUSD").</summary>
	[Required(ErrorMessage = "Symbol is required.")]
	public string Symbol { get; init; } = string.Empty;

	/// <summary>The exchange (e.g., "NASDAQ", "BINANCE").</summary>
	public string Exchange { get; init; } = string.Empty;

	/// <summary>The alert action from TradingView (e.g., "buy", "sell").</summary>
	public string Action { get; init; } = string.Empty;

	/// <summary>The price at the time the alert fired.</summary>
	public decimal Price { get; init; }

	/// <summary>The timeframe/interval that triggered the alert (e.g., "15", "60", "D").</summary>
	public string Interval { get; init; } = string.Empty;

	/// <summary>The alert message/description from TradingView.</summary>
	public string Message { get; init; } = string.Empty;

	/// <summary>Signal type: "entry" for new positions, "close" for exits.</summary>
	public string Type { get; init; } = "entry";

	/// <summary>Trade direction: "long" or "short" (entry signals only).</summary>
	public string? Direction { get; init; }

	/// <summary>Stop-loss price from the strategy (entry signals only).</summary>
	public decimal? StopLoss { get; init; }

	/// <summary>Take-profit price from the strategy (entry signals only).</summary>
	public decimal? TakeProfit { get; init; }

	/// <summary>Shared secret for webhook authentication.</summary>
	public string? Secret { get; init; }
}
