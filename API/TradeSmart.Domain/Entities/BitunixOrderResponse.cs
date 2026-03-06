namespace TradeSmart.Domain.Entities;

/// <summary>Response from Bitunix after placing an order.</summary>
public sealed record BitunixOrderResponse
{
	/// <summary>Exchange-assigned order ID.</summary>
	public string OrderId { get; init; } = string.Empty;

	/// <summary>Whether the order was accepted by the exchange.</summary>
	public bool Accepted { get; init; }

	/// <summary>Raw message from the exchange.</summary>
	public string Message { get; init; } = string.Empty;
}
