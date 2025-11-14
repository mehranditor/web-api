## WebApplication1 — ASP.NET Core 8 Web API (JWT, Identity, RAG, Redis, Swagger)

An opinionated ASP.NET Core 8 Web API demonstrating:
- **Authentication/Authorization**: ASP.NET Core Identity + JWT (access/refresh), role seeding (Admin/User), admin key-to-JWT.
- **User management**: CRUD + paging/filtering/sorting via MediatR and `UserRepository`.
- **Rate limiting**: Simple Redis-backed middleware for `/api/user` endpoints.
- **RAG utilities**: In-memory vector store, Ollama-based embeddings and generation, semantic search, and log summarization.
- **Observability**: Serilog (console + rolling files).
- **Docs**: Swagger/OpenAPI with JWT security scheme and response examples.

## Screenshot
![App overview](./Screenshot%202025-10-02%20at%2014-46-42%20Log%20Analysis%20%26%20Summarization%20API.png)

### Contents
- Controllers: `AuthController`, `AdminController`, `UserController`, `ChatController`, `RAGController`, `WeatherForecastController`
- Middleware: `ExceptionMiddleware`, `TokenValidationMiddleware`, `RateLimitingMiddleware`
- Services: `RAGService`, `OllamaEmbeddingService`, `InMemoryVectorStore`, `UserService`
- Data: EF Core `authdbcontext` with Identity + `RefreshToken` table (SQLite)
- Realtime: `NotificationHub` (SignalR) scaffold

## Prerequisites
- .NET SDK 8.0+
- SQLite (file-based; created on first run): `authdb.sqlite`
- Redis (for rate limiting and caching) at `localhost:6379` or update `appsettings.json`
- Ollama running locally for LLM and embeddings:
  - API: `http://127.0.0.1:11434`
  - Models: `llama3.1:8b` and `nomic-embed-text` (adjust in `appsettings.json` → `Ollama`)

## Quick start
```bash
dotnet restore
dotnet build -c Release

# Optionally set port (defaults to 5025)
$env:PORT=5025  # PowerShell on Windows

dotnet run
```

Open Swagger UI: `http://localhost:5025/swagger`

On first run the app will:
- Ensure the SQLite database file exists (`authdb.sqlite`).
- Seed Identity roles `Admin` and `User`.
- Bind to `http://0.0.0.0:<PORT>`; HTTPS is enabled only if an HTTPS binding is present.
- Write logs to `logs/app-<port>.log`.

## Configuration
Edit `appsettings.json` (or environment-specific overrides) to suit your environment:
- `Jwt`:
  - `Key`: Replace with a strong secret (≥32 chars). Do not commit real secrets.
  - `Issuer`, `Audience`: Must match values used when issuing/validating tokens.
- `AdminAuth.SecretKey`: The shared secret for `POST /api/admin/auth` to mint an admin JWT.
- `Redis.ConnectionString`: Redis endpoint for caching/rate limiting.
- `Ollama`: Base URL, default model(s), options, and timeouts.

Environment variables:
- `PORT`: Kestrel HTTP binding (defaults to 5025).
- `ASPNETCORE_ENVIRONMENT`: e.g., `Development`.

## Authentication flows
There are two complementary auth mechanisms:

- User credentials (Identity + JWT + refresh):
  - `POST /api/auth/register` → create a user (auto-assigned role `User`).
  - `POST /api/auth/login` → returns `{ accessToken, refreshToken }`.
  - `POST /api/auth/refresh` → returns a rotated pair; marks old refresh token revoked.

- Admin shared-secret to admin JWT:
  - `POST /api/admin/auth` with body `{ "key": "<AdminAuth.SecretKey>" }` → `{ token }` where token contains role `Admin`.

Attach the access token to requests:
```
Authorization: Bearer <accessToken>
```

## Endpoints

### AuthController (`/api/auth`)
- `POST /register` — Register a user: `{ userName, email, password }`.
- `POST /login` — Login: `{ userName, password }` → `{ accessToken, refreshToken }`.
- `POST /refresh` — Rotate tokens: `{ refreshToken }`.

### AdminController (`/api/admin`)
- `POST /auth` — Exchange admin secret for an admin JWT: `{ key }`.

