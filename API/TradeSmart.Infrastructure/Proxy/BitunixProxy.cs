using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TradeSmart.Domain;
using TradeSmart.Domain.Entities;
using TradeSmart.Domain.Interfaces.Proxies;

namespace TradeSmart.Infrastructure.Proxy;

/// <summary>
/// Proxy for the Bitunix futures exchange API.
/// Implements double SHA-256 request signing per Bitunix docs.
/// </summary>
public sealed class BitunixProxy : IBitunixProxy
{
	private readonly IConfiguration _configuration;
	private readonly IHttpClientFactory _httpClientFactory;
	private readonly ILogger<BitunixProxy> _logger;

	public BitunixProxy(
		IHttpClientFactory httpClientFactory,
		IConfiguration configuration,
		ILogger<BitunixProxy> logger)
	{
		_httpClientFactory = httpClientFactory;
		_configuration = configuration;
		_logger = logger;
	}

	/// <inheritdoc />
	public async Task<ProxyResponse<BitunixOrderResponse>> PlaceOrderAsync(
		BitunixOrderRequest request,
		CancellationToken cancellationToken = default)
	{
		var baseUrl = _configuration.GetBitunixBaseUrl();
		var url = $"{baseUrl}/api/v1/futures/trade/place_order";

		var body = new JObject
		{
			["symbol"] = request.Symbol,
			["qty"] = request.Qty.ToString("G"),
			["side"] = request.Side,
			["tradeSide"] = request.TradeSide,
			["orderType"] = request.OrderType
		};

		if (request.Price.HasValue && request.OrderType == "LIMIT")
			body["price"] = request.Price.Value.ToString("G");

		if (request.TpPrice.HasValue)
			body["tpPrice"] = request.TpPrice.Value.ToString("G");

		if (request.SlPrice.HasValue)
			body["slPrice"] = request.SlPrice.Value.ToString("G");

		try
		{
			var bodyJson = body.ToString(Formatting.None);
			var httpResponse = await SendSignedRequestAsync(HttpMethod.Post, url, queryParams: "", bodyJson, cancellationToken)
				.ConfigureAwait(false);

			var responseJson = await httpResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

			_logger.LogInformation(
				"Bitunix PlaceOrder response for {Symbol}: {StatusCode} — {Body}",
				request.Symbol,
				httpResponse.StatusCode,
				responseJson);

			if (!httpResponse.IsSuccessStatusCode)
			{
				return ProxyResponse<BitunixOrderResponse>.CreateError(
					Constants.ErrorCodes.BITUNIX_API_ERROR,
					$"Bitunix API returned {httpResponse.StatusCode}: {responseJson}");
			}

			var parsed = JObject.Parse(responseJson);
			var code = parsed["code"]?.Value<int>() ?? -1;
			var msg = parsed["msg"]?.ToString() ?? string.Empty;

			if (code != 0)
			{
				return ProxyResponse<BitunixOrderResponse>.CreateError(
					Constants.ErrorCodes.BITUNIX_ORDER_REJECTED,
					$"Bitunix order rejected (code {code}): {msg}");
			}

			var orderId = parsed["data"]?["orderId"]?.ToString() ?? string.Empty;

			return ProxyResponse<BitunixOrderResponse>.Success(new BitunixOrderResponse
			{
				OrderId = orderId,
				Accepted = true,
				Message = msg
			});
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Exception placing order on Bitunix for {Symbol}", request.Symbol);
			return ProxyResponse<BitunixOrderResponse>.CreateError(
				Constants.ErrorCodes.BITUNIX_API_ERROR,
				$"Failed to place order: {ex.Message}");
		}
	}

