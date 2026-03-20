using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using ComradeMajor;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

builder.Configuration.AddJsonFile("secrets.json");

builder.Services.Configure<BotSettings>(builder.Configuration);

builder.Services.AddHostedService<ComradeMajorBot>();

builder.Build().Run();