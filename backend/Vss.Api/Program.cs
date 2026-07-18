using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Web;
using Vss.Api.Auth;
using Vss.Infrastructure;
using Vss.Infrastructure.Documents;
using Vss.Infrastructure.Erp;

var builder = WebApplication.CreateBuilder(args);

// ---- Persistence (SQL Server / local SQL Express; connection string overridable) ----
var conn = builder.Configuration.GetConnectionString("Vss")
    ?? "Server=.\\SQLEXPRESS;Database=Vss;Trusted_Connection=True;TrustServerCertificate=True";
builder.Services.AddDbContext<VssDbContext>(o => o.UseSqlServer(conn));

// ---- ERP boundary (Stub | SapByDesign | BusinessCentral, per Erp:Provider) ----
builder.Services.AddErpClient(builder.Configuration);

// ---- Current-user resolution ----
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<CurrentUser>();
// Document storage seam: DB-backed for local/dev; swap for a UDP-drive implementation here.
builder.Services.AddScoped<IDocumentStore, DbDocumentStore>();

// ---- Auth: Dev handler locally, Microsoft Entra (JWT bearer) on the network ----
var authMode = builder.Configuration["Auth:Mode"] ?? "Dev";
if (authMode.Equals("Entra", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"));
}
else
{
    builder.Services.AddAuthentication(DevAuthHandler.SchemeName)
        .AddScheme<AuthenticationSchemeOptions, DevAuthHandler>(DevAuthHandler.SchemeName, _ => { });
}
builder.Services.AddAuthorization(o =>
    o.AddPolicy("Admin", p => p.RequireRole("admin")));

// ---- MVC + OpenAPI ----
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ---- CORS for the Vite frontend ----
var frontendOrigin = builder.Configuration["Frontend:Origin"] ?? "http://localhost:5173";
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .WithOrigins(frontendOrigin)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()));

var app = builder.Build();

// ---- Create + seed the dev database ----
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<VssDbContext>();
    await DbInitializer.InitializeAsync(db);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

/// <summary>Exposed so WebApplicationFactory can bootstrap the API in tests.</summary>
public partial class Program { }
