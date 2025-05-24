using System.Text.Json;
using MQTTnet;

namespace PrevencaoIncendio;

public class Valores
{
    public Guid Guid { get; set; }
    public int valorAnalogico_MQ2 { get; set; }
    public double ppm_MQ2 { get; set; }
    public bool chamaDetectada { get; set; }
    public double temperatura { get; set; }
    public double umidade { get; set; }
    public bool coDetectado { get; set; }
    public DateTime LeituraEm { get; set; }
}

public class Calibrando
{
    public bool calibrando { get; set; }
    public double Ro { get; set; }
    public double Rs { get; set; }
    public DateTime LeituraEm { get; set; }
}

public class MensagemRecebidaEventArgs : EventArgs
{
    public string Topico { get; set; }
    public string Mensagem { get; set; }

    public MensagemRecebidaEventArgs(string tópico, string mensagem)
    {
        Topico = tópico;
        Mensagem = mensagem;
    }
}

public static class MqttMensagem
{
    public static event EventHandler<MensagemRecebidaEventArgs>? MensagemRecebida;

    public static void OnMensagemRecebida(string topico, string mensagem)
    {
        MensagemRecebida?.Invoke(null, new MensagemRecebidaEventArgs(topico, mensagem));
    }
}
public class MqttConfig
{
    public static readonly MqttClientFactory Factory = new();
    public static readonly IMqttClient Client = Factory.CreateMqttClient();
    public const string Broker = "10.10.28.235";
    public const int Port = 1883;
    public static void ConfigureFaker()
    {
        var timer = new System.Timers.Timer(1000);

        timer.Elapsed += OnTimedEvent;

        // Configura o timer para ser executado apenas uma vez
        timer.AutoReset = false;

        // Inicia o timer
        timer.Start();
    }
    private static void OnTimedEvent(object source, System.Timers.ElapsedEventArgs e)
    {
        var valores = new Valores
        {
            Guid = Guid.NewGuid(),
            valorAnalogico_MQ2 = 100,
            ppm_MQ2 = 750,
            chamaDetectada = true,
            temperatura = 46.0,
            umidade = 60.0,
            coDetectado = false,
            LeituraEm = DateTime.Now
        };
        var jsonString = JsonSerializer.Serialize(valores);
        
        MqttMensagem.OnMensagemRecebida("sensor/#", jsonString);
    }
    public static async Task Configure()
    {
        string clientId = Guid.NewGuid().ToString();
        string topic = "sensor/#";
        MqttClientConnectResult connectResult = await Connect(clientId);

        if (connectResult.ResultCode == MqttClientConnectResultCode.Success)
        {
            Console.WriteLine("Connected to MQTT broker successfully.");
            await Client.SubscribeAsync(topic);
            Console.WriteLine($"Subscribed to topic: {topic}");

            Client.ApplicationMessageReceivedAsync += async e =>
            {
                var payloadString = e.ApplicationMessage.ConvertPayloadToString();
                var receivedTopic = e.ApplicationMessage.Topic;

                MqttMensagem.OnMensagemRecebida(receivedTopic, payloadString);
            };
        }
        else
        {
            Console.WriteLine($"Failed to connect to MQTT broker: {connectResult.ResultCode}");
        }
    }

    public static async Task<MqttClientConnectResult> Connect(string clientId)
    {
        MqttClientOptions options = GetOptions(clientId);

        var connectResult = await Client.ConnectAsync(options);
        return connectResult;
    }

    private static MqttClientOptions GetOptions(string clientId)
    {
        return new MqttClientOptionsBuilder()
                    .WithTcpServer(Broker, Port)
                    .WithClientId(clientId)
                    .WithCleanSession()
                    .Build();
    }
}
