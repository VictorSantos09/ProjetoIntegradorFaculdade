namespace PrevencaoIncendio;

public static class AppSettingsProvider
{
    public static IConfiguration Configuration { get; private set; }

    public static void Initialize(IConfiguration config)
    {
        Configuration = config;
    }

    public static string GetConnectionString(string name)
    {
        return Configuration.GetConnectionString(name) ?? throw new ArgumentException($"ConnectionString '{name}' not found."); ;
    }

    public static T GetSection<T>(string sectionName) where T : class, new()
    {
        var section = Configuration.GetSection(sectionName) ?? throw new ArgumentException($"Configuration section '{sectionName}' not found.");
        var options = new T();
        section.Bind(options);
        return options;
    }

    public static T GetValue<T>(string key)
    {
        var value = Configuration[key] ?? throw new ArgumentException($"Configuration key '{key}' not found.");
        return (T)Convert.ChangeType(value, typeof(T));
    }
}
