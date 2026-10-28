// Local stack: PostgreSQL (platform + tenant databases), Redis, RabbitMQ and the API.
// Secrets are generated once and kept in user secrets; see them in the dashboard's Parameters tab.
var builder = DistributedApplication.CreateBuilder(args);

var platformApiKey = builder.AddParameter("platform-api-key", new GenerateParameterDefault { MinLength = 32, Special = false }, secret: true, persist: true);
var demoOwnerPassword = builder.AddParameter("demo-owner-password", new GenerateParameterDefault { MinLength = 16, Special = false }, secret: true, persist: true);

var postgres = builder.AddPostgres("postgres")
    .WithDataVolume("clarioshop-postgres")
    .WithLifetime(ContainerLifetime.Persistent);
var platformDb = postgres.AddDatabase("platformdb", "clarioshop_platform");

var redis = builder.AddRedis("redis");
var rabbitMq = builder.AddRabbitMQ("rabbitmq").WithManagementPlugin();

builder.AddProject<Projects.ClarioShop_Api>("api")
    .WithReference(postgres)
    .WithReference(platformDb)
    .WithReference(redis)
    .WithReference(rabbitMq)
    .WaitFor(platformDb)
    .WaitFor(redis)
    .WaitFor(rabbitMq)
    .WithEnvironment("Platform__ApiKey", platformApiKey)
    .WithEnvironment("Platform__DemoTenant__OwnerPassword", demoOwnerPassword)
    .WithExternalHttpEndpoints();

builder.Build().Run();
