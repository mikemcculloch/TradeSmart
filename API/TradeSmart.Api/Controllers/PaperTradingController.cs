using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using TradeSmart.Api.Dto;
using TradeSmart.Domain.Interfaces.Services;

namespace TradeSmart.Api.Controllers;

/// <summary>Endpoints for viewing paper trading wallet and positions.</summary>
[ApiController]
[Route("api/[controller]")]
public sealed class PaperTradingController : ControllerBase
{
	private readonly ILogger<PaperTradingController> _logger;
	private readonly IMapper _mapper;
	private readonly IPaperTradingService _paperTradingService;

	public PaperTradingController(
		IPaperTradingService paperTradingService,
		IMapper mapper,
		ILogger<PaperTradingController> logger)
	{
		_paperTradingService = paperTradingService;
		_mapper = mapper;
		_logger = logger;
	}

	/// <summary>Gets the current paper trading wallet state and open positions.</summary>
	[HttpGet("state")]
	[ProducesResponseType(typeof(PaperTradingStateDto), StatusCodes.Status200OK)]
	public ActionResult<PaperTradingStateDto> GetState()
	{
		var state = _paperTradingService.GetState();
		var dto = _mapper.Map<PaperTradingStateDto>(state);
		return Ok(dto);
	}

	/// <summary>Gets the closed position history.</summary>
	[HttpGet("history")]
	[ProducesResponseType(typeof(IReadOnlyList<PaperPositionDto>), StatusCodes.Status200OK)]
	public ActionResult<IReadOnlyList<PaperPositionDto>> GetHistory()
	{
		var closed = _paperTradingService.GetClosedPositions();
		var dtos = _mapper.Map<IReadOnlyList<PaperPositionDto>>(closed);
		return Ok(dtos);
	}
}