	/// <inheritdoc />
	public async Task<ProxyResponse<BitunixAccountInfo>> GetAccountAsync(
		CancellationToken cancellationToken = default)
	{
		var baseUrl = _configuration.GetBitunixBaseUrl();
		var marginCoin = _configuration.GetBitunixMarginCoin();
		var url = $"{baseUrl}/api/v1/futures/account";
		var queryParams = $"marginCoin={marginCoin}";

		try
		{
			var httpResponse = await SendSignedRequestAsync(HttpMethod.Get, $"{url}?{queryParams}", queryParams, body: "", cancellationToken)
				.ConfigureAwait(false);

			var responseJson = await httpResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

			if (!httpResponse.IsSuccessStatusCode)
			{
				return ProxyResponse<BitunixAccountInfo>.CreateError(
					Constants.ErrorCodes.BITUNIX_API_ERROR,
					$"Bitunix API returned {httpResponse.StatusCode}: {responseJson}");
			}

			var parsed = JObject.Parse(responseJson);
			var code = parsed["code"]?.Value<int>() ?? -1;

			if (code != 0)
			{
				return ProxyResponse<BitunixAccountInfo>.CreateError(
					Constants.ErrorCodes.BITUNIX_API_ERROR,
					$"Bitunix account query failed (code {code}): {parsed["msg"]}");
			}

			var data = parsed["data"];
			return ProxyResponse<BitunixAccountInfo>.Success(new BitunixAccountInfo
			{
				MarginCoin = marginCoin,
				Available = data?["available"]?.Value<decimal>() ?? 0m,
				Frozen = data?["frozen"]?.Value<decimal>() ?? 0m,
				Equity = data?["equity"]?.Value<decimal>() ?? 0m,
				UnrealizedPnl = data?["unrealizedPL"]?.Value<decimal>() ?? 0m
			});
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Exception getting Bitunix account info");
			return ProxyResponse<BitunixAccountInfo>.CreateError(
				Constants.ErrorCodes.BITUNIX_API_ERROR,
				$"Failed to get account info: {ex.Message}");
		}
	}

	/// <inheritdoc />
	public async Task<ProxyResponse<IReadOnlyList<BitunixPosition>>> GetPositionsAsync(
		CancellationToken cancellationToken = default)
	{
		var baseUrl = _configuration.GetBitunixBaseUrl();
		var url = $"{baseUrl}/api/v1/futures/position/get_pending_positions";

		try
		{
			var httpResponse = await SendSignedRequestAsync(HttpMethod.Get, url, queryParams: "", body: "", cancellationToken)
				.ConfigureAwait(false);

			var responseJson = await httpResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

			if (!httpResponse.IsSuccessStatusCode)
			{
				return ProxyResponse<IReadOnlyList<BitunixPosition>>.CreateError(
					Constants.ErrorCodes.BITUNIX_API_ERROR,
					$"Bitunix API returned {httpResponse.StatusCode}: {responseJson}");
			}

			var parsed = JObject.Parse(responseJson);
			var code = parsed["code"]?.Value<int>() ?? -1;

			if (code != 0)
			{
				return ProxyResponse<IReadOnlyList<BitunixPosition>>.CreateError(
					Constants.ErrorCodes.BITUNIX_API_ERROR,
					$"Bitunix positions query failed (code {code}): {parsed["msg"]}");
			}

			var positions = new List<BitunixPosition>();
			var dataArray = parsed["data"] as JArray ?? [];

			foreach (var item in dataArray)
			{
				positions.Add(new BitunixPosition
				{
					PositionId = item["positionId"]?.ToString() ?? string.Empty,
					Symbol = item["symbol"]?.ToString() ?? string.Empty,
					Side = item["side"]?.ToString() ?? string.Empty,
					Qty = item["qty"]?.Value<decimal>() ?? 0m,
					EntryPrice = item["entryPrice"]?.Value<decimal>() ?? 0m,
					MarkPrice = item["markPrice"]?.Value<decimal>() ?? 0m,
					UnrealizedPnl = item["unrealizedPL"]?.Value<decimal>() ?? 0m,
					Leverage = item["leverage"]?.Value<decimal>() ?? 1m
				});
			}

			return ProxyResponse<IReadOnlyList<BitunixPosition>>.Success(positions);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Exception getting Bitunix positions");
			return ProxyResponse<IReadOnlyList<BitunixPosition>>.CreateError(
				Constants.ErrorCodes.BITUNIX_API_ERROR,
				$"Failed to get positions: {ex.Message}");
		}
	}

