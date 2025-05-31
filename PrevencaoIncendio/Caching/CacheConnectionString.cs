namespace PrevencaoIncendio.Caching;

public class CacheConnectionString
{
    public string EndPoints { get; set; }
    public string Password { get; set; }
    public bool Ssl { get; set; }
    public string SslHost { get; set; }
    public string User { get; set; }
    public bool AbortOnConnectFail { get; set; }
}

