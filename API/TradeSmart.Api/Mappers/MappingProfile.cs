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

		CreateMap<TradeAnalysis, TradeAnalysisResponseDto>()
			.ForMember(dest => dest.Direction, opt => opt.MapFrom(src => src.Direction.ToString()));

		// Paper trading mappings
		CreateMap<PaperTradingState, PaperTradingStateDto>();
		CreateMap<PaperWallet, PaperWalletDto>();
		CreateMap<PaperPosition, PaperPositionDto>()
			.ForMember(dest => dest.Direction, opt => opt.MapFrom(src => src.Direction.ToString()));
	}
}
