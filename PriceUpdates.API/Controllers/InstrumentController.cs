using Microsoft.AspNetCore.Mvc;
using PriceUpdates.API.Services;
using PriceUpdates.API.Storage.Interface;
using Microsoft.Extensions.Logging;
using System.Linq;
using System.Net.Http;
using System.Text.Json;

namespace PriceUpdates.API.Controllers
{
	[Route("api/instruments")]
	[ApiController]
	public class InstrumentController : ControllerBase
	{
		private readonly InstrumentService _instrumentService;
		private readonly TiingoPriceService _tiingoPriceService;
		private readonly IPriceStore _priceStore;
		private readonly ILogger<InstrumentController> _logger;

		public InstrumentController(InstrumentService instrumentService, TiingoPriceService tiingoPriceService, IPriceStore priceStore, ILogger<InstrumentController> logger)
		{
			_instrumentService = instrumentService;
			_tiingoPriceService = tiingoPriceService;
			_priceStore = priceStore;
			_logger = logger;
		}

		[HttpGet]
		public IActionResult GetInstruments()
		{
			var instruments = _instrumentService.GetInstruments();
			_logger.LogInformation("Retrieved {Count} instruments.", instruments.Count());
			return Ok(instruments);
		}

		[HttpGet("prices")]
		public IActionResult GetAllPrices()
		{
			var prices = _priceStore.GetAllPrices();
			if (prices.Count == 0)
			{
				_logger.LogWarning("No prices available.");
				return NotFound(new { message = "No price data available." });
			}

			_logger.LogInformation("Retrieved {Count} instrument prices.", prices.Count);
			return Ok(prices);
		}

		[HttpGet("{symbol}/price")]
		public async Task<IActionResult> GetPrice(string symbol)
		{
			var price = _priceStore.GetPrice(symbol);

			if (price.HasValue)
			{
				_logger.LogInformation("Returned cached price for {Symbol}: {Price}", symbol, price);
				return Ok(new { symbol, price });
			}
			else
			{
				_logger.LogWarning("Price for {Symbol} not found in cache. Fetching from Tiingo...", symbol);

				// Fetch price dynamically for fiat currencies
				var isFiat = _instrumentService.GetInstruments().Any(i => i.Symbol == symbol && i.Service == "fiat");

				if (isFiat)
				{
					await _tiingoPriceService.FetchForexPricesFromApi(symbol);
					var latestPrice = _priceStore.GetPrice(symbol);
					if (latestPrice.HasValue)
					{
						_priceStore.UpdatePrice(symbol, latestPrice.Value); // Store for caching
						return Ok(new { symbol, price = latestPrice.Value });
					}
				}

				return NotFound(new { message = $"No price available for {symbol}" });
			}
		}
	}
}
