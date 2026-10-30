using Microsoft.EntityFrameworkCore;
using RagPlatform.Api.Contracts.Health;
using RagPlatform.Api.Data;
using RagPlatform.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    var host = Environment.GetEnvironmentVariable("RAGPOSTGRES_HOST") ?? "localhost";
    var port = Environment.GetEnvironmentVariable("RAGPOSTGRES_PORT") ?? "5432";
    var database = Environment.GetEnvironmentVariable("RAGPOSTGRES_DB") ?? "ragplatform";
    var username = Environment.GetEnvironmentVariable("RAGPOSTGRES_USER") ?? "postgres";
    var password = Environment.GetEnvironmentVariable("RAGPOSTGRES_PASSWORD");

    connectionString = $"Host={host};Port={port};Database={database};Username={username};";
    if (!string.IsNullOrWhiteSpace(password))
    {
        connectionString += $"Password={password};";
    }
}

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddScoped<IHealthService, HealthService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "RAG Platform API v1");
    });
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
