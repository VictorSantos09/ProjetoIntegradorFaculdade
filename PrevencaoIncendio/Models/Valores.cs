using MongoDB.Bson.Serialization.Attributes;

namespace PrevencaoIncendio.Models;

public class Valores
{
    [BsonId]
    [BsonRepresentation(MongoDB.Bson.BsonType.ObjectId)]
    public string Id { get; set; }
    public int valorAnalogico_MQ2 { get; set; }
    public double ppm_MQ2 { get; set; }
    public bool chamaDetectada { get; set; }
    public double temperatura { get; set; }
    public double umidade { get; set; }
    public bool coDetectado { get; set; }
    public DateTime LeituraEm { get; set; }
}
