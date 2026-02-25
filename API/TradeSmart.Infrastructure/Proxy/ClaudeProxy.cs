using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TradeSmart.Domain;
using TradeSmart.Domain.Entities;
using TradeSmart.Domain.Interfaces.Proxies;

namespace TradeSmart.Infrastructure.Proxy;

/// <summary>Proxy for sending trade analysis requests to the Claude (Anthropic) API.</summary>
public sealed class ClaudeProxy : IClaudeProxy
{
	private readonly IConfiguration _configuration;
	private readonly IHttpClientFactory _httpClientFactory;
	private readonly ILogger<ClaudeProxy> _logger;

	public ClaudeProxy(
		IHttpClientFactory httpClientFactory,
		IConfiguration configuration,
		ILogger<ClaudeProxy> logger)
	{
		_httpClientFactory = httpClientFactory;
		_configuration = configuration;
		_logger = logger;
	}

	/// <inheritdoc />
	public async Task<ProxyResponse<TradeAnalysis>> AnalyzeTradeAsync(
		TradingViewAlert alert,
		IReadOnlyList<TimeframeData> marketData,
		CancellationToken cancellationToken = default)
	{
		var client = _httpClientFactory.CreateClient(Constants.CLAUDE_HTTP_CLIENT_NAME);
		var baseUrl = _configuration.GetClaudeBaseUrl();
		var model = _configuration.GetClaudeModel();
		var maxTokens = _configuration.GetClaudeMaxTokens();
		var url = $"{baseUrl}/v1/messages";

		try
		{
			var systemPrompt = BuildSystemPrompt();
			var userMessage = BuildUserMessage(alert, marketData);

			var requestBody = new
			{
				model,
				max_tokens = maxTokens,
				system = systemPrompt,
				messages = new[]
				{
					new { role = "user", content = userMessage }
				}
			};

			var jsonContent = JsonConvert.SerializeObject(requestBody);
			var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

			var httpResponse = await client.SendAsync(
				new HttpRequestMessage(HttpMethod.Post, url)
				{
					Content = httpContent
				},
				cancellationToken).ConfigureAwait(false);

			if (!httpResponse.IsSuccessStatusCode)
			{
				var errorBody = await httpResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
				_logger.LogError(
					"Claude API returned {StatusCode} for {Symbol}: {ErrorBody}",
					httpResponse.StatusCode,
					alert.Symbol,
					errorBody);

				return ProxyResponse<TradeAnalysis>.CreateError(
					Constants.ErrorCodes.CLAUDE_API_ERROR,
					$"Claude API returned {httpResponse.StatusCode}.");
			}

			var responseJson = await httpResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
			var responseObj = JObject.Parse(responseJson);

			// Extract the text content from Claude's response
			var textContent = responseObj["content"]?[0]?["text"]?.ToString();
			if (string.IsNullOrWhiteSpace(textContent))
			{
				_logger.LogError("Claude API returned empty content for {Symbol}", alert.Symbol);
				return ProxyResponse<TradeAnalysis>.CreateError(
					Constants.ErrorCodes.CLAUDE_API_ERROR,
					"Claude returned empty analysis content.");
			}

			// Parse the structured JSON from Claude's response
			var analysis = ParseClaudeResponse(textContent, alert.Symbol);
			return ProxyResponse<TradeAnalysis>.Success(analysis);
		}
		catch (JsonException ex)
		{
			_logger.LogError(ex, "Failed to parse Claude response for {Symbol}", alert.Symbol);
			return ProxyResponse<TradeAnalysis>.CreateError(
				Constants.ErrorCodes.CLAUDE_API_ERROR,
				$"Failed to parse Claude response: {ex.Message}");
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Exception calling Claude API for {Symbol}", alert.Symbol);
			return ProxyResponse<TradeAnalysis>.CreateError(
				Constants.ErrorCodes.CLAUDE_API_ERROR,
				$"Failed to call Claude API: {ex.Message}");
		}
	}

