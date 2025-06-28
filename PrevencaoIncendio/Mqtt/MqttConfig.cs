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
        var valores = new Valores
        {
            ppm_MQ2 = Random.Shared.Next(1023),
            chamaDetectada = true,
            temperatura = Random.Shared.Next(50),
            umidade = Random.Shared.Next(100),
            ppm_CO_MQ7 = Random.Shared.Next(4095),
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
