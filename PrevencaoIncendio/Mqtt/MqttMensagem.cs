namespace PrevencaoIncendio.Mqtt;

public static class MqttMensagem
{
    public static event EventHandler<MensagemRecebidaEventArgs>? MensagemRecebida;

    public static void OnMensagemRecebida(string topico, string mensagem)
    {
        MensagemRecebida?.Invoke(null, new MensagemRecebidaEventArgs(topico, mensagem));
    }
}
