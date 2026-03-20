using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Http;
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
        var httpClientHandler = new HttpClientHandler();

        if (settings.Value.Proxy is ProxySettings ps && ps.UseProxy && ps.ProxyUrl is string url)
        {
            httpClientHandler.Proxy = new WebProxy(url);
            httpClientHandler.UseProxy = true;
        };

        var httpClient = new HttpClient(httpClientHandler);
        
        _botClient = new TelegramBotClient(settings.Value.Token, httpClient);
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
        if (update.Type != UpdateType.Message || update.Message is null) return;
        var msg = update.Message;
        
        if (msg.From is null) return;

        bool isPersonalChat = msg.Chat.Id == msg.From.Id;

        if (msg.From.Id != _settings.AdminId)
        {
            _logger.LogInformation("Received message from {UserId}, who isn't an admin, ignoring", msg.From.Id);
            return;
        }

        try
        {
            string timestamp = msg.Date.ToString("yyyy_MM_dd_HH_mm_ss");
            string prefix = Path.Combine(_settings.TargetFolder, timestamp);

            var filesWritten = await SaveMessage(msg, prefix, cancellationToken);
            if (filesWritten)
                await ReactWith(msg, "\ud83d\udc4d", cancellationToken);
        }
        catch (Exception e)
        {
            _logger.LogError("Failed to process message: {Exception}", e);
            if (isPersonalChat)
            {
                await _botClient.SendMessage(
                    msg.Chat,
                    "Никак нет, товарищ пользователь.\n" + e.Message,
                    replyParameters: new ReplyParameters { MessageId = msg.MessageId });
            }
            await ReactWith(msg, "\ud83e\udd2f", cancellationToken);
        }
    }

    async Task<bool> SaveMessage(Message msg, string prefix, CancellationToken token)
    {
        if (msg.Type == MessageType.Text)
        {
            string name = GetFileName(prefix, ".txt");
            await File.WriteAllTextAsync(name, msg.Text, cancellationToken: token);
        }
        else if (msg.Type == MessageType.Document)
        {
            await Download(msg.Document!.FileId, prefix, msg.Document.FileName, token);
        }
        else if (ExtractFileId(msg) is string fileId)
        {
            await Download(fileId, prefix, null, token);
        }
        else
        {
            // Can't process, save nothing.
            return false;
        }

        // If file was sent with caption, save it as well
        if (msg.Caption != null)
        {
            await File.WriteAllTextAsync(prefix + ".caption.txt", msg.Caption, cancellationToken: token);
        }

        return true;
    }

    string? ExtractFileId(Message msg) => msg.Type switch
    {
        MessageType.Document => msg.Document!.FileId,
        MessageType.Photo => msg.Photo!.Last().FileId,
        MessageType.Video => msg.Video!.FileId,
        MessageType.Animation => msg.Animation!.FileId,
        MessageType.Audio => msg.Audio!.FileId,
        MessageType.Sticker => msg.Sticker!.FileId,
        MessageType.VideoNote => msg.VideoNote!.FileId,
        MessageType.Voice => msg.Voice!.FileId,
        _ => null
    };

    private async Task Download(string fileId, string prefix, string? suffix, CancellationToken token)
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