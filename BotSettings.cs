namespace ComradeMajor;

public class BotSettings
{
    public long AdminId { get; set; }
    public string TargetFolder { get; set; }
    public string Token { get; set; }
    public ProxySettings? Proxy { get; set; }
}

public class ProxySettings
{
    public bool UseProxy { get; set; } = true;
    public string? ProxyUrl { get; set; }
}