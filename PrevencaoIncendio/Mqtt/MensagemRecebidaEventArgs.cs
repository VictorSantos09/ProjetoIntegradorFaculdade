namespace PrevencaoIncendio.Mqtt;

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
