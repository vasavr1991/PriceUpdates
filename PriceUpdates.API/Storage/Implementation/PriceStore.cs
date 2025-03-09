using PriceUpdates.API.Storage.Interface;
using System.Collections.Concurrent;

namespace PriceUpdates.API.Storage.Implementation
{
	public class PriceStore : IPriceStore
	{
		private readonly ConcurrentDictionary<string, decimal> _prices = new();

		/// <summary>
		/// Update price in-memory cache
		/// </summary>
		/// <param name="symbol">Instrument symbol</param>
		/// <param name="price">Latest price</param>
		public void UpdatePrice(string symbol, decimal price)
		{
			_prices[symbol] = price;
		}

		/// <summary>
		/// Get price from in-memory cache
		/// </summary>
		/// <param name="symbol">Instrument symbol</param>
		/// <returns>Price if available, otherwise null</returns>
		public decimal? GetPrice(string symbol)
		{
			return _prices.TryGetValue(symbol, out var price) ? price : null;
		}

		/// <summary>
		/// Get all prices from in-memory cache
		/// </summary>
		/// <returns>Dictionary of symbols and their prices</returns>
		public Dictionary<string, decimal> GetAllPrices()
		{
			return new Dictionary<string, decimal>(_prices);
		}
	}
}
