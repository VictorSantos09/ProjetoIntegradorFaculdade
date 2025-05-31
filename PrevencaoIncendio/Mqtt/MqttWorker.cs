using Microsoft.Extensions.Options;
using PrevencaoIncendio.Config.Ip;
using PrevencaoIncendio.Repositories;

namespace PrevencaoIncendio.Mqtt;

public class MqttWorker : BackgroundService
{
    private readonly IValoresRepository _repo;
    private readonly IOptions<IpAddress> _optionsIpAddress;

    public MqttWorker(IValoresRepository repo, IOptions<IpAddress> optionsIpAddress)
    {
        _repo = repo;
        _optionsIpAddress = optionsIpAddress;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await MqttConfig.Configure(_repo, _optionsIpAddress);
    }
}
