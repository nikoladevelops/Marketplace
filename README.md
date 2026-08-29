# Marketplace

A simple web marketplace where people can post items they want to sell and others can browse them, contact sellers, and chat in real time. You create an account, post ads with photos and location, and browse what others have listed. Admins can manage users, premium users get more listings, and guests can still browse.

## What this app does

The goal is a small but complete marketplace with real features: listing creation, image upload, category and price filtering, user profiles, private contact info, block and report, real time chat, and an admin panel. Everything works without a separate API project. The app is MVC with Razor views, Entity Framework Core, and SignalR.

## Tech stack

* .NET 10.0, C# 14, Nullable enabled, Implicit Usings
* ASP.NET Core MVC + Razor Views
* Entity Framework Core 10.0.11 with Npgsql PostgreSQL 10.0.3
* ASP.NET Core Identity 10.0.11 for users and roles
* ASP.NET Core DataProtection persisted to Postgres
* SignalR for real time chat
* Bootstrap 5 for UI
* Leaflet 1.9.4 for maps
* DotNetEnv 3.2.0 for .env support

NuGet packages: `Microsoft.EntityFrameworkCore`, `Microsoft.EntityFrameworkCore.Design`, `Npgsql.EntityFrameworkCore.PostgreSQL`, `Microsoft.AspNetCore.Identity.EntityFrameworkCore`, `Microsoft.AspNetCore.DataProtection.EntityFrameworkCore`, `DotNetEnv`.

Database: PostgreSQL with `pg_trgm` extension for fuzzy title, description and location search. Auto migration on startup.

## Prerequisites

* .NET SDK 10 installed (`dotnet --version` should show 10.x)
* PostgreSQL running locally
* Git

Works the same on Linux, macOS and Windows. No OS specific steps except the database connection.

## Setup step by step

1. Clone the repo

```
git clone https://github.com/nikoladevelops/Marketplace.git
cd Marketplace
```

2. Restore packages

```
dotnet restore
```

3. Create your environment file. Copy the example:

```
cp Marketplace/.env.example Marketplace/.env
```

Edit `Marketplace/.env` and set your values. At minimum you need `CONNECTION_STRING`. The AI keys are optional if you do not use auto fill.

Example `Marketplace/.env`:

```
CONNECTION_STRING=Host=localhost;Port=5432;Database=marketplace;Username=marketplace_user;Password=CHANGEME
AI_API_URL=http://localhost:1234/v1
AI_API_KEY=lm-studio
AI_MODEL_NAME=Qwen3-VL-2B-Instruct-GGUF
```

For OpenRouter or another OpenAI compatible provider, just change the URL and key:

```
AI_API_URL=https://openrouter.ai/api/v1
AI_API_KEY=sk-or-v1-your-key
AI_MODEL_NAME=google/gemini-2.0-flash-001
```

To switch models locally with LM Studio, just change `AI_MODEL_NAME` to the exact name shown in LM Studio and restart the app.

4. First time setup. This creates the database, runs migrations and seeds roles, users and categories. It is safe to run again.

```
dotnet run --project Marketplace -- setup
```

This seeds roles `Seller`, `Premium`, `Admin`, users `seller` / `premium` / `admin` with password `aaaaaaA!1`, and categories like Furniture, Electronics, Vehicles, Clothing, Books, Sports and others. Code lives in `Utility/Seeding/IdentityAndCatalogSeeder.cs`.

5. Optional demo data. Needs setup first.

```
dotnet run --project Marketplace -- seed:demo
dotnet run --project Marketplace -- seed:demo --count 50
dotnet run --project Marketplace -- seed:demo --user admin --count 20
```

This creates `demo_*@example.com` users and sample ads. Each ad gets at least one main image and one to two extra images. Images are fetched title and category related via a provider chain that tries LoremFlickr, then Unsplash, then Picsum, then a local fallback in `wwwroot/seed-fallback`. The chain switches automatically on failure or rate limit. Order can be changed in `appsettings.json` `DemoSeeding:ImageProviders` or env `DEMO_IMAGE_PROVIDERS`.

6. Run the app

```
dotnet run --project Marketplace
```

Open `http://localhost:5256` in your browser. The app listens on HTTP in development so you do not need a certificate.

If you prefer manual migrations:

```
dotnet ef database update --project Marketplace
```

The app also runs `MigrateAsync()` on startup, so manual update is optional.

7. Test users after setup

* seller / aaaaaaA!1 (Seller)
* premium / aaaaaaA!1 (Premium)
* admin / aaaaaaA!1 (Admin)

## Environment variables

