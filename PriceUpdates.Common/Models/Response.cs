using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PriceUpdates.Common.Models
{
	public class Response<T>
	{
		public string DisplayMessage { get; set; }
		public string LogMessage { get; set; }
		public Exception Exception { get; set; }
		public T Data { get; set; }
		public DateTime ResponseDate { get; set; } = DateTime.UtcNow;
		public ResponseStatus Status { get; set; } = ResponseStatus.Success;
		public System.Net.HttpStatusCode HttpStatus { get; set; } = System.Net.HttpStatusCode.OK;
	}
	public enum ResponseStatus
	{
		Success,
		InternalError,
		RecordNotFound,
		Unauthorized,
		Failed
	}

}
