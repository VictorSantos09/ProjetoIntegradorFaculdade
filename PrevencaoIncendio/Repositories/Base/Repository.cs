using MongoDB.Driver;

public abstract class Repository
{
    private protected readonly IMongoClient _client;
    private protected readonly IMongoDatabase _mongoDatabase;

    public Repository(IMongoClient client, IMongoDatabase mongoDatabase)
    {
        _client = client;
        _mongoDatabase = mongoDatabase;
    }
}