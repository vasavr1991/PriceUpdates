# PriceUpdates

**PriceUpdates** is a real-time financial instrument price update system demonstrating how to:
- **Fetch data from Tiingo** via REST API and WebSockets.
- **Broadcast live updates** over WebSockets to clients.
- **Serve data** through a **.NET Web API** and a **frontend** in `.NET MVC`.

---
## Table of Contents

1. [Solution Overview](#solution-overview)  
2. [Getting Started](#getting-started)  
3. [Running the API](#running-the-api)  
4. [Running the Web App](#running-the-web-app)  
5. [API Usage](#api-usage)  
6. [Live Price Updates (WebSocket)](#live-price-updates-websocket)  
7. [Logging](#logging)  
8. [Project Structure](#project-structure)  
9. [Contributing](#contributing)  
10. [License](#license) 

---

## 1. Solution Overview

**PriceUpdates** has three main parts:

1. **PriceUpdates.API**  
   - A **.NET Web API** that:
     - Maintains instrument prices in-memory and via WebSocket.
     - Periodically fetches data from **Tiingo** using **REST API** (for forex when markets are closed) and **WebSockets** (for crypto, and also forex when markets are open).
     - Has a **PriceDistributorService** to broadcast the latest prices to WebSocket clients (`ws://localhost:7800/ws/prices`).

2. **PriceUpdates.Web**  
   - A **.NET MVC** frontend application.
   - Displays available instruments and **live updating** prices.
   - Communicates with the API for manual “Update Price” calls and listens on WebSocket for real-time data.

3. **PriceUpdates.Common**  
   - A shared library (class library) for common models, including `InstrumentModel`.

---

## 2. Getting Started

### Prerequisites

- **.NET 9** installed  
  - Verify with:
    ```bash
    dotnet --version
    ```
- A valid **Tiingo API key**  
  - Place it in **`appsettings.json`** (in the API project) or as an **environment variable**.  
- **Visual Studio / VS Code** or any other .NET-capable IDE for local development.

### Installation Steps

1. **Clone** this repository:
   ```bash
   git clone https://github.com/vasavr1991/PriceUpdates.git
   ```
2. **Restore dependencies**:
   ```bash
   cd PriceUpdates
   dotnet restore
   ```
3. **Check appsettings** in `PriceUpdates.API` to ensure your `TiingoApiKey` is correctly set.

---

## 3. Running the API

### Via Terminal

1. Navigate to **PriceUpdates.API**:
   ```bash
   cd PriceUpdates.API
   dotnet run
   ```
2. The API runs by default on `http://localhost:7800`.

3. If in **Development mode**, open:
   ```
   http://localhost:7800/swagger
   ```
   to see the Swagger UI.

### Via Visual Studio / VS Code

- Set **PriceUpdates.API** as the startup project.
- Press **F5** to start in Debug mode.
- Swagger: `http://localhost:7800/swagger`

---

## 4. Running the Web App

### Via Terminal

1. Open a **new terminal**:
   ```bash
   cd PriceUpdates.Web
   dotnet run
   ```
2. The web app will be served from `http://localhost:7900`.

### Via Visual Studio / VS Code

- Set **PriceUpdates.Web** as your startup project.
- Press **F5** to run with debugging.
- Visit `http://localhost:7900` in your browser to see the live prices page.

### Configuration

- The **`PriceController.cs`** in **PriceUpdates.Web** references the API base URL:
  ```csharp
  private readonly string _apiBaseUrl = "http://localhost:7800/api/instruments";
  ```
  If your API runs on a different port or URL, update `_apiBaseUrl` accordingly.

---

## 5. API Usage

**Key Endpoints** in `PriceUpdates.API`:

1. **`GET /api/instruments`**  
   Returns a list of all known instruments (symbol, name, service).

2. **`GET /api/instruments/prices`**  
   Returns a dictionary of `{ "SYMBOL": PRICE }` for all known instruments.

3. **`GET /api/instruments/{symbol}/price`**  
   Fetches the current price for the given `symbol`.

### Example Usage

```bash
curl http://localhost:7800/api/instruments
curl http://localhost:7800/api/instruments/btcusdt/price
```

---

## 6. Live Price Updates (WebSocket)

- **Endpoint**: `ws://localhost:7800/ws/prices`
- The **PriceDistributorService** in **PriceUpdates.API** continually broadcasts updated prices to all connected WebSocket clients.
- **PriceUpdates.Web**:
  - Opens a WebSocket connection on page load (`Index.cshtml`).
  - Receives JSON objects containing the latest prices, updating the UI in real-time.

**Example** broadcast message:
```json
{
  "EURUSD": 1.0652,
  "BTCUSDT": 23782.115,
  "USDJPY": 145.78
}
```

---

## 7. Logging

This project uses **Serilog** for structured logging:

- **Log file**: `Logs/log-.txt` (auto-rotates daily).
- **Log levels**:
  - `Information` for normal operations
  - `Warning` for unusual but recoverable issues
  - `Error` for exceptions
- Each **controller** and **service** logs relevant events:
  - Instrument retrieval
  - WebSocket connections, disconnections
  - Price fetch successes and failures
  - Broadcasting cycles

Configure or adjust logs in:
- `Program.cs`
- `appsettings.json`
- environment variables.

---

## 8. Project Structure

```
PriceUpdates/
├── PriceUpdates.API/               # REST API & WebSocket service
│   ├── Controllers/                # InstrumentController, etc.
│   ├── Services/                   # TiingoPriceService, PriceDistributorService, PriceWebSocketManager
│   ├── Storage/                    # PriceStore, IPriceStore
│   ├── Program.cs                  # Entry point for .NET
│   └── appsettings.json            # TiingoApiKey, logging config
├── PriceUpdates.Common/            # Shared library
│   └── Models/                     # InstrumentModel, etc.
├── PriceUpdates.Web/               # ASP.NET Core MVC frontend
│   ├── Controllers/                # PriceController, etc.
│   ├── Views/                      # Razor Views
│   └── Program.cs                  # Entry point for the web front-end
└── README.md                       # Documentation
```

---