### UserController (`/api/user`)
- `GET /test-redis` — Quick Redis connectivity check.
- `GET /` — List users. Auth required.
- `GET /{id}` — Get user by Id. Auth required.
- `POST /` — Create user (AllowAnonymous for demo). Body: `{ userName, email, password }`.
- `GET /paged` — Paged/filter/sort list: `search`, `sortBy` (`username|email`), `sortOrder` (`asc|desc`), `pageNumber`, `pageSize`.
- `PUT /{id}` — Update user. Requires role `Admin`.
- `DELETE /{id}` — Delete user. Requires role `Admin`.

Rate limiting: Requests to `/api/user` are limited to 10/minute/IP via Redis. If Redis is not available, requests may fail with 500.

### ChatController (`/api/chat`)
- `GET /test-ollama` — Connectivity to Ollama (`/api/tags`).
- `POST /` — Weather-focused chat wrapper over Ollama `generate`. Auth required. Body: `{ message }`.

### RAGController (`/api/rag`)
- `POST /index` — Chunk, embed, and index text: `{ content, metadata? }` → `{ documentId }`.
- `POST /search` — Semantic search: `{ query, topK?, minScore? }` → `SearchResult[]`.
- `DELETE /document/{documentId}` — Delete all chunks for a document.
- `DELETE /clear` — Clear all indexed chunks. Requires role `Admin`.
- `GET /health` — Simple health check.
- `POST /summarize-logs` — Analyze log text and produce a summary; also indexes the summary.

### WeatherForecast
- `GET /weatherforecast` — Sample endpoint.

## Example requests

Register/login and call a protected endpoint:
```bash
curl -s -X POST http://localhost:5025/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{"userName":"alice","email":"a@a.com","password":"P@ssw0rd!"}'

TOKEN=$(curl -s -X POST http://localhost:5025/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"userName":"alice","password":"P@ssw0rd!"}' | jq -r .accessToken)

curl -s http://localhost:5025/api/user \
  -H "Authorization: Bearer $TOKEN"
```

Admin JWT via shared secret:
```bash
curl -s -X POST http://localhost:5025/api/admin/auth \
  -H "Content-Type: application/json" \
  -d '{"key":"SuperSecretAdminKey123"}'
```

RAG index + search:
```bash
curl -s -X POST http://localhost:5025/api/rag/index \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"content":"Redis is an in-memory data store used as a database, cache, and message broker."}'

curl -s -X POST http://localhost:5025/api/rag/search \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"query":"What is Redis used for?","topK":3}'
```

Chat (requires token):
```bash
curl -s -X POST http://localhost:5025/api/chat \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"message":"Weather in Berlin today?"}'
```

## Architecture notes
- `Program.cs` configures Serilog, Kestrel URLs, Swagger (with Bearer scheme), Identity + roles, JWT bearer auth, Redis cache, and middleware pipeline.
- `TokenValidationMiddleware` ensures `Authorization` header presence on protected routes (Swagger, auth endpoints, and root are excluded).
- `RateLimitingMiddleware` uses Redis to count requests by IP+path within a rolling window.
- `RAGService` uses `OllamaEmbeddingService` and `IVectorStore` (default `InMemoryVectorStore`) to index and search content; `SummarizeLogsAsync` can call Ollama to produce summaries with a safe fallback.
- `authdbcontext` extends `IdentityDbContext<IdentityUser>` and includes `DbSet<RefreshToken>`.
- `NotificationHub` is scaffolded; map it in the pipeline if you plan to use SignalR (e.g., `app.MapHub<NotificationHub>("/hubs/notifications");`).

## Running multiple instances (optional)
For Linux-based hosts, `start.sh` can run three instances on ports 5001–5003 and includes helper commands (`start|stop|restart|status|logs`). An example Nginx config is in `default` (HTTP→HTTPS redirect, upstream to 5001/5002).

## Troubleshooting
- Ollama not reachable: ensure Ollama is running and the `Ollama:Url` is correct. Test via `GET /api/chat/test-ollama`.
- Redis errors: start Redis (default `localhost:6379`) or disable rate limiting middleware.
- 401 Unauthorized: include `Authorization: Bearer <token>` header; check token expiry (30 minutes default).
- JWT key missing: set `Jwt:Key` (≥32 chars) in configuration or environment.

## License
MIT (or your preferred license)


