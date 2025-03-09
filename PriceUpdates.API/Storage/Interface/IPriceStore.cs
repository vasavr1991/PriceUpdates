using System.Collections.Concurrent;

namespace PriceUpdates.API.Storage.Interface
{
	public interface IPriceStore
	{
		decimal? GetPrice(string symbol);
		void UpdatePrice(string symbol, decimal price);
		Dictionary<string, decimal> GetAllPrices();
	}
}
