using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PriceUpdates.Common.Models;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace PriceUpdates.Web.Controllers
{
	public class PriceController : Controller
	{
		private readonly HttpClient _httpClient;
		private readonly ILogger<PriceController> _logger;
		private readonly string _apiBaseUrl = "http://localhost:7800/api/instruments"; // API Base URL

		public PriceController(IHttpClientFactory httpClientFactory, ILogger<PriceController> logger)
		{
			_httpClient = httpClientFactory.CreateClient();
			_logger = logger;
		}

		/// <summary>
		/// Fetch list of available instruments from API
		/// </summary>
		public async Task<IActionResult> Index()
		{
			// API URL (Ensure this matches your API project URL)
			string apiUrl = "http://localhost:7800/api/instruments";

			// Fetch the list of instruments
			var response = await _httpClient.GetAsync(apiUrl);
			var json = await response.Content.ReadAsStringAsync();
			var instruments = JsonSerializer.Deserialize<List<InstrumentModel>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

			return View(instruments);
		}

		/// <summary>
		/// Fetch the latest price for a specific symbol from the API
		/// </summary>
		/// <param name="symbol">Instrument symbol</param>
		/// <returns>Price data in JSON format</returns>
		[HttpGet("price/{symbol}/get")]
		public async Task<IActionResult> GetPrice(string symbol)
		{
			string url = $"{_apiBaseUrl}/{symbol}/price";
			_logger.LogInformation("Fetching latest price for {Symbol} from API", symbol);

			try
			{
				var response = await _httpClient.GetAsync(url);
				if (!response.IsSuccessStatusCode)
				{
					_logger.LogWarning("Failed to fetch price for {Symbol}. API returned {StatusCode}", symbol, response.StatusCode);
					return NotFound(new { message = $"No price available for {symbol}" });
				}

				var json = await response.Content.ReadAsStringAsync();
				return Content(json, "application/json");
			}
			catch (HttpRequestException ex)
			{
				_logger.LogError(ex, "Error communicating with API for {Symbol}", symbol);
				return StatusCode(500, new { message = "Error fetching price data" });
			}
		}
	}
}