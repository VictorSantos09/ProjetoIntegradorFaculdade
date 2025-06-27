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
    public async Task<Valores> GetNextAveragesAsync(DateTime inicio, DateTime fim, int horasGrupo = 1)
    {
        var pipeline = _collection.Aggregate()
            .Match(v => v.LeituraEm >= inicio && v.LeituraEm <= fim)
            .Group(new BsonDocument
            {
            {
                "_id", new BsonDocument
                {
                    { "ano", new BsonDocument("$year", "$LeituraEm") },
                    { "mes", new BsonDocument("$month", "$LeituraEm") },
                    { "dia", new BsonDocument("$dayOfMonth", "$LeituraEm") },
                    { "blocoTempo", new BsonDocument(
                        "$subtract", new BsonArray
                        {
                            new BsonDocument("$hour", "$LeituraEm"),
                            new BsonDocument("$mod", new BsonArray
                            {
                                new BsonDocument("$hour", "$LeituraEm"),
                                horasGrupo
                            })
                        })
                    }
                }
            },
            { "TemperaturaMedia", new BsonDocument("$avg", "$temperatura") },
            { "UmidadeMedia", new BsonDocument("$avg", "$umidade") },
            { "FumacaMedia", new BsonDocument("$avg", "$ppm_MQ2") },
            { "CO2Media", new BsonDocument("$avg", new BsonDocument("$cond", new BsonArray { "$coDetectado", 1, 0 })) },
            { "ChamaMedia", new BsonDocument("$avg", new BsonDocument("$cond", new BsonArray { "$chamaDetectada", 1, 0 })) },
            { "LeituraMaisRecente", new BsonDocument("$max", "$LeituraEm") }
            })
            .Sort(Builders<BsonDocument>.Sort.Ascending("_id"));

        var documentos = await pipeline.ToListAsync();

        var resultados = documentos.Select(d =>
        {
            return new Valores
            {
                LeituraEm = d["LeituraMaisRecente"].ToUniversalTime(),
                temperatura = ((float)d.GetValue("TemperaturaMedia", 0).ToDouble()),
                umidade = d.GetValue("UmidadeMedia", 0).ToDouble(),
                ppm_MQ2 = d.GetValue("FumacaMedia", 0).ToDouble(),
                coDetectado = d.GetValue("CO2Media", 0).ToDouble() >= 0.5,
                chamaDetectada = d.GetValue("ChamaMedia", 0).ToDouble() >= 0.5
            };
        });

        return new Valores
        {
            temperatura = resultados.Average(v => v.temperatura),
            umidade = resultados.Average(v => v.umidade),
            ppm_MQ2 = resultados.Average(v => v.ppm_MQ2),
            coDetectado = resultados.Average(v => v.coDetectado ? 1 : 0) >= 0.5,
            chamaDetectada = resultados.Average(v => v.chamaDetectada ? 1 : 0) >= 0.5,
            LeituraEm = resultados.Max(v => v.LeituraEm)
        };
    }

    public async Task<Valores> GetById(string id)
    {
        return await _collection.Find(x => x.Id == id).SingleOrDefaultAsync();
    }
}