	private static string BuildSystemPrompt()
	{
		return """
			You are an expert quantitative trading analyst. You analyze market data across multiple timeframes 
			to determine whether a trading alert represents a high-probability trade opportunity.

			You MUST respond with ONLY a JSON object (no markdown, no explanation outside JSON) with these exact fields:
			{
				"direction": "Long" | "Short" | "NoTrade",
				"confidence": <integer 0-100>,
				"entryPrice": <decimal or null>,
				"stopLoss": <decimal or null>,
				"takeProfit": <decimal or null>,
				"riskRewardRatio": "<string like '1:2.5' or null>",
				"reasoning": "<detailed multi-sentence analysis>"
			}

			Analysis guidelines:
			- Analyze trend alignment across timeframes (higher timeframe trend should confirm lower timeframe entry)
			- Look for key support/resistance levels based on recent price action
			- Evaluate volume patterns for confirmation
			- Consider the current price relative to recent highs/lows
			- Set stop loss at a logical technical level (recent swing high/low, key support/resistance)
			- Set take profit at a realistic target based on recent price structure
			- If the setup is unclear or conflicting signals exist, recommend "NoTrade" with reasoning
			- Confidence should reflect the strength of trend alignment, volume confirmation, and clean technical setup
			""";
	}

	private static string BuildUserMessage(TradingViewAlert alert, IReadOnlyList<TimeframeData> marketData)
	{
		var sb = new StringBuilder();
		sb.AppendLine($"## TradingView Alert");
		sb.AppendLine($"- **Symbol**: {alert.Symbol}");
		sb.AppendLine($"- **Exchange**: {alert.Exchange}");
		sb.AppendLine($"- **Action**: {alert.Action}");
		sb.AppendLine($"- **Current Price**: {alert.Price}");
		sb.AppendLine($"- **Alert Interval**: {alert.Interval}");
		sb.AppendLine($"- **Message**: {alert.Message}");
		sb.AppendLine();
		sb.AppendLine("## Market Data (OHLCV Candles by Timeframe)");

		foreach (var tf in marketData)
		{
			sb.AppendLine();
			sb.AppendLine($"### {tf.Timeframe} ({tf.Candles.Count} candles)");
			sb.AppendLine("| Datetime | Open | High | Low | Close | Volume |");
			sb.AppendLine("|----------|------|------|-----|-------|--------|");

			foreach (var candle in tf.Candles.Take(20)) // limit for token efficiency
			{
				sb.AppendLine(
					$"| {candle.Datetime:yyyy-MM-dd HH:mm} | {candle.Open} | {candle.High} | {candle.Low} | {candle.Close} | {candle.Volume} |");
			}
		}

		sb.AppendLine();
		sb.AppendLine("Based on this data, analyze whether this is a good trade opportunity. Respond with ONLY a JSON object.");

		return sb.ToString();
	}

	private TradeAnalysis ParseClaudeResponse(string claudeText, string symbol)
	{
		// Claude may wrap response in markdown code block — strip it
		var jsonText = claudeText.Trim();
		if (jsonText.StartsWith("```"))
		{
			var firstNewline = jsonText.IndexOf('\n');
			if (firstNewline >= 0)
			{
				jsonText = jsonText[(firstNewline + 1)..];
			}

			if (jsonText.EndsWith("```"))
			{
				jsonText = jsonText[..^3].Trim();
			}
		}

		var parsed = JObject.Parse(jsonText);

		var directionStr = parsed["direction"]?.ToString() ?? "NoTrade";
		var direction = directionStr switch
		{
			"Long" => TradeDirection.Long,
			"Short" => TradeDirection.Short,
			_ => TradeDirection.NoTrade
		};

		return new TradeAnalysis
		{
			Symbol = symbol,
			Direction = direction,
			Confidence = parsed["confidence"]?.Value<int>() ?? 0,
			EntryPrice = parsed["entryPrice"]?.Value<decimal?>(),
			StopLoss = parsed["stopLoss"]?.Value<decimal?>(),
			TakeProfit = parsed["takeProfit"]?.Value<decimal?>(),
			RiskRewardRatio = parsed["riskRewardRatio"]?.ToString(),
			Reasoning = parsed["reasoning"]?.ToString() ?? "No reasoning provided.",
			AnalyzedAt = DateTimeOffset.UtcNow
		};
	}
}
