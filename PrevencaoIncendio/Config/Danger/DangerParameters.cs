namespace PrevencaoIncendio.Config.Danger;

public class DangerParameters
{
    public double PercentualDiferenca { get; set; }
    public double Co { get; set; }
    public long TempoBuscaMediaMs { get; set; }
    public DateTime? DataInicioMedia { get; set; }
    public DateTime? DataFimMedia { get; set; }
    public DangerOptions Medio { get; set; }
    public DangerOptions Alto { get; set; }

    public void VerificarValores()
    {
        if (Medio is null)
            throw new ArgumentNullException(nameof(Medio), "Valores de perigo médio não podem ser nulos.");

        if (Alto is null)
            throw new ArgumentNullException(nameof(Alto), "Valores de perigo alto não podem ser nulos.");

        if (Medio.Ppm <= 0 || Medio.Temperatura <= 0)
            throw new ArgumentException("Valores de perigo médio devem ser maiores que zero.");
        if (Alto.Ppm <= 0 || Alto.Temperatura <= 0)
            throw new ArgumentException("Valores de perigo alto devem ser maiores que zero.");

        if (Co <= 0)
            throw new ArgumentException("Valores de monóxido de carbono devem ser maiores que zero.");
    }
}

public record DangerOptions(float Ppm, float Temperatura);
