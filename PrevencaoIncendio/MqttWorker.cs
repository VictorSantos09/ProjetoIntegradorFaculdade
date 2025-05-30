using PrevencaoIncendio.Repositories;

namespace PrevencaoIncendio;

public class MqttWorker : BackgroundService
{
    private readonly IValoresRepository _repo;

    public MqttWorker(IValoresRepository repo)
    {
        _repo = repo;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await MqttConfig.Configure(_repo);
    }
}
