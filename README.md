# MouseProfiles Cloud API

ASP.NET Core 8 Web API providing cloud backup and sync for the [MouseProfiles](../MouseProfiles) desktop app. Users register, verify their email, log in with JWT, and sync their mouse button presets to the cloud. Swagger UI is the web interface; the WinForms app is the client.

## Tech stack

| Concern | Choice |
|---------|--------|
| Framework | ASP.NET Core 8 Web API |
| Data access | Dapper + MySqlConnector 2.6.1 |
| Database | MySQL |
| Auth | JWT bearer + BCrypt (no ASP.NET Identity) |
| Email | Brevo REST API via plain `HttpClient` |
| Config | Strongly-typed Options with `ValidateOnStart()` |
| Errors | ProblemDetails throughout, global exception handler |
| API docs | Swagger / Swashbuckle with JWT bearer |
| Tests | xUnit + Moq + FakeTimeProvider — 28 tests, 0 failures |

## Project structure

```
MouseProfilesCloud/
├── src/MouseProfilesCloud/
│   ├── Controllers/           AuthController, ProfilesController (thin)
│   ├── Data/Db.cs             Opens MySqlConnection for Dapper
│   ├── Models/                AppUser, Profile, ServiceResult<T>
│   ├── Options/               JwtOptions, BrevoOptions, AppOptions (ValidateOnStart)
│   ├── Repositories/          IUserRepository, IProfileRepository + implementations
│   ├── Services/              AuthService, ProfileService, TokenService, BrevoEmailService
│   ├── GlobalExceptionHandler.cs
│   ├── Program.cs             DI wiring, JWT, Swagger
│   ├── appsettings.json       git-ignored — real credentials go here
│   └── appsettings.sample.json
├── tests/MouseProfilesCloud.Tests/
│   ├── Auth/AuthServiceTests.cs      15 tests
│   ├── Auth/TokenServiceTests.cs      1 test
│   └── Profiles/ProfileServiceTests.cs 12 tests
└── sql/
    └── 001_initial_schema.sql
```

## API endpoints

### Auth (no token required)

| Method | Route | Notes |
|--------|-------|-------|
| POST | `/auth/register` | Creates unverified account, sends verification email |
| GET | `/auth/verify-email?token=…` | Activates account |
| POST | `/auth/login` | Returns JWT (7-day expiry) |
| POST | `/auth/resend-verification` | Never reveals whether the email exists |

### Profiles (JWT required)

| Method | Route | Notes |
|--------|-------|-------|
| GET | `/profiles` | List metadata only (id, name — no config blob) |
| GET | `/profiles/{id}` | Full config JSON for one profile |
| POST | `/profiles/sync` | Batch upsert + delete in one transaction |

#### Sync request shape

```json
{
  "upsert": [
    { "id": "guid-from-desktop", "name": "Gaming", "configJson": "{...}" }
  ],
  "delete": ["guid-to-remove"]
}
```

Returns `{ "created": 1, "updated": 0, "deleted": 0 }`.

## Design decisions

**No ASP.NET Identity.** Two tables, plain BCrypt + JWT — Identity is too heavy for this scope.

**GUIDs from the desktop.** Profile IDs are assigned by the WinForms app, not the server. This lets `/profiles/sync` match local profiles to cloud records without a separate lookup.

**Timing-safe login.** `BCrypt.Verify` is called even for unknown emails (using a dummy hash at the same work factor) to prevent email enumeration via response timing.

**Lazy sweep.** Unverified accounts older than 5 days are deleted at the start of register/login/verify — no background job needed at this scale.

**`ValidateOnStart()`** on all Options bindings: a missing config key fails at startup, not at the first request that needs it.

**`MapInboundClaims = false`** in JWT: keeps `sub` as `"sub"` rather than mapping it to `NameIdentifier`. `CurrentUser` reads `JwtRegisteredClaimNames.Sub` directly.

**Cross-user isolation in sync.** The upsert uses `ON DUPLICATE KEY UPDATE … IF(UserId = @UserId, …)` so a GUID collision (vanishingly unlikely with random GUIDs) can never overwrite another user's data.

## Known limitations

- No token revocation — JWTs are stateless; logout deletes the local token file only.
- No rate limiting on auth endpoints.
- The desktop app stores the JWT as plaintext in `%APPDATA%\MouseProfiles\cloud-token.json`.
- `POST /auth/register` returns HTTP 409 on a duplicate email, so email existence is inferable from the status code on that one endpoint. All other auth paths avoid leaking it.

## Running locally

```powershell
# 1. Copy appsettings.sample.json → appsettings.json and fill in your values
# 2. Run the schema
mysql -h <host> -u <user> -p <database> < sql/001_initial_schema.sql
# 3. Start the API
dotnet run --project src/MouseProfilesCloud/MouseProfilesCloud.csproj
# Swagger UI: http://localhost:5110/swagger
```

## Running tests

```powershell
dotnet test
```

## Configuration keys

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=...;Database=...;User=...;Password=...;"
  },
  "Jwt": {
    "Secret": "<36+ char random string>",
    "Issuer": "MouseProfilesCloud",
    "Audience": "MouseProfilesCloudUsers"
  },
  "Brevo": {
    "ApiKey": "<xkeysib-...>",
    "FromEmail": "no-reply@example.com",
    "FromName": "MouseProfiles Cloud"
  },
  "App": {
    "BaseUrl": "http://localhost:5110"
  }
}
```
