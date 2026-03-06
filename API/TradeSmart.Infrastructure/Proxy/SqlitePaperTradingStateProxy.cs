using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TradeSmart.Domain;
using TradeSmart.Domain.Entities;
using TradeSmart.Domain.Interfaces.Proxies;

namespace TradeSmart.Infrastructure.Proxy;

/// <summary>
/// SQLite-backed implementation of paper trading state persistence.
/// Replaces the fragile JSON file approach with proper database tables.
/// </summary>
public sealed class SqlitePaperTradingStateProxy : IPaperTradingStateProxy
{
	private readonly IConfiguration _configuration;
	private readonly TradeDatabase _database;
	private readonly ILogger<SqlitePaperTradingStateProxy> _logger;

	public SqlitePaperTradingStateProxy(
		TradeDatabase database,
		IConfiguration configuration,
		ILogger<SqlitePaperTradingStateProxy> logger)
	{
		_database = database;
		_configuration = configuration;
		_logger = logger;
	}

	/// <inheritdoc />
	public async Task<ProxyResponse<PaperTradingState>> LoadStateAsync(
		CancellationToken cancellationToken = default)
	{
		try
		{
			await using var conn = await _database.CreateConnectionAsync(cancellationToken).ConfigureAwait(false);

			// Load wallet
			var wallet = await LoadWalletAsync(conn, cancellationToken).ConfigureAwait(false);

			// Load open positions
			var openPositions = await LoadPositionsAsync(conn, isOpen: true, cancellationToken).ConfigureAwait(false);

			// Load closed positions (last 100 for history)
			var closedPositions = await LoadPositionsAsync(conn, isOpen: false, cancellationToken).ConfigureAwait(false);

			_logger.LogInformation(
				"Loaded state from SQLite: Balance={Balance:C}, Open={Open}, Closed={Closed}",
				wallet.AvailableBalance, openPositions.Count, closedPositions.Count);

			return ProxyResponse<PaperTradingState>.Success(new PaperTradingState
			{
				Wallet = wallet,
				OpenPositions = openPositions,
				ClosedPositions = closedPositions,
				LastUpdatedAt = DateTimeOffset.UtcNow
			});
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to load paper trading state from SQLite");
			return ProxyResponse<PaperTradingState>.CreateError(
				Constants.ErrorCodes.PAPER_TRADING_STATE_ERROR,
				$"Failed to load state: {ex.Message}");
		}
	}

	/// <inheritdoc />
	public async Task<ProxyResponse<bool>> SaveStateAsync(
		PaperTradingState state,
		CancellationToken cancellationToken = default)
	{
		try
		{
			await using var conn = await _database.CreateConnectionAsync(cancellationToken).ConfigureAwait(false);
			await using var transaction = await conn.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

			// Upsert wallet
			await SaveWalletAsync(conn, state.Wallet, cancellationToken).ConfigureAwait(false);

			// Upsert all positions (open + closed)
			foreach (var pos in state.OpenPositions)
			{
				await UpsertPositionAsync(conn, pos, isOpen: true, cancellationToken).ConfigureAwait(false);
			}

			foreach (var pos in state.ClosedPositions)
			{
				await UpsertPositionAsync(conn, pos, isOpen: false, cancellationToken).ConfigureAwait(false);
			}

			await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

			_logger.LogDebug("Saved state to SQLite: Balance={Balance:C}", state.Wallet.AvailableBalance);
			return ProxyResponse<bool>.Success(true);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to save paper trading state to SQLite");
			return ProxyResponse<bool>.CreateError(
				Constants.ErrorCodes.PAPER_TRADING_STATE_ERROR,
				$"Failed to save state: {ex.Message}");
		}
	}

	// ── Wallet ──────────────────────────────────────────────────────────

