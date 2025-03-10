// Connection status element
const statusElement = document.getElementById('connectionStatus');

function connectWebSocket() {
	const socket = new WebSocket("ws://localhost:7800/ws/prices");

	socket.onopen = () => {
		statusElement.textContent = 'Connected';
		statusElement.parentElement.className = 'badge bg-success bg-opacity-10 text-success fs-7 ms-3';
	};

	socket.onmessage = (event) => {
		const data = JSON.parse(event.data);
		for (const symbol in data) {
			setPrice(symbol, data[symbol]);
		}
	};

	socket.onclose = () => {
		statusElement.textContent = 'Reconnecting...';
		statusElement.parentElement.className = 'badge bg-danger bg-opacity-10 text-danger fs-7 ms-3';
		setTimeout(connectWebSocket, 5000);
	};
}

async function updatePrice(symbol) {
	const btn = document.querySelector(`button[onclick="updatePrice('${symbol}')"]`);
	btn.innerHTML = '<i class="bi bi-arrow-repeat fs-13 spin"></i>';
	btn.disabled = true;
	//debugger;
	try {
		const response = await fetch(`/price/${symbol}/get/`);
		if (response.ok) {
			const data = await response.json();
			setPrice(symbol, data.price);
		}
	} finally {
		setTimeout(() => {
			btn.innerHTML = '<i class="bi bi-arrow-repeat fs-13"></i>';
			btn.disabled = false;
		}, 1000);
	}
}

function setPrice(symbol, price) {
	//debugger;
	const priceElement = document.getElementById(`price-${symbol}`);
	if (!priceElement) return;

	const previousPrice = parseFloat(priceElement.dataset.currentPrice) || 0;
	const newPrice = parseFloat(price);

	priceElement.innerHTML = `
			<div class="d-flex align-items-center gap-2">
				<span class="fs-14 fw-500 ${newPrice > previousPrice ? 'text-success' : 'text-danger'}">
					$${newPrice.toFixed(4)}
				</span>
				<i class="bi bi-arrow-${newPrice > previousPrice ? 'up' : 'down'}-right fs-12 ${newPrice > previousPrice ? 'text-success' : 'text-danger'
		}"></i>
			</div>
		`;

	priceElement.dataset.currentPrice = newPrice;
	priceElement.classList.add('price-update');
	setTimeout(() => priceElement.classList.remove('price-update'), 500);
}

connectWebSocket();