* `CONNECTION_STRING` - required. Postgres connection string. Example `Host=localhost;Port=5432;Database=marketplace;Username=marketplace_user;Password=...`
* `AI_API_URL` - optional. Base URL for AI. Default `http://localhost:1234/v1` for LM Studio. Use `https://openrouter.ai/api/v1` for OpenRouter.
* `AI_API_KEY` - optional. API key. Default `lm-studio` for local.
* `AI_MODEL_NAME` - optional. Model name as shown by provider. Default `Qwen3-VL-2B-Instruct-GGUF`. Just change the name in .env to switch models. The service trims quotes and spaces, so both `Qwen3-VL-2B-Instruct-GGUF` and `"Qwen3-VL-2B-Instruct-GGUF"` work.
* `ASPNETCORE_ENVIRONMENT` - `Development` or `Production`. Dev enables `db:reset` and detailed SignalR errors.
* `DEMO_IMAGE_PROVIDERS` - optional comma list to override image provider order for demo seeding.

See `Marketplace/.env.example` for a template.

## Maps

The app uses Leaflet for picking a meeting point when you create or edit an ad, and for showing it on the ad details page.

* Library: Leaflet 1.9.4 with `leaflet.css` and `locationPicker.js`
* Tiles: `https://tile.openstreetmap.org/{z}/{x}/{y}.png` by OpenStreetMap. Free, no API key, requires attribution which is shown on the map.
* Geocoding: `https://nominatim.openstreetmap.org/reverse` for turning lat and lng into a readable address. Free, no key, rate limited to about one request per second. Used only when you click or drag the pin or use Detect my location.
* The map always uses the normal light tiles, even in dark mode. This avoids any dark tile provider that would need a key and keeps the map readable for everyone.
* Leaflet controls (zoom buttons, attribution) are themed with CSS variables so they match the site dark and light themes.

No map API key is needed. No billing. The only limit is the public Nominatim usage policy. If you need commercial or high volume geocoding, you can replace the URL in `wwwroot/js/locationPicker.js` with your own provider.

## AI image service

The ad form has a button Auto Fill Listing Details with AI. You upload a main image, click the button, and the app sends the image to an AI vision model that returns title, description and category.

* Service: `Services/AiImageService.cs` with interface `Services/IAiImageService.cs`
* Provider: any OpenAI compatible chat completions endpoint. The service builds a request to `POST {AI_API_URL}/chat/completions` with `model: AI_MODEL_NAME`, a system prompt with the list of categories from the database, and the image as `data:image/jpeg;base64,...`.
* Works with LM Studio locally (`http://localhost:1234/v1`, model like `Qwen3-VL-2B-Instruct-GGUF`) and with hosted providers like OpenRouter (`https://openrouter.ai/api/v1`). Just change `.env` and restart.
* Timeout: 60 seconds via HttpClient in `Program.cs`.
* If the service is offline or fails, you get a modal dialog, not a browser popup. The modal explains to check that the service is running and that `AI_API_URL` and `AI_MODEL_NAME` match the loaded model. The controller `AdvertisementController.GenerateListingAI` returns `{ success: false, message: "..." }` in that case.

## Roles and permissions

* Guest (not logged in): browse Home, view ad details, view user profiles, see censored contact info (like `j***n@example.com` or `....123`) with a login prompt, search and filter, see paginated lists. Cannot chat, create, edit, delete, block or report.
* Seller: all guest plus create up to 20 ads, edit and delete own ads, update own profile and avatar, toggle Show email and Show phone, chat with sellers about ads, block other users (except admins), report chats, share phone with one click if phone is valid (8 to 15 digits).
* Premium: same as Seller but up to 40 ads. Can be given or removed by admin.
* Admin: same as Premium plus admin panel at `/Admin/AdminPanel` to search users by name or email, filter by role, reported, blocked, paginate 20 per page, give or remove Premium and Admin roles (cannot change own Admin role), ban and unban with reason (banned users are signed out and cannot log in, their ads are hidden from public), delete accounts permanently (cannot delete self or other admins), view reports per user with counts of available and resolved, open chat logs for reported threads read only, dismiss or ban via report. Admins bypass blocks for sending messages and reading reported chats, and cannot be blocked or reported.

## Admin panel

At `/Admin/AdminPanel` you can search, filter, and manage every account. The user list shows badges for Banned, Admin, Premium, Seller, Reported count and Blocked by count. Selecting a user opens a manage card where you can grant Premium, make admin, ban with optional reason (shown in a confirmation modal with preview), unban, delete (also with confirmation), and load reports. The reports section fetches `GET /Admin/Reports?userId=` and for each report shows reason, description, ad link, and a Show chat button that loads `GET /Admin/ChatLog?reportId=` inline. That chat log is read only and shows the exact messages between reporter and reported user for that ad, even if one side blocked the other. Only admins can call that endpoint and only if the report exists.

