using Microsoft.Extensions.Logging;
using PriceUpdates.API.Storage.Interface;

namespace PriceUpdates.API.Services
{
	public class PriceDistributorService : BackgroundService
	{
		private readonly ILogger<PriceDistributorService> _logger;
		private readonly IPriceStore _priceStore;
		private readonly PriceWebSocketManager _wsManager;

		public PriceDistributorService(
			ILogger<PriceDistributorService> logger,
			IPriceStore priceStore,
			PriceWebSocketManager wsManager)
		{
			_logger = logger;
			_priceStore = priceStore;
			_wsManager = wsManager;
		}

		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			_logger.LogInformation("PriceDistributorService started at {StartTime}.", DateTime.UtcNow);

			while (!stoppingToken.IsCancellationRequested)
			{
				try
				{
					var prices = _priceStore.GetAllPrices();
					if (prices.Count > 0)
					{
						var message = Newtonsoft.Json.JsonConvert.SerializeObject(prices);
						await _wsManager.BroadcastMessageAsync(message);
						_logger.LogInformation("Broadcasted {Count} prices at {TimeUtc} to WebSocket clients.", prices.Count, DateTime.UtcNow);
					}
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, "Error broadcasting price updates at {TimeUtc}.", DateTime.UtcNow);
				}

				await Task.Delay(1000, stoppingToken); // Broadcast every second
			}
		}
	}
}
