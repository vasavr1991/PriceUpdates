using PriceUpdates.Common.Models;
using System.Diagnostics.Metrics;
using System.Text.Json;

namespace PriceUpdates.API.Services
{
	public class InstrumentService
	{
		private readonly ILogger<InstrumentService> _logger;
		private readonly string _jsonFilePath = "Data/instruments.json";
		private List<InstrumentModel> _instruments = new();

		public InstrumentService(ILogger<InstrumentService> logger)
		{
			_logger = logger;
			LoadInstruments();
		}

		// Load instruments from the JSON file
		private void LoadInstruments()
		{
			try
			{
				if (File.Exists(_jsonFilePath))
				{
					var json = File.ReadAllText(_jsonFilePath);
					_instruments = Newtonsoft.Json.JsonConvert.DeserializeObject<List<InstrumentModel>>(json) ?? new List<InstrumentModel>();
					_logger.LogInformation("Loaded {Count} instruments from JSON file.", _instruments.Count);
				}
				else
				{
					_logger.LogWarning("Instruments JSON file not found at {Path}", _jsonFilePath);
					_instruments = new List<InstrumentModel>();
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error loading instruments from JSON.");
			}
		}
		public List<InstrumentModel> GetInstruments() => _instruments;
	}
}
