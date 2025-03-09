using PriceUpdates.API.Storage.Interface;
using System.Collections.Concurrent;

namespace PriceUpdates.API.Storage.Implementation
{
	public class PriceStore : IPriceStore
	{
		private readonly ConcurrentDictionary<string, decimal> _prices = new();

		public void UpdatePrice(string symbol, decimal price)
		{
			_prices[symbol] = price;
		}

		public decimal? GetPrice(string symbol)
		{
			return _prices.TryGetValue(symbol, out var price) ? price : null;
		}

		public Dictionary<string, decimal> GetAllPrices()
		{
			return new Dictionary<string, decimal>(_prices);
		}
	}
}
