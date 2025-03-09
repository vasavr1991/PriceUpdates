using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PriceUpdates.API.Storage.Interface;

namespace PriceUpdates.API.Services
{
	public class TiingoPriceService : BackgroundService
	{
		private readonly ILogger<TiingoPriceService> _logger;
		private readonly IPriceStore _priceStore;
		private readonly PriceWebSocketManager _wsManager;
		private readonly HttpClient _httpClient;
		private readonly string _tiingoApiKey;
		private readonly List<string> _fiatSymbols;
		private readonly List<string> _cryptoSymbols;
		private readonly Uri _forexWebSocketUri;
		private readonly Uri _cryptoWebSocketUri;
		public TiingoPriceService(
			ILogger<TiingoPriceService> logger,
			IPriceStore priceStore,
			PriceWebSocketManager wsManager,
			IHttpClientFactory httpClientFactory,
			IConfiguration configuration,
			InstrumentService instrumentService)
		{
			_logger = logger;
			_priceStore = priceStore;
			_wsManager = wsManager;
			_httpClient = httpClientFactory.CreateClient();

			//Get API key correctly from configuration
			_tiingoApiKey = configuration["TiingoApiKey"] ?? throw new ArgumentNullException(nameof(configuration), "Tiingo API key is missing.");

			//Load instruments by type
			_fiatSymbols = instrumentService.GetInstruments().Where(i => i.Service == "fiat").Select(i => i.Symbol.ToLower()).ToList();

			_cryptoSymbols = instrumentService.GetInstruments().Where(i => i.Service == "crypto").Select(i => i.Symbol.ToLower()).ToList();

			//Build each WS endpoint including the authentication key
			_forexWebSocketUri = new Uri($"wss://api.tiingo.com/fx?eventName=auth&authorization={_tiingoApiKey}");
			_cryptoWebSocketUri = new Uri($"wss://api.tiingo.com/crypto?eventName=auth&authorization={_tiingoApiKey}");
		}

		/// <summary>
		/// Start Tiingo price service for fetching forex and crypto prices
		/// </summary>
		/// <param name="stoppingToken">Cancellation token</param>
		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			//return;
			_logger.LogInformation("TiingoPriceService started.");

			// Start WebSockets for Crypto
			if (_cryptoSymbols.Count > 0)
			{
				_ = Task.Run(() => ConnectCryptoWebSocket(stoppingToken), stoppingToken);
			}

			if (_fiatSymbols.Count > 0)
			{
				// Handle Forex: Use WebSocket on weekdays, REST API on weekends
				_ = Task.Run(() => ManageForexUpdates(stoppingToken), stoppingToken);
			}
			_logger.LogInformation("TiingoPriceService is now managing price updates.");
		}

		/// <summary>
		/// Dynamically manage Forex updates (WebSocket on weekdays, API on weekends)
		/// </summary>
		/// <param name="stoppingToken">Cancellation token</param>
		private async Task ManageForexUpdates(CancellationToken stoppingToken)
		{
			bool marketClosedPreviously = false;

			while (!stoppingToken.IsCancellationRequested)
			{
				DayOfWeek today = DateTime.UtcNow.DayOfWeek;
				bool isMarketOpen = today is >= DayOfWeek.Monday and <= DayOfWeek.Friday;

				if (isMarketOpen)
				{
					_logger.LogInformation("Forex market is open. Using WebSockets.");
					await ConnectForexWebSocket(stoppingToken);
					marketClosedPreviously = false; // Reset flag when market reopens
				}
				else
				{
					_logger.LogInformation("Forex market is closed.");

					// Only fetch prices once when the market closes
					if (!marketClosedPreviously)
					{
						_logger.LogInformation("Fetching last known prices since market just closed.");
						await FetchForexPricesFromApi();
						marketClosedPreviously = true; // Set flag to prevent redundant calls
					}
					else
					{
						_logger.LogInformation("Market is still closed. Using cached prices.");
					}
				}

				// Wait before re-evaluating market status
				await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
			}
		}

		/// <summary>
		/// Connect to Tiingo WebSocket for Forex prices
		/// </summary>
		/// <param name="stoppingToken">Cancellation token</param>
		private async Task ConnectForexWebSocket(CancellationToken stoppingToken)
		{
			await ConnectWebSocket(_forexWebSocketUri, _fiatSymbols, "forex", stoppingToken);
		}

		/// <summary>
		/// Connect to Tiingo WebSocket for Crypto prices
		/// </summary>
		/// <param name="stoppingToken">Cancellation token</param>
		private async Task ConnectCryptoWebSocket(CancellationToken stoppingToken)
		{
			await ConnectWebSocket(_cryptoWebSocketUri, _cryptoSymbols, "crypto", stoppingToken);
		}

