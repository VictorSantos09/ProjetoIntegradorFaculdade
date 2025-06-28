using System.Text.Json;
using Microsoft.Extensions.Options;
using MQTTnet;
using PrevencaoIncendio.Config.Ip;
using PrevencaoIncendio.Models;
using PrevencaoIncendio.Repositories;

namespace PrevencaoIncendio.Mqtt;
public class MqttConfig
{
    public static readonly MqttClientFactory Factory = new();
    public static readonly IMqttClient Client = Factory.CreateMqttClient();
    #region FAKER
    public static void ConfigureFaker()
    {
        var timer = new System.Timers.Timer(1000);

        timer.Elapsed += OnTimedEvent;

        timer.AutoReset = true;
        timer.Enabled = true;

        timer.Start();
    }
    private static void OnTimedEvent(object source, System.Timers.ElapsedEventArgs e)
    {
        var random = new Random();

        var valores = new Valores
        {
            // MQ-2: ppm realista entre 200 e 800 (simula fumaça moderada)
            ppm_MQ2 = random.NextDouble() switch
            {
                < 0.1 => random.Next(800, 1200),  // 10% chance de valor alto (alarme)
                < 0.9 => random.Next(200, 600),   // 80% chance de valor moderado
                _ => random.Next(0, 200)      // 10% chance de valor baixo
            },

            // Chama: simula intermitência
            chamaDetectada = random.NextDouble() < 0.2, // 20% de chance de detectar chama

            // Temperatura: valores entre 18 e 35 °C
            temperatura = ((float)Math.Round(18 + random.NextDouble() * 17, 1)),

            // Umidade: entre 40% e 80%
            umidade = Math.Round(40 + random.NextDouble() * 40, 1),

            // MQ-7 (CO): valores típicos entre 10 e 400 ppm
            ppm_CO_MQ7 = random.NextDouble() switch
            {
                < 0.05 => random.Next(400, 1000),  // picos
                < 0.9 => random.Next(30, 250),    // normal
                _ => random.Next(0, 30)       // limpo
            },

            LeituraEm = DateTime.Now
        };

        var jsonString = JsonSerializer.Serialize(valores);

        MqttMensagem.OnMensagemRecebida("sensor/#", jsonString);
    }
    #endregion
    public static async Task Configure(IValoresRepository valoresRepository, IOptions<IpAddress> ipAddress)
    {
        string clientId = Guid.NewGuid().ToString();
        string topic = "sensor/#";
        MqttClientConnectResult connectResult = await Connect(clientId, ipAddress.Value);

        if (connectResult.ResultCode == MqttClientConnectResultCode.Success)
        {
            Console.WriteLine("Connected to MQTT broker successfully.");
            await Client.SubscribeAsync(topic);
            Console.WriteLine($"Subscribed to topic: {topic}");

            Client.ApplicationMessageReceivedAsync += async e =>
            {
                var payloadString = e.ApplicationMessage.ConvertPayloadToString();
                var receivedTopic = e.ApplicationMessage.Topic;

                try
                {
                    var valores = JsonSerializer.Deserialize<Valores>(payloadString);
                    if (valores is not null)
                    {
                        await valoresRepository.InsertOne(valores);
                    }
                }
                finally
                {
                    MqttMensagem.OnMensagemRecebida(receivedTopic, payloadString);
                }
            };
        }
        else
        {
            Console.WriteLine($"Failed to connect to MQTT broker: {connectResult.ResultCode}");
        }
    }

    public static async Task<MqttClientConnectResult> Connect(string clientId, IpAddress ipAddress)
    {
        MqttClientOptions options = GetOptions(clientId, ipAddress);

        var connectResult = await Client.ConnectAsync(options);
        return connectResult;
    }

    private static MqttClientOptions GetOptions(string clientId, IpAddress ipAddress)
    {
        return new MqttClientOptionsBuilder()
                    .WithTcpServer(ipAddress.Broker, ipAddress.Port)
                    .WithClientId(clientId)
                    .WithCleanSession()
                    .Build();
    }
}