## Recently browsed and Recommended

* Recently browsed uses `localStorage` key `mb.recentAds` and `wwwroot/js/recentlyViewed.js` plus `horizontalScroller.js`. Every time you open an ad detail page, the ad payload is saved via `data-recent-ad` attribute, deduped, limited to 50, filtered to exclude your own ads, and synced across tabs via storage events. The strip renders as a horizontal scroller with thumbnails.
* Recommended uses `HomeController.Recommendations` POST JSON `{ viewedIds, limit }` with `wwwroot/js/recommendedAds.js`. It scores candidates by category affinity (top 2 categories from your history weighted by recency `0.85^i`), price band `median +/- 35 percent`, and location match, with weights `cat 3.0, price 1.0, loc 0.4`, diversifies round robin. It hides viewed ids and own ads and banned users ads. The scroller shows up to 15.
* Both use AJAX and abort previous requests to stay fast.

## Ajax

The app uses `fetch` with `AbortController` for live updates without full page reload.

* Home: `Views/Home/Index.cshtml` `fetchGrid` calls `GET /Home/Search?searchTerm=&category=&location=&minimumPrice=&maximumPrice=&filter=&pageNumber=` with `X-Requested-With: XMLHttpRequest` and swaps `#adGridContainer` with the returned `_AdGrid` partial. Debounced 400 ms on typing, instant on selects. Pagination is also AJAX and keeps URL via `history.pushState`.
* Admin: `Views/Admin/AdminPanel.cshtml` `fetchUserList` for user search and `loadReports` plus `attachChatLogHandlers` for chat logs.
* Chat unread badge: `wwwroot/js/navbarMessages.js` polls `GET /Chat/UnreadCount` and also receives pushes.
* All `fetch` handlers catch `AbortError` separately and log other errors to console.

Ajax is used to keep the UI responsive, reduce server load by not re-rendering the whole layout, and to allow cancellation when you type fast or switch pages quickly.

## SignalR real time chat

SignalR is at `/hubs/chat` via `Hubs/ChatHub.cs`.

* Methods the client can call: `GetMessagesSince(adId, with, afterMessageId)`, `JoinThread(adId, with)`, `MarkThreadRead(adId, with)`, `SendMessage(adId, with, message)` (1 to 1000 chars, checks ad exists, not self, block bypass only for admins).
* Events the server sends: `ReceiveMessage` with `{ id, body, sentAt, senderName, advertisementId }` to both user groups, `MessagesRead` with `{ byUserName }` to both groups for the double check marks.
* Groups are `user-{userId}` added on connect. Config in `Program.cs` has `KeepAlive 15s`, `ClientTimeout 30s`, `Max message 32KB`, `EnableDetailedErrors` only in development.
* Client files `wwwroot/js/chat.js` (thread), `chatInbox.js` (inbox live move to top, unread badge), `navbarMessages.js` (global badge) all use `WebSockets | LongPolling`, auto reconnect with exponential backoff 2s to 30s, `syncMissing` poll every 20 seconds as safety net, and `visibilitychange` to resync when you return to the tab. Messages are inserted with `textContent`, not `innerHTML`, so they are safe from XSS.

## Database schema

All tables are in `Models/ApplicationDbContext.cs`. Postgres is the provider.

* `AspNetUsers` (Identity `ApplicationUser`): `Id`, `UserName`, `Email`, `PasswordHash`, `ProfilePicturePath`, `Description`, `ShowEmail`, `ShowPhone`, `PhoneNumber`, `Status` (Active or Banned), `BanReason`, `BannedAtUtc`, `BannedByUserId` plus nav `BannedByUser`. Index on `Status`.
* `Advertisements`: `Id`, `Title`, `Description`, `Price` decimal 18,2, `Location`, `Latitude`, `Longitude`, `DateCreatedOn`, `ImagePath`, `UserId` FK to `AspNetUsers` Cascade, `CategoryId` FK to `Categories`. Indexes GIN trigram on `Title`, `Description`, `Location`, plus `Price`, `DateCreatedOn`, `CategoryId`, composite `CategoryId,Price`.
* `AdvertisementImages`: `Id`, `AdvertisementId` FK Cascade, `ImagePath`. One to many from ad, holds extra 1 to 3 images.
* `Categories`: `Id`, `Name`. Seeded.
* `ChatMessages`: `Id`, `Body` 1 to 1000, `SentAt`, `IsReadByReceiver`, `SenderId` FK Cascade, `ReceiverId` FK Cascade, `AdvertisementId` FK Cascade. Indexes on `ReceiverId,IsReadByReceiver`, `SenderId`, `AdvertisementId`.
* `ChatReports`: `Id`, `ReporterId` FK Restrict, `ReportedUserId` FK Restrict, `AdvertisementId` FK Restrict, `ThreadKey` string `adId:userA:userB` sorted, `Reason` enum Spam, Harassment, Scam, InappropriateContent, Other, `Description` 20 to 500, `Status` Pending or Resolved, `CreatedAtUtc`, `ReviewedByAdminId` FK SetNull, `ReviewedAtUtc`, `ActionTaken` Dismissed or Banned. Unique index `ReporterId,ThreadKey` for one report per thread, indexes on `ReportedUserId,Status` and `Status`.
* `UserBlocks`: `Id`, `BlockerId` FK Cascade, `BlockedId` FK Cascade, unique `BlockerId,BlockedId`.
* `UserBanHistories`: `Id`, `UserId` FK Restrict, `AdminUserId` FK Restrict, `Action` ban or unban, `Reason`, `PerformedAtUtc`. Indexes on `UserId,PerformedAtUtc` and `AdminUserId`.
* `DataProtectionKeys`: `Id`, `FriendlyName`, `Xml`. Persisted keys so cookies survive restart, mapped in `Program.cs` with `PersistKeysToDbContext` and `ForwardedHeaders` for proxy.

