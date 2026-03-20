using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace ComradeMajor;

public class ComradeMajorBot : BackgroundService
{
    private readonly TelegramBotClient _botClient;
    private readonly ILogger<ComradeMajorBot> _logger;
    private readonly BotSettings _settings;

    public ComradeMajorBot(ILogger<ComradeMajorBot> logger, IOptions<BotSettings> settings)
    {
        _botClient = new TelegramBotClient(settings.Value.Token);
        _logger = logger;
        _settings = settings.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var me = await _botClient.GetMe(stoppingToken);
        _logger.LogInformation("Logged in as {Me}", me);

        await _botClient.ReceiveAsync(HandleUpdateAsync, HandleErrorAsync, cancellationToken: stoppingToken);
    }

    
    Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        _logger.LogError("Telegram bot client reported an error: {}", exception);
        return Task.CompletedTask;
    }

    async Task HandleUpdateAsync(ITelegramBotClient sender, Update update, CancellationToken cancellationToken)
    {
        if (update.Type != UpdateType.Message) return;
        if (update.Message!.From == null) return;
        if (update.Message.From.Id != _settings.AdminId)
        {
            _logger.LogInformation("Received message from {UserId}, who isn't an admin, ignoring", update.Message.From.Id);
            return;
        }

        try
        {
            string timestamp = update.Message.Date.ToString("yyyy_MM_dd_HH_mm_ss");
            string prefix = Path.Combine(_settings.TargetFolder, timestamp);

            if (update.Message.Type == MessageType.Text)
            {
                string name = GetFileName(prefix, ".txt");
                await File.WriteAllTextAsync(name, update.Message.Text, cancellationToken: cancellationToken);
            }
            else if (update.Message.Type == MessageType.Document)
            {
                string target = prefix + update.Message.Document!.FileName;

                await Save(update.Message.Document.FileId, prefix, update.Message.Document.FileName, cancellationToken);
            }
            else if (update.Message.Type == MessageType.Photo)
            {
                var photo = update.Message.Photo![^1];

                await Save(photo.FileId, prefix, null, cancellationToken);                    
            }
            else if (update.Message.Type == MessageType.Video)
            {
                await Save(update.Message.Video!.FileId, prefix, null, cancellationToken);
            }
            else if (update.Message.Type == MessageType.Animation)
            {
                await Save(update.Message.Animation!.FileId, prefix, null, cancellationToken);
            }
            else if (update.Message.Type == MessageType.Audio)
            {
                await Save(update.Message.Audio!.FileId, prefix, null, cancellationToken);
            }
            else if (update.Message.Type == MessageType.Sticker)
            {
                await Save(update.Message.Sticker!.FileId, prefix, null, cancellationToken);
            }
            else if (update.Message.Type == MessageType.VideoNote)
            {
                await Save(update.Message.VideoNote!.FileId, prefix, null, cancellationToken);
            }
            else if (update.Message.Type == MessageType.Voice)
            {
                await Save(update.Message.Voice!.FileId, prefix, null, cancellationToken);
            }
            else
            {
                return;
            }

            if (update.Message.Caption != null)
            {
                await File.WriteAllTextAsync(prefix + ".txt", update.Message.Caption, cancellationToken: cancellationToken);
            }

            await ReactWith(update.Message, "\ud83d\udc4d", cancellationToken);
        }
        catch (Exception e)
        {
            _logger.LogError("Failed to process message: {Exception}", e);

            await ReactWith(update.Message, "\ud83e\udd2f", cancellationToken);
        }
    }

    private async Task Save(string fileId, string prefix, string? suffix, CancellationToken token)
    {
        var file = await _botClient.GetFile(fileId, cancellationToken: token);

        string target = GetFileName(prefix, suffix ?? Path.GetExtension(file.FilePath)!);
                
        await using var stream = File.OpenWrite(target);
        await _botClient.DownloadFile(file, stream, cancellationToken: token);
    }

    private string GetFileName(string prefix, string suffix)
    {
        if (!File.Exists(prefix + suffix)) return prefix + suffix;
        
        int i = 1;

        while (File.Exists(prefix + "_" + i + suffix)) i += 1;

        return prefix + "_" + i + suffix;
    }

    private async Task ReactWith(Message msg, string emoji, CancellationToken cancellationToken)
    {
        await _botClient.SetMessageReaction(msg.Chat, msg.Id, [
            new ReactionTypeEmoji()
            {
                Emoji = emoji
            }
        ], cancellationToken: cancellationToken);
    }
}