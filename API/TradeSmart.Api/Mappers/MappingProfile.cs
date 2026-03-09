using AutoMapper;
using TradeSmart.Api.Dto;
using TradeSmart.Domain.Entities;

namespace TradeSmart.Api.Mappers;

/// <summary>AutoMapper profile for TradeSmart API DTOs.</summary>
public sealed class MappingProfile : Profile
{
	public MappingProfile()
	{
		CreateMap<TradingViewAlertRequestDto, TradingViewAlert>();

		CreateMap<StrategySignalRequestDto, TradingViewAlert>()
			.ForMember(dest => dest.StopLoss, opt => opt.Ignore())
			.ForMember(dest => dest.TakeProfit, opt => opt.Ignore())
			.ForMember(dest => dest.Exchange, opt => opt.Ignore())
			.ForMember(dest => dest.Action, opt => opt.Ignore());

		CreateMap<TradeAnalysis, TradeAnalysisResponseDto>()
			.ForMember(dest => dest.Direction, opt => opt.MapFrom(src => src.Direction.ToString()));

		// Paper trading mappings
		CreateMap<PaperTradingState, PaperTradingStateDto>();
		CreateMap<PaperWallet, PaperWalletDto>();
		CreateMap<PaperPosition, PaperPositionDto>()
			.ForMember(dest => dest.Direction, opt => opt.MapFrom(src => src.Direction.ToString()));
	}
}
