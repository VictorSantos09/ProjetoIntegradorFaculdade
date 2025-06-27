using PrevencaoIncendio.Repositories;

namespace PrevencaoIncendio.Config.Graphic;

public class GraphicParameters
{
    public DateTime Inicio { get; set; }
    public IntervaloAgrupamento Agrupamento { get; set; }
    public int? IntervaloMinuto { get; set; } = 60;

    public void VerificarParametros()
    {
        if (Inicio == default)
        {
            throw new ArgumentException("O parâmetro 'Inicio' não pode ser nulo ou vazio.");
        }
        if (!Enum.IsDefined(Agrupamento))
        {
            throw new ArgumentException("O parâmetro 'Agrupamento' é inválido.");
        }
    }
}
