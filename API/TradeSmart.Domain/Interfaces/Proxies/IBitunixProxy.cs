using TradeSmart.Domain.Entities;

namespace TradeSmart.Domain.Interfaces.Proxies;

/// <summary>Proxy for interacting with the Bitunix futures exchange API.</summary>
public interface IBitunixProxy
{
	/// <summary>Places an order on Bitunix futures.</summary>
	/// <param name="request">The order parameters.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The order result from the exchange.</returns>
	Task<ProxyResponse<BitunixOrderResponse>> PlaceOrderAsync(
		BitunixOrderRequest request,
		CancellationToken cancellationToken = default);

	/// <summary>Gets the futures account information.</summary>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>Account summary including balances.</returns>
	Task<ProxyResponse<BitunixAccountInfo>> GetAccountAsync(
		CancellationToken cancellationToken = default);

	/// <summary>Gets all pending (open) positions.</summary>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>List of open positions.</returns>
	Task<ProxyResponse<IReadOnlyList<BitunixPosition>>> GetPositionsAsync(
		CancellationToken cancellationToken = default);

	/// <summary>Cancels one or more orders by ID.</summary>
	/// <param name="symbol">The trading pair symbol.</param>
	/// <param name="orderIds">The order IDs to cancel.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>Success or failure.</returns>
	Task<ProxyResponse<bool>> CancelOrdersAsync(
		string symbol,
		IReadOnlyList<string> orderIds,
		CancellationToken cancellationToken = default);
}
