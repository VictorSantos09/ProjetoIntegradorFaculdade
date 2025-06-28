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
                                { "detectadoCO", new BsonDocument("$max", new BsonDocument("$cond", new BsonArray { "ppm_CO_MQ7", 1, 0 })) }
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
            ppm_CO_MQ7 = d.GetValue("detectadoCO", 0).ToDouble(),
        });
    }

    public async Task<Valores> GetNextAveragesAsync(DateTime inicio, DateTime fim, int horasGrupo = 1)
    {
        var pipeline = _collection.Aggregate()
    .Match(v => v.LeituraEm >= inicio && v.LeituraEm <= fim)
    .Group(new BsonDocument
    {
        { "_id", new BsonDocument
            {
                { "ano",       new BsonDocument("$year", "$LeituraEm") },
                { "mes",       new BsonDocument("$month", "$LeituraEm") },
                { "dia",       new BsonDocument("$dayOfMonth", "$LeituraEm") },
                { "blocoTempo", new BsonDocument("$subtract", new BsonArray
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
        { "umidades",     new BsonDocument("$push", "$umidade") },
        { "fumacas",      new BsonDocument("$push", "$ppm_MQ2") },
        { "coDetectados", new BsonDocument("$push", "$ppm_CO_MQ7") },    // <<< corrige aqui
        { "chamasDetectadas", new BsonDocument("$push", "$chamaDetectada") },
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
                ppm_CO_MQ7 = d.GetValue("CO2Media", 0).ToDouble(),
                chamaDetectada = d.GetValue("ChamaMedia", 0).ToDouble() >= 0.5
            };
        });

        return new Valores
        {
            temperatura = resultados.Average(v => v.temperatura),
            umidade = resultados.Average(v => v.umidade),
            ppm_MQ2 = resultados.Average(v => v.ppm_MQ2),
            ppm_CO_MQ7 = resultados.Average(v => v.ppm_CO_MQ7),
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
            { "_id", new BsonDocument
                {
                    { "ano",   new BsonDocument("$year", "$LeituraEm") },
                    { "mes",   new BsonDocument("$month", "$LeituraEm") },
                    { "dia",   new BsonDocument("$dayOfMonth", "$LeituraEm") },
                    { "blocoTempo", new BsonDocument("$subtract", new BsonArray
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
            { "temperaturas",      new BsonDocument("$push", "$temperatura") },
            { "umidades",          new BsonDocument("$push", "$umidade") },
            { "fumacas",           new BsonDocument("$push", "$ppm_MQ2") },
            { "coDetectados",      new BsonDocument("$push", "$ppm_CO_MQ7") },  // <<< Descomente e use '$ppm_CO_MQ7'
            { "chamasDetectadas",  new BsonDocument("$push", "$chamaDetectada") },
            { "LeituraMaisRecente",new BsonDocument("$max",  "$LeituraEm") }
            })
            .Sort(Builders<BsonDocument>.Sort.Ascending("_id"));

        var documentos = await pipeline.ToListAsync();

        var resultados = documentos.Select(d =>
        {
            // Extrai arrays e trata possíveis vazios
            var tempArr = d["temperaturas"].AsBsonArray.Select(x => x.ToDouble()).ToList();
            var humArr = d["umidades"].AsBsonArray.Select(x => x.ToDouble()).ToList();
            var mq2Arr = d["fumacas"].AsBsonArray.Select(x => x.ToDouble()).ToList();
            var coArr = d["coDetectados"].AsBsonArray.Select(x => x.ToDouble()).ToList();
            var chamaArr = d["chamasDetectadas"].AsBsonArray.Select(x => x.ToBoolean() ? 1.0 : 0.0).ToList();

            return new Valores
            {
                LeituraEm = d["LeituraMaisRecente"].ToUniversalTime(),
                temperatura = tempArr.Any() ? (float)Mediana(tempArr) : 0f,
                umidade = humArr.Any() ? (float)Mediana(humArr) : 0f,
                ppm_MQ2 = mq2Arr.Any() ? (float)Mediana(mq2Arr) : 0f,
                ppm_CO_MQ7 = coArr.Any() ? Mediana(coArr) : 0d,
                chamaDetectada = Mediana(chamaArr) >= 0.5
            };
        });

        // Agregação final: mediana dos blocos
        var lista = resultados.ToList();
        return new Valores
        {
            LeituraEm = lista.Max(v => v.LeituraEm),
            temperatura = (float)Mediana(lista.Select(v => v.temperatura).Select(x => (double)x).ToList()),
            umidade = (float)Mediana(lista.Select(v => (double)v.umidade).ToList()),
            ppm_MQ2 = (float)Mediana(lista.Select(v => (double)v.ppm_MQ2).ToList()),
            ppm_CO_MQ7 = Mediana(lista.Select(v => (double)v.ppm_CO_MQ7).ToList()),
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