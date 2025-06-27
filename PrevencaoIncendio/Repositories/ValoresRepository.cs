using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using PrevencaoIncendio.Data;
using PrevencaoIncendio.Models;

namespace PrevencaoIncendio.Repositories;

public enum IntervaloAgrupamento
{
    Ano,
    Mes,
    Dia,
    Hora,
    Minuto
}

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
    public async Task<IEnumerable<Valores>> GetUltimosAgrupadosAsync(
     DateTime inicio,
     IntervaloAgrupamento agrupamento,
     int intervaloMinuto = 60)
    {
        var matchStage = new BsonDocument("$match", new BsonDocument("LeituraEm", new BsonDocument("$gte", inicio)));

        var addFieldsDoc = new BsonDocument();
        if (agrupamento >= IntervaloAgrupamento.Hora)
        {
            addFieldsDoc.Add("intervaloHora", new BsonDocument("$hour", "$LeituraEm"));
        }
        if (agrupamento == IntervaloAgrupamento.Minuto)
        {
            addFieldsDoc.Add("intervaloMinuto", new BsonDocument("$subtract", new BsonArray
        {
            new BsonDocument("$minute", "$LeituraEm"),
            new BsonDocument("$mod", new BsonArray {
                new BsonDocument("$minute", "$LeituraEm"),
                intervaloMinuto
            })
        }));
        }

        var idDoc = new BsonDocument
    {
        { "ano", new BsonDocument("$year", "$LeituraEm") }
    };

        if (agrupamento >= IntervaloAgrupamento.Mes)
            idDoc.Add("mes", new BsonDocument("$month", "$LeituraEm"));

        if (agrupamento >= IntervaloAgrupamento.Dia)
            idDoc.Add("dia", new BsonDocument("$dayOfMonth", "$LeituraEm"));

        if (agrupamento >= IntervaloAgrupamento.Hora)
            idDoc.Add("hora", "$intervaloHora");

        if (agrupamento == IntervaloAgrupamento.Minuto)
            idDoc.Add("minuto", "$intervaloMinuto");

        var groupDoc = new BsonDocument
                            {
                                { "_id", idDoc },
                                { "data", new BsonDocument("$max", "$LeituraEm") },
                                { "temperaturas", new BsonDocument("$push", "$temperatura") },
                                { "umidades", new BsonDocument("$push", "$umidade") },
                                { "ppms", new BsonDocument("$push", "$ppm_MQ2") },
                                { "detectadoFogo", new BsonDocument("$max", new BsonDocument("$cond", new BsonArray { "$chamaDetectada", 1, 0 })) },
                                { "detectadoCO", new BsonDocument("$max", new BsonDocument("$cond", new BsonArray { "$coDetectado", 1, 0 })) }
                            };

        var pipeline = _collection.Aggregate()
       .As<BsonDocument>()
       .AppendStage<BsonDocument>(matchStage);

        if (addFieldsDoc.ElementCount > 0)
        {
            pipeline = pipeline.AppendStage<BsonDocument>(new BsonDocument("$addFields", addFieldsDoc));
        }

        pipeline = pipeline
            .AppendStage<BsonDocument>(new BsonDocument("$group", groupDoc))
            .AppendStage<BsonDocument>(new BsonDocument("$sort", new BsonDocument("data", 1)));

        var documentos = await pipeline.ToListAsync();

        return documentos.Select(d => new Valores
        {
            LeituraEm = d["data"].ToUniversalTime(),
            temperatura = (float)Mediana(d["temperaturas"].AsBsonArray.Select(x => x.ToDouble()).ToList()),
            umidade = Mediana(d["umidades"].AsBsonArray.Select(x => x.ToDouble()).ToList()),
            ppm_MQ2 = Mediana(d["ppms"].AsBsonArray.Select(x => x.ToDouble()).ToList()),
            chamaDetectada = d.GetValue("detectadoFogo", 0).ToInt32() == 1,
            coDetectado = d.GetValue("detectadoCO", 0).ToInt32() == 1
        });
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

    public async Task<Valores> GetNextMediansAsync(DateTime inicio, DateTime fim, int horasGrupo = 1)
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
            { "temperaturas", new BsonDocument("$push", "$temperatura") },
            { "umidades", new BsonDocument("$push", "$umidade") },
            { "fumacas", new BsonDocument("$push", "$ppm_MQ2") },
            { "coDetectados", new BsonDocument("$push", "$coDetectado") },
            { "chamasDetectadas", new BsonDocument("$push", "$chamaDetectada") },
            { "LeituraMaisRecente", new BsonDocument("$max", "$LeituraEm") }
            })
            .Sort(Builders<BsonDocument>.Sort.Ascending("_id"));

        var documentos = await pipeline.ToListAsync();

        var resultados = documentos.Select(d => new Valores
        {
            LeituraEm = d["LeituraMaisRecente"].ToUniversalTime(),
            temperatura = (float)Mediana(d["temperaturas"].AsBsonArray.Select(v => v.ToDouble()).ToList()),
            umidade = Mediana(d["umidades"].AsBsonArray.Select(v => v.ToDouble()).ToList()),
            ppm_MQ2 = Mediana(d["fumacas"].AsBsonArray.Select(v => v.ToDouble()).ToList()),
            coDetectado = Mediana(d["coDetectados"].AsBsonArray.Select(v => v.ToBoolean() ? 1.0 : 0.0).ToList()) >= 0.5,
            chamaDetectada = Mediana(d["chamasDetectadas"].AsBsonArray.Select(v => v.ToBoolean() ? 1.0 : 0.0).ToList()) >= 0.5
        });

        // Resultado geral: agregação final com mediana entre os blocos
        var lista = resultados.ToList();
        return new Valores
        {
            LeituraEm = lista.Max(v => v.LeituraEm),
            temperatura = (float)Mediana(lista.Select(v => (double)v.temperatura).ToList()),
            umidade = (float)Mediana(lista.Select(v => (double)v.umidade).ToList()),
            ppm_MQ2 = (float)Mediana(lista.Select(v => (double)v.ppm_MQ2).ToList()),
            coDetectado = Mediana(lista.Select(v => v.coDetectado ? 1.0 : 0.0).ToList()) >= 0.5,
            chamaDetectada = Mediana(lista.Select(v => v.chamaDetectada ? 1.0 : 0.0).ToList()) >= 0.5
        };

    }

    private static double Mediana(List<double> valores)
    {
        if (valores == null || valores.Count == 0)
            return 0;

        var ordenados = valores.OrderBy(x => x).ToList();
        int meio = ordenados.Count / 2;

        if (ordenados.Count % 2 == 0)
            return (ordenados[meio - 1] + ordenados[meio]) / 2.0;

        return ordenados[meio];
    }

    public async Task<Valores> GetById(string id)
    {
        return await _collection.Find(x => x.Id == id).SingleOrDefaultAsync();
    }
}