using ClarioShop.Api.Setup;
using ClarioShop.Catalog;
using ClarioShop.Customers;
using ClarioShop.Identity;
using ClarioShop.Infrastructure.Endpoints;
using ClarioShop.Infrastructure.Messaging;
using ClarioShop.Infrastructure.Security;
using ClarioShop.Infrastructure.Tenancy;
using ClarioShop.Inventory;
using ClarioShop.Ordering;
using ClarioShop.Platform;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

var useRabbitMq = !string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("rabbitmq"));
if (useRabbitMq)
{
    builder.AddRabbitMQClient("rabbitmq");
}

if (!string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("redis")))
{
    builder.AddRedisDistributedCache("redis");
}

builder.Services.AddHybridCache();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddOpenApi(options => options.AddDocumentTransformer<BearerSecuritySchemeTransformer>());

builder.Services.AddClarioTenancy(builder.Configuration);
builder.Services.AddClarioMessaging(builder.Configuration, useRabbitMq);
builder.Services.AddClarioAuthentication(builder.Configuration, builder.Environment);
builder.Services.AddPermissionAuthorization();
builder.Services.AddClarioRateLimiting(builder.Configuration);
builder.Services.AddClarioCors(builder.Configuration);

builder.Services
    .AddPlatformModule(builder.Configuration)
    .AddIdentityModule()
    .AddCatalogModule()
    .AddInventoryModule(builder.Configuration)
    .AddCustomersModule()
    .AddOrderingModule();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseCors();
app.UseTenantResolution();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapDefaultEndpoints();
app.MapEndpoints();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
    app.MapScalarApiReference().AllowAnonymous();
}

if (app.Configuration.GetValue<bool>("Platform:MigrateOnStartup"))
{
    await app.Services.MigrateClarioDatabasesAsync();
}

await app.RunAsync();

/// <summary>Entry point, public for WebApplicationFactory in integration tests.</summary>
public partial class Program;
