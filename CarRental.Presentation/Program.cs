using CarRental.Application.Extensions;
using CarRental.Application.Services;
using CarRental.Infrastructure.Data;
using CarRental.Infrastructure.Extensions;
using Microsoft.AspNetCore.Identity;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.MigrateDatabase(builder.Configuration); // database migration
builder.Services.AddApplication(builder.Configuration);

builder.Services.AddLogging(); // add ILogger 

// Log in configuration
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// Identity configuration
builder.Services.AddIdentity<IdentityUser, IdentityRole>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = false;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
})
.AddEntityFrameworkStores<ApplicationDbContext>() // Register a used database
.AddDefaultTokenProviders(); // Add default token providers to generate tokes for editing data

// Cookies configuration
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.HttpOnly = true;
    options.LoginPath = "/Identity/Account/Login";  // path to log in page
    options.AccessDeniedPath = "/Identity/Account/AccessDenied"; // path in case of lack of permissions
    options.SlidingExpiration = true; 
});

builder.Services.AddControllersWithViews(); 
builder.Services.AddRazorPages();  

builder.Services.AddTransient<EmailService>(); // refactor TODO

var app = builder.Build();

// Middleware configuration
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage(); 
}
else
{
    app.UseExceptionHandler("/Home/Error"); // Default page in case of errors 
    app.UseHsts(); // forces to use HTTPS
}

app.UseHttpsRedirection(); // redirect to HTTPS
app.UseStaticFiles(); // allow acces to static files

app.UseRouting(); 

app.UseAuthentication(); 
app.UseAuthorization(); 

app.MapRazorPages(); 

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run(); 