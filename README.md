# Marketplace
Marketplace web app where people display the items they wish to sell alongside their contact information.

Used technologies: ASP.NET Core with .NET 10, Entity Framework Core 10 using Npgsql PostgreSQL provider, Bootstrap 5, SignalR for real-time chat. The project uses MVC without a separate Web API.

## What is supported?
### 1. Different user roles
- Seller
- Premium
- Admin
### 2. Different functionality based on user role
- Example: Admins have admin panel and can manage other users
### 3. Flexible filtering based on:
- Category
- Price
- Location
- Min and Max price range
### 4. User profiles
Each user has a profile that can be visited. You can browse through the items they have put up for sale and choose to contact them via email or phone.
### 5. Real-time chat
- SignalR hub `/hubs/chat` with live Inbox and Thread
- Navbar unread badge + toast updates instantly
- Block/unblock (admins cannot be blocked), paginated Inbox (12) and Thread (50), scrollbars, responsive

## How to run (Linux / CachyOS dev — HTTP default)

1. Clone: `git clone https://github.com/nikoladevelops/Marketplace.git && cd Marketplace`
2. Restore: `dotnet restore`
3. Create `Marketplace/.env` (next to `Marketplace.csproj`):
   ```
   CONNECTION_STRING=Host=localhost;Database=marketplace;Username=postgres;Password=postgres
   # optional AI
   AI_API_URL=
   AI_API_KEY=
   ```
4. First-time setup (migrate + seed core):
   ```
   dotnet run --project Marketplace -- setup
   ```
   Seeds roles, users (`seller`/`premium`/`admin` — pass `aaaaaaA!1`), categories (`Furniture` … `Sports & Outdoors`). Idempotent — safe to re-run.

5. Then demo data (requires setup first):
   ```
   dotnet run --project Marketplace -- seed:demo            # 25 ads
   dotnet run --project Marketplace -- seed:demo --count 50 # 1..200
   # compat: dotnet run --project Marketplace -- --seed-demo=25
   ```

6. Start app:
   ```
   dotnet run --project Marketplace
   # opens http://localhost:5256 (dev default, no cert needed)
   ```

Order matters: **setup → seed:demo → run**. Help: `dotnet run --project Marketplace -- help`.

Windows / manual migrations alternative:
```
dotnet ef database update --project Marketplace
```
But the app also runs `MigrateAsync()` on startup, so `update-database` is optional.

### Test users after setup
Username: `seller` / pass `aaaaaaA!1` (Seller)  
Username: `premium` / pass `aaaaaaA!1` (Premium)  
Username: `admin` / pass `aaaaaaA!1` (Admin)

## Troubleshooting chat

Real-time chat runs over SignalR (`/hubs/chat`) and needs the page and the hub on the same scheme.

* Dev default is `http://localhost:5256` (no cert). Always open the app at that exact URL.
* If you re-enable `https://localhost:7256` in `Properties/launchSettings.json`, trust the dev cert once: `dotnet dev-certs https --trust` (Linux: also `certutil -d sql:$HOME/.pki/nssdb -A` for Firefox/NSS).
* If `Connection lost — reconnecting` appears, check browser Console ` [chat] start` and Network `POST /hubs/chat/negotiate` (should be `200` when logged in, `401` anon is normal).
* Only one `dotnet run` at a time — kill stale holders if `Failed to bind to address … already in use`: `lsof -ti :5256 | xargs -r kill -9`.

## Production

`appsettings.Production.json` binds `http://*:80` behind reverse proxy (nginx/caddy) with `ForwardedHeaders`. Data Protection keys are persisted to Postgres (`DataProtectionKeys` table) so auth cookies survive restarts.
