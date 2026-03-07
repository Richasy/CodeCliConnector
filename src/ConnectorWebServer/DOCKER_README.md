# CodeCliConnector Server

Relay server for remotely controlling [Claude Code](https://docs.anthropic.com/en/docs/build-with-claude/claude-code) instances from mobile devices.

CodeCliConnector acts as a bridge between your local Claude Code CLI sessions and your phone, enabling you to receive notifications and approve permission requests on the go.

## Architecture

```
┌──────────────┐     WebSocket      ┌─────────────────────┐     WebSocket      ┌──────────────┐
│  Claude Code  │ ◄──── Hook ────► │  ConnectorConsole /  │ ◄───────────────► │  This Server  │
│   (Local PC)  │                   │  ConnectorApp        │                   │   (Docker)    │
└──────────────┘                   └─────────────────────┘                   └──────┬───────┘
                                                                                     │
                                                                              WebSocket / REST
                                                                                     │
                                                                              ┌──────▼───────┐
                                                                              │  Android App  │
                                                                              │  (Your Phone) │
                                                                              └──────────────┘
```

## Quick Start

### Docker Run

```bash
docker run -d \
  --name codecliconnector-server \
  -p 8080:8080 \
  -v connector-data:/app/data \
  -e ServerSettings__PreSharedKey=your-secret-key-here \
  richasyz/codecliconnector-server:latest
```

### Docker Compose

```yaml
services:
  connector-server:
    image: richasyz/codecliconnector-server:latest
    container_name: codecliconnector-server
    restart: unless-stopped
    ports:
      - "8080:8080"
    volumes:
      - connector-data:/app/data
    environment:
      - ServerSettings__PreSharedKey=your-secret-key-here

volumes:
  connector-data:
```

Save as `docker-compose.yml` and run:

```bash
docker compose up -d
```

## Configuration

All settings are configurable via environment variables using the `ServerSettings__` prefix.

| Environment Variable | Default | Description |
|---|---|---|
| `ServerSettings__PreSharedKey` | `change-me-in-production` | **Required.** Pre-shared key for device registration authentication. All clients (ConnectorConsole, Android App) must use the same key to register. |
| `ServerSettings__TokenExpiryDays` | `3` | Access token lifetime in days. After expiry, devices must re-register. |
| `ServerSettings__HeartbeatTimeoutSeconds` | `30` | WebSocket heartbeat timeout. Clients not sending a heartbeat within this period are considered disconnected. |
| `ServerSettings__DatabasePath` | `/app/data/connector.db` | SQLite database file path. Mount `/app/data` as a volume to persist data. |
| `ServerSettings__MessageExpirySeconds` | `300` | Pending message lifetime (5 minutes). Undelivered messages expire after this period. |
| `ServerSettings__MessageCleanupDays` | `7` | Messages older than this are cleaned up by the background service. |
| `ServerSettings__HeartbeatCheckIntervalSeconds` | `10` | How often the server checks for timed-out connections. |
| `ServerSettings__MessageCleanupIntervalSeconds` | `60` | How often the server runs the message cleanup job. |
| `Logging__LogLevel__Default` | `Information` | Log level. Options: `Trace`, `Debug`, `Information`, `Warning`, `Error`, `Critical`. |

## Ports

| Port | Protocol | Description |
|---|---|---|
| `8080` | HTTP + WebSocket | REST API and WebSocket endpoint |

### API Endpoints

- `POST /api/auth/register` — Register a device (requires pre-shared key)
- `POST /api/auth/refresh` — Refresh access token
- `GET /api/devices` — List registered devices and their online status
- `GET /api/messages/pending` — Get pending offline messages
- `/ws/connect?token=xxx` — WebSocket connection for real-time messaging

## Data Persistence

The server uses SQLite for storing device registrations and message history. **Mount `/app/data` as a volume** to persist data across container restarts.

```bash
# Named volume (recommended)
-v connector-data:/app/data

# Or bind mount
-v /path/on/host:/app/data
```

## Reverse Proxy (Recommended)

For production use, place behind a reverse proxy (Nginx, Caddy, Traefik) with TLS:

```nginx
server {
    listen 443 ssl;
    server_name connector.example.com;

    ssl_certificate     /path/to/cert.pem;
    ssl_certificate_key /path/to/key.pem;

    location / {
        proxy_pass http://localhost:8080;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_read_timeout 86400;
    }
}
```

> **Important:** WebSocket connections require `Upgrade` and `Connection` headers to be forwarded. The `proxy_read_timeout` should be set high to prevent premature disconnection of long-lived WebSocket connections.

## Security Notes

- **Change the default pre-shared key** before deploying. This key is the primary authentication mechanism for device registration.
- The server uses token-based authentication. After registration with the pre-shared key, devices receive a time-limited access token.
- For production deployments, **always use TLS** (via reverse proxy) to encrypt traffic.
- The SQLite database contains device tokens — protect the data volume accordingly.

## Source Code

[https://github.com/Richasy/CodeCliConnector](https://github.com/Richasy/CodeCliConnector)