		/// <summary>
		/// Generic method to connect and subscribe to a WebSocket
		/// </summary>
		/// <param name="webSocketUri">WebSocket URI</param>
		/// <param name="symbols">List of symbols to subscribe</param>
		/// <param name="serviceType">Service type (forex or crypto)</param>
		/// <param name="stoppingToken">Cancellation token</param>
		private async Task ConnectWebSocket(Uri webSocketUri, List<string> symbols, string serviceType, CancellationToken stoppingToken)
		{
			ClientWebSocket? socket = new ClientWebSocket();
			while (!stoppingToken.IsCancellationRequested)
			{
				try
				{
					_logger.LogInformation("Connecting to Tiingo {ServiceType} WebSocket...", serviceType);
					await socket.ConnectAsync(webSocketUri, stoppingToken);
					_logger.LogInformation("Connected to Tiingo {ServiceType} WebSocket.", serviceType);

					// Subscribe to symbols
					await SubscribeToSymbols(socket, symbols, serviceType);

					var buffer = new byte[4096];
					while (socket.State == WebSocketState.Open && !stoppingToken.IsCancellationRequested)
					{
						var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), stoppingToken);
						if (result.MessageType == WebSocketMessageType.Close)
						{
							_logger.LogWarning("Tiingo {ServiceType} WebSocket closed. Reconnecting...", serviceType);
							break;
						}

						var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
						await ProcessWebSocketMessage(json, serviceType);
					}
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, "Error in Tiingo {ServiceType} WebSocket connection. Retrying in 5 seconds...", serviceType);
					await Task.Delay(5000, stoppingToken);
				}
			}
		}

		/// <summary>
		/// Fetch Forex prices via REST API when the market is closed
		/// </summary>
		/// <param name="symbol">Optional symbol for fetching a single price</param>
		public async Task FetchForexPricesFromApi(string symbol = null)
		{
			bool isMarketClosed = !(DateTime.UtcNow.DayOfWeek is >= DayOfWeek.Monday and <= DayOfWeek.Friday);
			var symbols = _fiatSymbols;

			if (!string.IsNullOrEmpty(symbol))
				symbols = symbols.Where(w => w.ToLower() == symbol.ToLower()).ToList();

			foreach (var fiatSymbol in symbols)
			{
				var cachedPrice = _priceStore.GetPrice(fiatSymbol);
				// If market is closed, check if we already have a cached price
				if (isMarketClosed && cachedPrice > 0)
				{
					_logger.LogInformation("Market is closed. Using cached price for {Symbol}: {Price}", fiatSymbol, cachedPrice);
					continue; // ✅ Skip unnecessary API calls
				}

				// Fetch from API if price is missing or market is open
				string url = $"https://api.tiingo.com/tiingo/fx/top?tickers={fiatSymbol}&token={_tiingoApiKey}";
				var response = await _httpClient.GetAsync(url);

				if (!response.IsSuccessStatusCode)
				{
					_logger.LogWarning("Failed to fetch {Symbol} price from Tiingo.", fiatSymbol);
					continue;
				}

				var json = await response.Content.ReadAsStringAsync();
				var doc = JsonDocument.Parse(json);

				if (doc.RootElement.GetArrayLength() > 0 && doc.RootElement[0].TryGetProperty("midPrice", out var priceProperty))
				{
					decimal price = priceProperty.GetDecimal();
					_priceStore.UpdatePrice(fiatSymbol, price);
					_logger.LogInformation("Updated Forex price for {Symbol}: {Price}", fiatSymbol, price);
				}
				else
				{
					_logger.LogWarning("No valid price data found for {Symbol} in Tiingo API response.", fiatSymbol);
				}
			}
		}


		/// <summary>
		/// Subscribe to symbols for WebSocket updates
		/// </summary>
		/// <param name="socket">WebSocket instance</param>
		/// <param name="symbols">List of symbols</param>
		/// <param name="serviceType">Service type (forex or crypto)</param>
		private async Task SubscribeToSymbols(ClientWebSocket socket, List<string> symbols, string serviceType)
		{
			if (socket.State != WebSocketState.Open)
			{
				_logger.LogError("Cannot subscribe to Tiingo {ServiceType} WebSocket. WebSocket is not open.", serviceType);
				return;
			}

			var subscribeMessage = new
			{
				eventName = "subscribe",
				authorization = _tiingoApiKey,
				eventData = new
				{
					tickers = symbols,
					topOfBook = true,
					priceUpdates = true
				}
			};

			var messageJson = JsonSerializer.Serialize(subscribeMessage);
			var messageBuffer = Encoding.UTF8.GetBytes(messageJson);

			await socket.SendAsync(new ArraySegment<byte>(messageBuffer), WebSocketMessageType.Text, true, CancellationToken.None);
			_logger.LogInformation("Subscribed to Tiingo {ServiceType} WebSocket for symbols: {Symbols}", serviceType, string.Join(", ", symbols));
		}

		/// <summary>
		/// Process WebSocket messages for both Forex & Crypto
		/// </summary>
		/// <param name="jsonString">Received JSON message</param>
		/// <param name="serviceType">Service type (forex or crypto)</param>
		private async Task ProcessWebSocketMessage(string jsonString, string serviceType)
		{
			try
			{
				using var doc = JsonDocument.Parse(jsonString);
				var root = doc.RootElement;

				//Ignore non-price messages
				if (root.TryGetProperty("messageType", out var messageTypeProperty))
				{
					string messageType = messageTypeProperty.GetString() ?? "";
					if (messageType == "I") // Ignore subscription confirmation messages
					{
						_logger.LogInformation("Received subscription confirmation for {ServiceType}. Ignoring message.", serviceType);
						return;
					}
				}

				//Process actual price data
				if (root.TryGetProperty("data", out var dataProperty) && dataProperty.ValueKind == JsonValueKind.Array)
				{
					var dataArray = dataProperty.EnumerateArray().ToArray();

					if (dataArray.Length < 6)
					{
						_logger.LogWarning("Received invalid {ServiceType} message format: {Message}", serviceType, jsonString);
						return;
					}

					//Extract values correctly
					string symbol = dataArray[1].GetString()?.ToLower() ?? "";
					decimal price = dataArray[5].GetDecimal(); // Correct floating-point parsing

					//Store price in PriceStore
					_priceStore.UpdatePrice(symbol, price);
					_logger.LogInformation("Updated {ServiceType} price for {Symbol}: {Price}", serviceType, symbol, price);

				}
				else
				{
					_logger.LogWarning("Received unexpected message format from {ServiceType}: {Message}", serviceType, jsonString);
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Failed to process Tiingo {ServiceType} message. Raw message: {Message}", serviceType, jsonString);
			}
		}
	}
}
