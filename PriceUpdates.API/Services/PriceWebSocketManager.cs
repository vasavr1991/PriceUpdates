using System.Net.WebSockets;
using System.Collections.Concurrent;
using System.Text;
using System.Threading.Tasks;

namespace PriceUpdates.API.Services
{
	public class PriceWebSocketManager
	{
		private ConcurrentDictionary<string, WebSocket> _clients = new();

		public string AddSocket(WebSocket socket)
		{
			var connectionId = Guid.NewGuid().ToString();
			_clients[connectionId] = socket;
			return connectionId;
		}

		public void RemoveSocket(string connectionId)
		{
			_clients.TryRemove(connectionId, out _);
		}

		public async Task BroadcastMessageAsync(string message)
		{
			var messageBuffer = Encoding.UTF8.GetBytes(message);

			foreach (var (connId, socket) in _clients)
			{
				if (socket.State == WebSocketState.Open)
				{
					var segment = new ArraySegment<byte>(messageBuffer);
					await socket.SendAsync(segment, WebSocketMessageType.Text, true, default);
				}
				else
				{
					_clients.TryRemove(connId, out _);
				}
			}
		}
	}
}