Relationships: a user has many ads, a user has many sent and received messages, an ad has many messages and reports, a user can block many users, a report belongs to one ad and two users plus optional reviewer admin.

## Project structure

* `Controllers/` - `Home`, `Advertisement`, `Account`, `Chat`, `Admin`
* `Models/` - entities and `ApplicationDbContext`
* `ViewModels/` - `HomeViewModel`, `Create/Edit/ShowAdvertisement`, `ProfileViewModel`, `ChatThread`, `AdminPanel`, etc.
* `Services/` - `AdvertisementService`, `AccountService`, `ChatService`, `AdvertisementFilterService`, `UserAdministrationService`, `AiImageService`
* `Utility/Seeding/` - `IdentityAndCatalogSeeder`, `DemoContentSeeder`, `UserSeeder`, `DevDatabaseCleaner`, `SeedingCommands`, `ImageProviders`
* `Hubs/ChatHub.cs` - SignalR hub
* `Middleware/BannedUserMiddleware.cs` - signs out banned users and deleted users
* `Views/` - Razor views, `Shared/_Layout`, partials like `_AdGrid`, `_ChatLog`, `_ReportList`, `_LocationMapModal`
* `wwwroot/js/` - `chat.js`, `chatInbox.js`, `navbarMessages.js`, `pagination.js`, `horizontalScroller.js`, `recentlyViewed.js`, `recommendedAds.js`, `locationPicker.js`, `imageManipulation.js`, `site.js`
* `wwwroot/css/site.css` - theme variables and component styles

## Security notes

* Identity with hashed passwords, DataProtection keys in DB, antiforgery tokens on all POST forms (the ad create and edit forms and admin panel include tokens).
* Contact privacy via `Utility/ContactVisibilityHelper.cs` - hidden by default, only owner and admin see raw, anonymous sees censored.
* Banned middleware checks `Status` on every request via `ApplicationDbContext` and signs out banned or deleted users.
* Chat checks self block, admin bypass only for admins, admin cannot be blocked or reported.
* Admin endpoints are `[Authorize(Roles=Admin)]`.

## CLI helpers

* `dotnet run -- setup` - migrate and seed essential data
* `dotnet run -- seed:demo --count 50` - demo ads
* `dotnet run -- user:create --username x --email x@example.com --role Admin`
* `dotnet run -- user:give-role --user admin --role Premium`
* `dotnet run -- user:list --search mar`
* `dotnet run -- db:reset --force` - wipes dev DB and uploads (dev only)
* `dotnet run -- help` - shows all commands without needing DB

## Troubleshooting

* `PendingModelChangesWarning` on startup means you changed `ApplicationDbContext` without a migration. Run `dotnet ef migrations add YourName` then `dotnet ef database update`.
* `Npgsql: Connection refused` means Postgres is not running or `CONNECTION_STRING` is wrong. Check `Marketplace/.env` and `pg_isready -h localhost -p 5432`.
* Chat `Connection lost` - make sure you open `http://localhost:5256` exactly, not `https`. Check browser console for `[chat] start` and `POST /hubs/chat/negotiate` returning 200 when logged in.
* Port already in use - `lsof -ti :5256 | xargs -r kill -9` then run again.
* AI says service offline - check `Marketplace/.env` `AI_API_URL` and `AI_MODEL_NAME` match what LM Studio shows, and that LM Studio server is running.

## Production

`appsettings.Production.json` binds `http://*:80` behind a reverse proxy. `ForwardedHeaders` is configured and DataProtection keys stay in Postgres so restarts keep logins valid.

