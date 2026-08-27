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
Each user has a profile that can be visited at `/Users/{username}`. You can browse the items they have put up for sale and choose to contact them via email or phone. The profile shows paginated advertisements (12 per page) and owner-only controls (Edit Profile, Create, Edit/Delete own ads, admin sees Edit/Delete on all).
### 5. Real-time chat
- SignalR hub `/hubs/chat` with live Inbox and Thread
- Navbar unread badge + toast updates instantly
- Block/unblock (admins cannot be blocked), paginated Inbox (12) and Thread (50), scrollbars, responsive
### 6. Contact privacy
- Email and phone hidden by default. Owner toggles `Show email` and `Show phone` in Edit Profile.
- When enabled, logged-in users see full contact. Anonymous users see censored value (e.g. `j***n@example.com`, `•••••123`) plus a Login to reveal CTA. Admins see all.
- Chat requires login. Anonymous clicking Message Seller is redirected to Login with ReturnUrl.

## How to run (Linux / CachyOS dev, HTTP default)

1. Clone: `git clone https://github.com/nikoladevelops/Marketplace.git && cd Marketplace`
2. Restore: `dotnet restore`
3. Create `Marketplace/.env` (next to `Marketplace.csproj`):
   ```
   CONNECTION_STRING=Host=localhost;Database=marketplace;Username=postgres;Password=postgres
   # optional AI
   AI_API_URL=
   AI_API_KEY=
   ```
4. First-time setup (migrate and seed essential data):
   ```
   dotnet run --project Marketplace -- setup
   # aliases: seed:core, seed-core
   ```
   Uses `Utility/Seeding/IdentityAndCatalogSeeder.cs` to seed roles (`Seller`/`Premium`/`Admin`), users (`seller`/`premium`/`admin` with password `aaaaaaA!1`), and categories (`Furniture` ... `Sports & Outdoors`). Idempotent and safe to re-run.

5. Demo data (requires setup first):
   ```
   dotnet run --project Marketplace -- seed:demo            # 25 ads round-robin demo users
   dotnet run --project Marketplace -- seed:demo --count 50 # 1..200
   dotnet run --project Marketplace -- seed:demo --count=50 # same
   dotnet run --project Marketplace -- seed:demo --user admin --count 20  # 20 ads for specific user
   # compat: dotnet run --project Marketplace -- --seed-demo=25
   ```
   Uses `Utility/Seeding/DemoContentSeeder.cs` to create 8 `demo_*@example.com` users and sample ads. Each ad gets at least 2 images (1 main + 1-2 extras). Images are fetched title and category related via a switchable provider chain with generic fallback, so no ad ever ends up with a placeholder. Missing categories or missing target user abort with a clear message suggesting `setup` or check spelling. Per-user mode seeds only that user, otherwise round-robin demo users.

   Image providers live in `Utility/Seeding/ImageProviders/` (`LoremFlickr`, `Unsplash`, `Picsum`, `LocalFallback`) and are order-configurable via `appsettings.json` key `DemoSeeding:ImageProviders` or env var `DEMO_IMAGE_PROVIDERS` (e.g. `DEMO_IMAGE_PROVIDERS=LoremFlickr,Picsum dotnet run -- seed:demo`). The chain automatically switches on failure or rate limit (429/500) and logs which provider failed for which title/category. A per-category local fallback in `wwwroot/seed-fallback/` guarantees offline operation.

6. User and role management via CLI (flexible seeder, no em dashes, idempotent and graceful):
   ```
   # create user (email required, password defaults to aaaaaaA!1, default role Seller)
   dotnet run --project Marketplace -- user:create --username newadmin --email newadmin@example.com --role Admin
   dotnet run --project Marketplace -- user:create --username alice --email alice@example.com --password MyPass123! --role Seller,Premium

   # give or remove roles (username or email, multiple roles comma separated for create)
   dotnet run --project Marketplace -- user:give-role --user admin --role Premium
   dotnet run --project Marketplace -- user:remove-role --user premium --role Premium

   # list users
   dotnet run --project Marketplace -- user:list --search mar --take 10
   dotnet run --project Marketplace -- user:list
   ```
   Implemented in `Utility/Seeding/UserSeeder.cs` (validation for username min 3, valid email, existing user/role checks, idempotent). Errors are printed to stderr with suggestions, never throw. Valid roles: `Seller`, `Premium`, `Admin`.

7. Dev database reset (DEV only):
   ```
   dotnet run --project Marketplace -- db:reset --force          # wipe DB and uploads
   dotnet run --project Marketplace -- db:reset --force --reseed # wipe and re-seed essential data
   # aliases: reset:dev, db-reset, reset
   ```
   Guarded: refuses to run when `ASPNETCORE_ENVIRONMENT != Development` and requires `--force`. Clears `Advertisements`, `AdvertisementImages`, `ChatMessages`, `UserBlocks`, `AspNet*`, `Categories`, `DataProtectionKeys` and `wwwroot/uploads/{advertisements,profiles}`. Also resets Postgres sequences. Implemented in `Utility/Seeding/DevDatabaseCleaner.cs`.

8. Start app:
   ```
   dotnet run --project Marketplace
   # opens http://localhost:5256 (dev default, no cert needed)
   ```

Order matters: **setup -> seed:demo -> run**. Help (no DB needed): `dotnet run --project Marketplace -- help`.

Seeding implementation lives in `Marketplace/Utility/Seeding/` with `IdentityAndCatalogSeeder.cs`, `DemoContentSeeder.cs`, `UserSeeder.cs`, `DevDatabaseCleaner.cs`, `SeedingCommands.cs` (CLI dispatcher) and `ImageProviders/` (switchable image sources).

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
* If `Connection lost - reconnecting` appears, check browser Console ` [chat] start` and Network `POST /hubs/chat/negotiate` (should be `200` when logged in, `401` anon is normal).
* Only one `dotnet run` at a time. If you see `Failed to bind to address ... already in use`, kill stale holders: `lsof -ti :5256 | xargs -r kill -9`.

## Production

`appsettings.Production.json` binds `http://*:80` behind reverse proxy (nginx/caddy) with `ForwardedHeaders`. Data Protection keys are persisted to Postgres (`DataProtectionKeys` table) so auth cookies survive restarts.
