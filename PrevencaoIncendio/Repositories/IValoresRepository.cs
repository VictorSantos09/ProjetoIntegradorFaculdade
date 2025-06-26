using PrevencaoIncendio.Models;

namespace PrevencaoIncendio.Repositories;

public interface IValoresRepository
{
    Task<IEnumerable<Valores>> GetAll();
    Task<IEnumerable<Valores>> GetLastMinutesGroupedAsync(int days = 30);
    Task<Valores> GetNextHourAveragesAsync(DateTime inicio, DateTime fim, int horasGrupo = 1);
    Task InsertOne(Valores valores);
}