using Marketplace.Models;
using Marketplace.Utility;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Read the .env file in the project directory, automatically adds all those key value pairs as environment variables that can be accessed in runtime (you should create that file and have that DotNetEnv)
DotNetEnv.Env.Load();

string? connection_string = Environment.GetEnvironmentVariable("CONNECTION_STRING");
if (connection_string == null)
{
    throw new Exception("You haven't configured the connection string, check Program.cs and the .env file");
}

builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(connection_string));

// Add Identity support + tables
builder.Services.AddIdentity<ApplicationUser, IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");


// After the app is built, we seed the database with some initial data, so all this code can be removed after the first run
DataSeeder seeder = new DataSeeder(app.Services);

await seeder.SeedRoles();
await seeder.SeedUsers();
await seeder.SeedCategories();

app.Run();
