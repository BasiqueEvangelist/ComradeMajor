// See https://aka.ms/new-console-template for more information

using ComradeMajor;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Telegram.Bot;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

builder.Configuration.AddJsonFile("secrets.json");

var token =  builder.Configuration["ApiKey"]!;

builder.Services.Configure<BotSettings>(builder.Configuration);
builder.Services.AddSingleton<TelegramBotClient>(x => new TelegramBotClient(token));

builder.Services.AddHostedService<ComradeMajorBot>();

builder.Build().Run();