	/// <inheritdoc />
	public async Task<ProxyResponse<bool>> CancelOrdersAsync(
		string symbol,
		IReadOnlyList<string> orderIds,
		CancellationToken cancellationToken = default)
	{
		var baseUrl = _configuration.GetBitunixBaseUrl();
		var url = $"{baseUrl}/api/v1/futures/trade/cancel_orders";

		var body = new JObject
		{
			["symbol"] = symbol,
			["orderIdList"] = new JArray(orderIds.Select(id => new JObject { ["orderId"] = id }))
		};

		try
		{
			var bodyJson = body.ToString(Formatting.None);
			var httpResponse = await SendSignedRequestAsync(HttpMethod.Post, url, queryParams: "", bodyJson, cancellationToken)
				.ConfigureAwait(false);

			var responseJson = await httpResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

			if (!httpResponse.IsSuccessStatusCode)
			{
				return ProxyResponse<bool>.CreateError(
					Constants.ErrorCodes.BITUNIX_API_ERROR,
					$"Bitunix API returned {httpResponse.StatusCode}: {responseJson}");
			}

			var parsed = JObject.Parse(responseJson);
			var code = parsed["code"]?.Value<int>() ?? -1;

			return code == 0
				? ProxyResponse<bool>.Success(true)
				: ProxyResponse<bool>.CreateError(
					Constants.ErrorCodes.BITUNIX_API_ERROR,
					$"Bitunix cancel failed (code {code}): {parsed["msg"]}");
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Exception cancelling orders on Bitunix for {Symbol}", symbol);
			return ProxyResponse<bool>.CreateError(
				Constants.ErrorCodes.BITUNIX_API_ERROR,
				$"Failed to cancel orders: {ex.Message}");
		}
	}

	// ── Signing ─────────────────────────────────────────────────────────

	/// <summary>
	/// Bitunix double SHA-256 signing:
	///   digest = SHA256(nonce + timestamp + apiKey + queryParams + body)
	///   sign   = SHA256(digest + secretKey)
	/// </summary>
	private async Task<HttpResponseMessage> SendSignedRequestAsync(
		HttpMethod method,
		string url,
		string queryParams,
		string body,
		CancellationToken cancellationToken)
	{
		var client = _httpClientFactory.CreateClient(Constants.BITUNIX_HTTP_CLIENT_NAME);
		var apiKey = _configuration.GetBitunixApiKey();
		var secret = _configuration.GetBitunixApiSecret();

		var nonce = GenerateNonce(32);
		var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();

		// Step 1: digest = SHA256(nonce + timestamp + apiKey + queryParams + body)
		var digestInput = $"{nonce}{timestamp}{apiKey}{queryParams}{body}";
		var digest = ComputeSha256Hex(digestInput);

		// Step 2: sign = SHA256(digest + secretKey)
		var signInput = $"{digest}{secret}";
		var sign = ComputeSha256Hex(signInput);

		var request = new HttpRequestMessage(method, url);
		request.Headers.Add("api-key", apiKey);
		request.Headers.Add("nonce", nonce);
		request.Headers.Add("timestamp", timestamp);
		request.Headers.Add("sign", sign);

		if (method == HttpMethod.Post && !string.IsNullOrEmpty(body))
		{
			request.Content = new StringContent(body, Encoding.UTF8, "application/json");
		}

		return await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
	}

	private static string GenerateNonce(int length)
	{
		const string chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
		var bytes = RandomNumberGenerator.GetBytes(length);
		var sb = new StringBuilder(length);

		for (var i = 0; i < length; i++)
		{
			sb.Append(chars[bytes[i] % chars.Length]);
		}

		return sb.ToString();
	}

	private static string ComputeSha256Hex(string input)
	{
		var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
		return Convert.ToHexStringLower(bytes);
	}
}
