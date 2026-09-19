using DotnetApiSample.Infrastructure;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddProductCreatedConsumer();

var host = builder.Build();

await host.RunAsync();
