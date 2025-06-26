namespace PrevencaoIncendio.Config.Danger;

public class DangerParameters
{
    public DangerOptions Baixo { get; set; }
    public DangerOptions Medio { get; set; }
    public DangerOptions Alto { get; set; }
}

public record DangerOptions(float Ppm, float Temperatura);
