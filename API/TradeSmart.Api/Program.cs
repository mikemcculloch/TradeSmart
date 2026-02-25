using TradeSmart.Api;
using TradeSmart.Domain;
using TradeSmart.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// 1. Configuration
builder.Services.AddControllers()
	.AddNewtonsoftJson();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
	options.SwaggerDoc("v1", new()
	{
		Title = "TradeSmart API",
		Version = "v1",
		Description = "AI-powered trade analysis API. Receives TradingView webhook alerts, " +
					  "fetches multi-timeframe market data from Twelve Data, and uses Claude " +
					  "to evaluate trade opportunities."
	});
});

// 2. AutoMapper
builder.Services.AddAutoMapper(cfg =>
{
	cfg.AddProfile<TradeSmart.Api.Mappers.MappingProfile>();
});

// 3. Register services — Api → Domain → Infrastructure
builder.Services.RegisterApiServices(builder.Configuration);
builder.Services.RegisterDomainServices(builder.Configuration);
builder.Services.RegisterInfrastructureServices(builder.Configuration);

var app = builder.Build();

// 4. Middleware
if (app.Environment.IsDevelopment())
{
	app.UseSwagger();
	app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "TradeSmart API v1"));
}

app.MapControllers();

// 5. Health check endpoint
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTimeOffset.UtcNow }));

await app.RunAsync();
