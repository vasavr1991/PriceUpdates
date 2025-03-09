using System.Net.WebSockets;
using System.Collections.Concurrent;
using System.Text;
using System.Threading.Tasks;

namespace PriceUpdates.API.Services
{
	public class PriceWebSocketManager
	{
		private ConcurrentDictionary<string, WebSocket> _clients = new();

		/// <summary>
		/// Add WebSocket client to the manager
		/// </summary>
		/// <param name="socket">WebSocket instance</param>
		/// <returns>Connection ID</returns>
		public string AddSocket(WebSocket socket)
		{
			var connectionId = Guid.NewGuid().ToString();
			_clients[connectionId] = socket;
			return connectionId;
		}

		/// <summary>
		/// Remove WebSocket client from the manager
		/// </summary>
		/// <param name="connectionId">Connection ID</param>
		public void RemoveSocket(string connectionId)
		{
			_clients.TryRemove(connectionId, out _);
		}

		/// <summary>
		/// Broadcast message to all WebSocket clients
		/// </summary>
		/// <param name="message">Message to broadcast</param>
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