	private async Task<PaperWallet> LoadWalletAsync(
		Microsoft.Data.Sqlite.SqliteConnection conn, CancellationToken cancellationToken)
	{
		await using var cmd = conn.CreateCommand();
		cmd.CommandText = "SELECT * FROM wallet_state WHERE id = 1";

		await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

		if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
		{
			return new PaperWallet
			{
				InitialBalance = (decimal)reader.GetDouble(reader.GetOrdinal("initial_balance")),
				AvailableBalance = (decimal)reader.GetDouble(reader.GetOrdinal("available_balance")),
				TotalRealizedPnl = (decimal)reader.GetDouble(reader.GetOrdinal("total_realized_pnl")),
				TotalTrades = reader.GetInt32(reader.GetOrdinal("total_trades")),
				WinningTrades = reader.GetInt32(reader.GetOrdinal("winning_trades")),
				LosingTrades = reader.GetInt32(reader.GetOrdinal("losing_trades"))
			};
		}

		// No wallet yet — return default
		var initialBalance = _configuration.GetPaperTradingInitialBalance();
		return new PaperWallet
		{
			InitialBalance = initialBalance,
			AvailableBalance = initialBalance
		};
	}

	private static async Task SaveWalletAsync(
		Microsoft.Data.Sqlite.SqliteConnection conn, PaperWallet wallet, CancellationToken cancellationToken)
	{
		await using var cmd = conn.CreateCommand();
		cmd.CommandText = """
			INSERT INTO wallet_state (id, initial_balance, available_balance, total_realized_pnl,
				total_trades, winning_trades, losing_trades, updated_at)
			VALUES (1, $initial, $available, $pnl, $total, $wins, $losses, $updated)
			ON CONFLICT(id) DO UPDATE SET
				initial_balance = $initial,
				available_balance = $available,
				total_realized_pnl = $pnl,
				total_trades = $total,
				winning_trades = $wins,
				losing_trades = $losses,
				updated_at = $updated
			""";

		cmd.Parameters.AddWithValue("$initial", (double)wallet.InitialBalance);
		cmd.Parameters.AddWithValue("$available", (double)wallet.AvailableBalance);
		cmd.Parameters.AddWithValue("$pnl", (double)wallet.TotalRealizedPnl);
		cmd.Parameters.AddWithValue("$total", wallet.TotalTrades);
		cmd.Parameters.AddWithValue("$wins", wallet.WinningTrades);
		cmd.Parameters.AddWithValue("$losses", wallet.LosingTrades);
		cmd.Parameters.AddWithValue("$updated", DateTimeOffset.UtcNow.ToString("o"));

		await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
	}

	// ── Positions ───────────────────────────────────────────────────────

	private static async Task<List<PaperPosition>> LoadPositionsAsync(
		Microsoft.Data.Sqlite.SqliteConnection conn, bool isOpen, CancellationToken cancellationToken)
	{
		await using var cmd = conn.CreateCommand();
		cmd.CommandText = isOpen
			? "SELECT * FROM positions WHERE is_open = 1 ORDER BY opened_at DESC"
			: "SELECT * FROM positions WHERE is_open = 0 ORDER BY closed_at DESC LIMIT 200";

		var positions = new List<PaperPosition>();
		await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

		while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
		{
			positions.Add(new PaperPosition
			{
				PositionId = reader.GetString(reader.GetOrdinal("position_id")),
				Symbol = reader.GetString(reader.GetOrdinal("symbol")),
				Direction = Enum.Parse<TradeDirection>(reader.GetString(reader.GetOrdinal("direction"))),
				EntryPrice = (decimal)reader.GetDouble(reader.GetOrdinal("entry_price")),
				PositionSizeUsd = (decimal)reader.GetDouble(reader.GetOrdinal("position_size_usd")),
				Quantity = (decimal)reader.GetDouble(reader.GetOrdinal("quantity")),
				Leverage = (decimal)reader.GetDouble(reader.GetOrdinal("leverage")),
				StopLoss = (decimal)reader.GetDouble(reader.GetOrdinal("stop_loss")),
				TakeProfit = (decimal)reader.GetDouble(reader.GetOrdinal("take_profit")),
				Confidence = reader.GetInt32(reader.GetOrdinal("confidence")),
				Reasoning = reader.IsDBNull(reader.GetOrdinal("reasoning")) ? "" : reader.GetString(reader.GetOrdinal("reasoning")),
				OpenedAt = DateTimeOffset.Parse(reader.GetString(reader.GetOrdinal("opened_at"))),
				ClosedAt = reader.IsDBNull(reader.GetOrdinal("closed_at")) ? null : DateTimeOffset.Parse(reader.GetString(reader.GetOrdinal("closed_at"))),
				ExitPrice = reader.IsDBNull(reader.GetOrdinal("exit_price")) ? null : (decimal)reader.GetDouble(reader.GetOrdinal("exit_price")),
				RealizedPnl = reader.IsDBNull(reader.GetOrdinal("realized_pnl")) ? null : (decimal)reader.GetDouble(reader.GetOrdinal("realized_pnl")),
				CloseReason = reader.IsDBNull(reader.GetOrdinal("close_reason")) ? null : reader.GetString(reader.GetOrdinal("close_reason"))
			});
		}

		return positions;
	}

