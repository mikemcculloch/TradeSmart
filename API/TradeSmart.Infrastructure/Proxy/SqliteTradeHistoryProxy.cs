using Microsoft.Extensions.Logging;
using TradeSmart.Domain;
using TradeSmart.Domain.Entities;
using TradeSmart.Domain.Interfaces.Proxies;

namespace TradeSmart.Infrastructure.Proxy;

/// <summary>Persists signal logs and trade history to SQLite.</summary>
public sealed class SqliteTradeHistoryProxy : ITradeHistoryProxy
{
	private readonly TradeDatabase _database;
	private readonly ILogger<SqliteTradeHistoryProxy> _logger;

	public SqliteTradeHistoryProxy(TradeDatabase database, ILogger<SqliteTradeHistoryProxy> logger)
	{
		_database = database;
		_logger = logger;
	}

	/// <inheritdoc />
	public async Task<ProxyResponse<bool>> LogSignalAsync(
		SignalLogEntry entry,
		CancellationToken cancellationToken = default)
	{
		try
		{
			await using var conn = await _database.CreateConnectionAsync(cancellationToken).ConfigureAwait(false);
			await using var cmd = conn.CreateCommand();

			cmd.CommandText = """
				INSERT INTO signal_log
					(id, received_at, type, symbol, exchange, direction, price, interval,
					 stop_loss, take_profit, decision, details, executed, trading_mode)
				VALUES
					($id, $received_at, $type, $symbol, $exchange, $direction, $price, $interval,
					 $stop_loss, $take_profit, $decision, $details, $executed, $trading_mode)
				""";

			cmd.Parameters.AddWithValue("$id", entry.Id);
			cmd.Parameters.AddWithValue("$received_at", entry.ReceivedAt.ToString("o"));
			cmd.Parameters.AddWithValue("$type", entry.Type);
			cmd.Parameters.AddWithValue("$symbol", entry.Symbol);
			cmd.Parameters.AddWithValue("$exchange", (object?)entry.Exchange ?? DBNull.Value);
			cmd.Parameters.AddWithValue("$direction", (object?)entry.Direction ?? DBNull.Value);
			cmd.Parameters.AddWithValue("$price", (double)entry.Price);
			cmd.Parameters.AddWithValue("$interval", (object?)entry.Interval ?? DBNull.Value);
			cmd.Parameters.AddWithValue("$stop_loss", entry.StopLoss.HasValue ? (double)entry.StopLoss.Value : DBNull.Value);
			cmd.Parameters.AddWithValue("$take_profit", entry.TakeProfit.HasValue ? (double)entry.TakeProfit.Value : DBNull.Value);
			cmd.Parameters.AddWithValue("$decision", entry.Decision);
			cmd.Parameters.AddWithValue("$details", (object?)entry.Details ?? DBNull.Value);
			cmd.Parameters.AddWithValue("$executed", entry.Executed ? 1 : 0);
			cmd.Parameters.AddWithValue("$trading_mode", entry.TradingMode);

			await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

			_logger.LogDebug("Signal logged: {Symbol} {Type} — {Decision}", entry.Symbol, entry.Type, entry.Decision);
			return ProxyResponse<bool>.Success(true);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to log signal for {Symbol}", entry.Symbol);
			return ProxyResponse<bool>.CreateError(
				Constants.ErrorCodes.PAPER_TRADING_STATE_ERROR,
				$"Failed to log signal: {ex.Message}");
		}
	}

	/// <inheritdoc />
	public async Task<ProxyResponse<IReadOnlyList<SignalLogEntry>>> GetRecentSignalsAsync(
		int count = 50,
		CancellationToken cancellationToken = default)
	{
		try
		{
			await using var conn = await _database.CreateConnectionAsync(cancellationToken).ConfigureAwait(false);
			await using var cmd = conn.CreateCommand();
			cmd.CommandText = "SELECT * FROM signal_log ORDER BY received_at DESC LIMIT $count";
			cmd.Parameters.AddWithValue("$count", count);

			return ProxyResponse<IReadOnlyList<SignalLogEntry>>.Success(
				await ReadSignalEntriesAsync(cmd, cancellationToken).ConfigureAwait(false));
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to get recent signals");
			return ProxyResponse<IReadOnlyList<SignalLogEntry>>.CreateError(
				Constants.ErrorCodes.PAPER_TRADING_STATE_ERROR, $"Failed to get signals: {ex.Message}");
		}
	}

	/// <inheritdoc />
	public async Task<ProxyResponse<IReadOnlyList<SignalLogEntry>>> GetSignalsBySymbolAsync(
		string symbol,
		int count = 50,
		CancellationToken cancellationToken = default)
	{
		try
		{
			await using var conn = await _database.CreateConnectionAsync(cancellationToken).ConfigureAwait(false);
			await using var cmd = conn.CreateCommand();
			cmd.CommandText = "SELECT * FROM signal_log WHERE symbol = $symbol ORDER BY received_at DESC LIMIT $count";
			cmd.Parameters.AddWithValue("$symbol", symbol);
			cmd.Parameters.AddWithValue("$count", count);

			return ProxyResponse<IReadOnlyList<SignalLogEntry>>.Success(
				await ReadSignalEntriesAsync(cmd, cancellationToken).ConfigureAwait(false));
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to get signals for {Symbol}", symbol);
			return ProxyResponse<IReadOnlyList<SignalLogEntry>>.CreateError(
				Constants.ErrorCodes.PAPER_TRADING_STATE_ERROR, $"Failed to get signals: {ex.Message}");
		}
	}

	private static async Task<List<SignalLogEntry>> ReadSignalEntriesAsync(
		Microsoft.Data.Sqlite.SqliteCommand cmd, CancellationToken cancellationToken)
	{
		var entries = new List<SignalLogEntry>();
		await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

		while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
		{
			entries.Add(new SignalLogEntry
			{
				Id = reader.GetString(reader.GetOrdinal("id")),
				ReceivedAt = DateTimeOffset.Parse(reader.GetString(reader.GetOrdinal("received_at"))),
				Type = reader.GetString(reader.GetOrdinal("type")),
				Symbol = reader.GetString(reader.GetOrdinal("symbol")),
				Exchange = reader.IsDBNull(reader.GetOrdinal("exchange")) ? "" : reader.GetString(reader.GetOrdinal("exchange")),
				Direction = reader.IsDBNull(reader.GetOrdinal("direction")) ? null : reader.GetString(reader.GetOrdinal("direction")),
				Price = (decimal)reader.GetDouble(reader.GetOrdinal("price")),
				Interval = reader.IsDBNull(reader.GetOrdinal("interval")) ? "" : reader.GetString(reader.GetOrdinal("interval")),
				StopLoss = reader.IsDBNull(reader.GetOrdinal("stop_loss")) ? null : (decimal)reader.GetDouble(reader.GetOrdinal("stop_loss")),
				TakeProfit = reader.IsDBNull(reader.GetOrdinal("take_profit")) ? null : (decimal)reader.GetDouble(reader.GetOrdinal("take_profit")),
				Decision = reader.GetString(reader.GetOrdinal("decision")),
				Details = reader.IsDBNull(reader.GetOrdinal("details")) ? null : reader.GetString(reader.GetOrdinal("details")),
				Executed = reader.GetInt32(reader.GetOrdinal("executed")) == 1,
				TradingMode = reader.GetString(reader.GetOrdinal("trading_mode"))
			});
		}

		return entries;
	}
}
