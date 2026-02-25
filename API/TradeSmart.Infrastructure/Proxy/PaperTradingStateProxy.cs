using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using TradeSmart.Domain;
using TradeSmart.Domain.Entities;
using TradeSmart.Domain.Interfaces.Proxies;

namespace TradeSmart.Infrastructure.Proxy;

/// <summary>Persists paper trading state to a JSON file on disk.</summary>
public sealed class PaperTradingStateProxy : IPaperTradingStateProxy
{
	private static readonly JsonSerializerSettings JsonSettings = new()
	{
		Formatting = Formatting.Indented,
		Converters = { new StringEnumConverter() },
		NullValueHandling = NullValueHandling.Ignore
	};

	private readonly IConfiguration _configuration;
	private readonly ILogger<PaperTradingStateProxy> _logger;

	public PaperTradingStateProxy(
		IConfiguration configuration,
		ILogger<PaperTradingStateProxy> logger)
	{
		_configuration = configuration;
		_logger = logger;
	}

	/// <inheritdoc />
	public async Task<ProxyResponse<PaperTradingState>> LoadStateAsync(
		CancellationToken cancellationToken = default)
	{
		var filePath = _configuration.GetPaperTradingStateFilePath();

		try
		{
			if (!File.Exists(filePath))
			{
				_logger.LogInformation(
					"No paper trading state file found at {FilePath} — starting fresh",
					filePath);

				return ProxyResponse<PaperTradingState>.Success(CreateDefaultState());
			}

			var json = await File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false);
			var state = JsonConvert.DeserializeObject<PaperTradingState>(json, JsonSettings);

			if (state is null)
			{
				_logger.LogWarning("Paper trading state file was null/empty — starting fresh");
				return ProxyResponse<PaperTradingState>.Success(CreateDefaultState());
			}

			_logger.LogInformation(
				"Loaded paper trading state: Balance={Balance:C}, OpenPositions={OpenCount}, ClosedPositions={ClosedCount}",
				state.Wallet.AvailableBalance,
				state.OpenPositions.Count,
				state.ClosedPositions.Count);

			return ProxyResponse<PaperTradingState>.Success(state);
		}
		catch (JsonException ex)
		{
			_logger.LogError(ex, "Paper trading state file is corrupted at {FilePath} — backing up and starting fresh", filePath);

			// Backup the corrupted file
			var backupPath = filePath + $".corrupted.{DateTimeOffset.UtcNow:yyyyMMddHHmmss}";
			try
			{
				File.Move(filePath, backupPath, overwrite: true);
				_logger.LogInformation("Corrupted state file backed up to {BackupPath}", backupPath);
			}
			catch (Exception moveEx)
			{
				_logger.LogWarning(moveEx, "Failed to backup corrupted state file");
			}

			return ProxyResponse<PaperTradingState>.Success(CreateDefaultState());
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to load paper trading state from {FilePath}", filePath);
			return ProxyResponse<PaperTradingState>.CreateError(
				Constants.ErrorCodes.PAPER_TRADING_STATE_ERROR,
				$"Failed to load paper trading state: {ex.Message}");
		}
	}

	/// <inheritdoc />
	public async Task<ProxyResponse<bool>> SaveStateAsync(
		PaperTradingState state,
		CancellationToken cancellationToken = default)
	{
		var filePath = _configuration.GetPaperTradingStateFilePath();

		try
		{
			var json = JsonConvert.SerializeObject(state, JsonSettings);

			// Atomic write: write to temp file, then move with overwrite
			var tempPath = filePath + ".tmp";
			await File.WriteAllTextAsync(tempPath, json, cancellationToken).ConfigureAwait(false);
			File.Move(tempPath, filePath, overwrite: true);

			return ProxyResponse<bool>.Success(true);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to save paper trading state to {FilePath}", filePath);
			return ProxyResponse<bool>.CreateError(
				Constants.ErrorCodes.PAPER_TRADING_STATE_ERROR,
				$"Failed to save paper trading state: {ex.Message}");
		}
	}

	private PaperTradingState CreateDefaultState()
	{
		var initialBalance = _configuration.GetPaperTradingInitialBalance();

		return new PaperTradingState
		{
			Wallet = new PaperWallet
			{
				InitialBalance = initialBalance,
				AvailableBalance = initialBalance
			}
		};
	}
}
