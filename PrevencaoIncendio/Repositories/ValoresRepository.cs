using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using PrevencaoIncendio.Data;
using PrevencaoIncendio.Models;

namespace PrevencaoIncendio.Repositories;

public class ValoresRepository : Repository, IValoresRepository
{
    private readonly IMongoCollection<Valores> _collection;
    public ValoresRepository(IMongoClient client, IMongoDatabase mongoDatabase) : base(client, mongoDatabase)
    {
        _collection = mongoDatabase.GetCollection<Valores>(DbManager.CollectionName);
    }

    public async Task InsertOne(Valores valores)
    {
        valores.LeituraEm = DateTime.Now;
        await _collection.InsertOneAsync(valores);
    }
    public async Task<IEnumerable<Valores>> GetAll()
    {
        return await _collection.Find(_ => true).ToListAsync();
    }
    public async Task<IEnumerable<Valores>> GetLastMinutesGroupedAsync(int minutos = 30)
    {
        var dataLimite = DateTime.UtcNow.AddMinutes(-minutos);

        var pipeline = _collection.Aggregate()
            .Match(v => v.LeituraEm >= dataLimite)
            .Group(new BsonDocument
            {
        {
            "_id", new BsonDocument
            {
                { "ano", new BsonDocument("$year", "$LeituraEm") },
                { "mes", new BsonDocument("$month", "$LeituraEm") },
                { "dia", new BsonDocument("$dayOfMonth", "$LeituraEm") },
                { "hora", new BsonDocument("$hour", "$LeituraEm") },
                { "minuto", new BsonDocument("$minute", "$LeituraEm") }
            }
        },
        { "maisRecente", new BsonDocument("$max", "$LeituraEm") },
        { "doc", new BsonDocument("$first", "$$ROOT") }
            })
            .ReplaceRoot<BsonDocument>("$doc") // se quiser resultado tipado, será convertido abaixo
            .Sort(Builders<BsonDocument>.Sort.Ascending("LeituraEm"));

        var documentos = await pipeline.ToListAsync();

        return documentos.Select(d => BsonSerializer.Deserialize<Valores>(d)).ToList();
    }
    public async Task<Valores> GetById(string id)
    {
        return await _collection.Find(x => x.Id == id).SingleOrDefaultAsync();
    }
}