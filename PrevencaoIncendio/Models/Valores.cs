using MongoDB.Bson.Serialization.Attributes;

namespace PrevencaoIncendio.Models;

public class Valores
{
    [BsonId]
    [BsonRepresentation(MongoDB.Bson.BsonType.ObjectId)]
    public string Id { get; set; }
    public double ppm_MQ2 { get; set; }
    public bool chamaDetectada { get; set; }
    public float temperatura { get; set; }
    public double umidade { get; set; }
    public double ppm_CO_MQ7 { get; set; }
    public DateTime LeituraEm { get; set; }

    public bool CoNaoSeguro => ppm_CO_MQ7 >= 1500;
}
