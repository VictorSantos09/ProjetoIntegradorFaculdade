using MQTTnet;

namespace PrevencaoIncendio;

public class EnvioESP
{
    public static async Task EnviarValor(string valor)
    {
        var mqttClient = MqttConfig.Client;
        //await MqttConfig.Connect(Guid.NewGuid().ToString());

        var options = new MqttClientOptionsBuilder()
            .WithTcpServer("10.10.29.151", 1883) // IP do Mosquitto
            .WithClientId(Guid.NewGuid().ToString())
            .Build();

        //await mqttClient.ConnectAsync(options);

        var message = new MqttApplicationMessageBuilder()
            .WithTopic("sensor/config")
            .WithPayload(valor)
            .WithContentType("application/json")
            .WithRetainFlag(false)
            .Build();

        await mqttClient.PublishAsync(message);

        Console.WriteLine("Mensagem enviada para o ESP8266!");
        //await mqttClient.DisconnectAsync();
    }
}