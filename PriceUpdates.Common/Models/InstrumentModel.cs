using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PriceUpdates.Common.Models
{
	public class InstrumentModel
	{
		private string _symbol = string.Empty;

		public string Symbol
		{
			get => _symbol;
			set => _symbol = value?.ToLower() ?? string.Empty;
		}
		public string Name { get; set; } = string.Empty;
		public string Service { get; set; } = string.Empty;
	}
}
