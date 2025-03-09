using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using PriceUpdates.API.Services;
using PriceUpdates.API.Storage.Implementation;
using PriceUpdates.API.Storage.Interface;
using Microsoft.OpenApi.Models;
using System.Net.WebSockets;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Configure logging (Serilog logs to file)
var logFilePath = "Logs/log-.txt";
Log.Logger = new LoggerConfiguration()
	.WriteTo.File(logFilePath, rollingInterval: RollingInterval.Day)
	.CreateLogger();

// Use Serilog
builder.Logging.ClearProviders();
builder.Logging.AddSerilog();

// Register services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
	c.SwaggerDoc("v1", new OpenApiInfo
	{
		Title = "Live Price Service API",
		Version = "v1",
		Description = "API for retrieving financial instrument prices."
	});
});
builder.Services.AddHttpClient();

// Register dependencies
builder.Services.AddSingleton<InstrumentService>();
builder.Services.AddSingleton<IPriceStore, PriceStore>();
builder.Services.AddSingleton<PriceWebSocketManager>();

//Tiingo Service
builder.Services.AddSingleton<TiingoPriceService>();
builder.Services.AddHostedService<TiingoPriceService>(); // Ensures background execution

//Price Distributor Service
builder.Services.AddHostedService<PriceDistributorService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
	app.UseSwagger();
	app.UseSwaggerUI();
}

app.UseWebSockets();
app.UseRouting();
app.UseAuthorization();

// Log middleware pipeline events if needed
// e.g., app.UseSerilogRequestLogging(); // optional

app.MapControllers();

// Updated WebSocket route handling with structured logs
app.Use(async (context, next) =>
{
	if (context.Request.Path == "/ws/prices")
	{
		var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
		var webSocketManager = context.RequestServices.GetRequiredService<PriceWebSocketManager>();

		if (context.WebSockets.IsWebSocketRequest)
		{
			using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
			var connectionId = webSocketManager.AddSocket(webSocket);

			logger.LogInformation("Accepted new WebSocket connection. ConnectionId={ConnectionId}", connectionId);

			await HandleWebSocketConnection(context, webSocketManager, connectionId, webSocket, logger);
		}
		else
		{
			logger.LogWarning("Bad Request: Non-WebSocket request made to /ws/prices");
			context.Response.StatusCode = 400; // Bad Request
		}
	}
	else
	{
		await next();
	}
});

app.Run();

// Updated WebSocket handler with structured logging
static async Task HandleWebSocketConnection(
	HttpContext context,
	PriceWebSocketManager wsManager,
	string connectionId,
	WebSocket webSocket,
	Microsoft.Extensions.Logging.ILogger logger)
{
	var buffer = new byte[4 * 1024];

	try
	{
		while (webSocket.State == WebSocketState.Open)
		{
			var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), context.RequestAborted);

			if (result.MessageType == WebSocketMessageType.Close)
			{
				logger.LogInformation("WebSocket closing for ConnectionId={ConnectionId}", connectionId);
				break;
			}

			var receivedMessage = Encoding.UTF8.GetString(buffer, 0, result.Count);

			// Broadcast received message to all clients
			logger.LogDebug("Received message from ConnectionId={ConnectionId}: {Message}", connectionId, receivedMessage);
			await wsManager.BroadcastMessageAsync($"Client {connectionId} sent: {receivedMessage}");
		}
	}
	catch (Exception ex)
	{
		logger.LogError(ex, "Error in WebSocket communication for ConnectionId={ConnectionId}", connectionId);
	}
	finally
	{
		wsManager.RemoveSocket(connectionId);

		if (webSocket.State == WebSocketState.Open)
		{
			logger.LogInformation("Closing WebSocket for ConnectionId={ConnectionId}", connectionId);
			await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", context.RequestAborted);
		}
	}
}
