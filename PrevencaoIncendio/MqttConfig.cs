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
    public string Tópico { get; set; }
    public string Mensagem { get; set; }

    public MensagemRecebidaEventArgs(string tópico, string mensagem)
    {
        Tópico = tópico;
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
    public static async Task Configure()
    {
        string broker = "192.168.0.102";
        int port = 1883;
        string clientId = Guid.NewGuid().ToString();
        string topic = "sensor/#";

        var factory = new MqttClientFactory();
        var mqttClient = factory.CreateMqttClient();

        var options = new MqttClientOptionsBuilder()
            .WithTcpServer(broker, port)
            .WithClientId(clientId)
            .WithCleanSession()
            .Build();

        var connectResult = await mqttClient.ConnectAsync(options);

        if (connectResult.ResultCode == MqttClientConnectResultCode.Success)
        {
            Console.WriteLine("Connected to MQTT broker successfully.");
            await mqttClient.SubscribeAsync(topic);
            Console.WriteLine($"Subscribed to topic: {topic}");

            mqttClient.ApplicationMessageReceivedAsync += async e =>
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
}