	private static async Task UpsertPositionAsync(
		Microsoft.Data.Sqlite.SqliteConnection conn, PaperPosition pos, bool isOpen, CancellationToken cancellationToken)
	{
		await using var cmd = conn.CreateCommand();
		cmd.CommandText = """
			INSERT INTO positions
				(position_id, symbol, direction, entry_price, position_size_usd, quantity,
				 leverage, stop_loss, take_profit, confidence, reasoning, opened_at,
				 closed_at, exit_price, realized_pnl, close_reason, is_open)
			VALUES
				($id, $symbol, $direction, $entry, $size, $qty,
				 $leverage, $sl, $tp, $confidence, $reasoning, $opened,
				 $closed, $exit, $pnl, $reason, $is_open)
			ON CONFLICT(position_id) DO UPDATE SET
				closed_at = $closed,
				exit_price = $exit,
				realized_pnl = $pnl,
				close_reason = $reason,
				is_open = $is_open
			""";

		cmd.Parameters.AddWithValue("$id", pos.PositionId);
		cmd.Parameters.AddWithValue("$symbol", pos.Symbol);
		cmd.Parameters.AddWithValue("$direction", pos.Direction.ToString());
		cmd.Parameters.AddWithValue("$entry", (double)pos.EntryPrice);
		cmd.Parameters.AddWithValue("$size", (double)pos.PositionSizeUsd);
		cmd.Parameters.AddWithValue("$qty", (double)pos.Quantity);
		cmd.Parameters.AddWithValue("$leverage", (double)pos.Leverage);
		cmd.Parameters.AddWithValue("$sl", (double)pos.StopLoss);
		cmd.Parameters.AddWithValue("$tp", (double)pos.TakeProfit);
		cmd.Parameters.AddWithValue("$confidence", pos.Confidence);
		cmd.Parameters.AddWithValue("$reasoning", (object?)pos.Reasoning ?? DBNull.Value);
		cmd.Parameters.AddWithValue("$opened", pos.OpenedAt.ToString("o"));
		cmd.Parameters.AddWithValue("$closed", pos.ClosedAt.HasValue ? pos.ClosedAt.Value.ToString("o") : DBNull.Value);
		cmd.Parameters.AddWithValue("$exit", pos.ExitPrice.HasValue ? (double)pos.ExitPrice.Value : DBNull.Value);
		cmd.Parameters.AddWithValue("$pnl", pos.RealizedPnl.HasValue ? (double)pos.RealizedPnl.Value : DBNull.Value);
		cmd.Parameters.AddWithValue("$reason", (object?)pos.CloseReason ?? DBNull.Value);
		cmd.Parameters.AddWithValue("$is_open", isOpen ? 1 : 0);

		await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
	}
}
