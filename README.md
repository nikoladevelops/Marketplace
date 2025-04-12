# Marketplace
Marketplace web app where people display the items they wish to sell alongside their contact information.

Used technologies: ASP .NET Core with .NET 9, Entity Framework Core 9 using SQL Server provider and finally Bootstrap 5 for styling purposes. The project uses MVC, without a Web API.

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

## How to test the project?
1. Clone it to a folder on your pc `git clone https://github.com/nikoladevelops/Marketplace.git`
2. Download all necessary `NuGet` packages
3. Create a `.env` file inside the project and configure the connection string<br>
Example:
```
CONNECTION_STRING=Server=.\SQLEXPRESS;Database=Marketplace;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True;
```
4. Open `Package Manager Console` and run `update-database` in order for all migrations to be applied to your SQL Server database
5. Run the project

By default inside the `Program.cs` file, a `DataSeeder` class is used, to generate test users and categories, so you can edit the logic however you wish.

In order to test the functionality after running the app, here are the test user details you need for login:

Username: seller<br>
Pass: aaaaaaA!1

Username: premium<br>
Pass: aaaaaaA!1

Username: admin<br>
Pass: aaaaaaA!1


