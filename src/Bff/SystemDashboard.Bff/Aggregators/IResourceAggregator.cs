using SystemDashboard.Bff.Models;

namespace SystemDashboard.Bff.Aggregators;

public interface IResourceAggregator
{
    Task<List<Resource>> GetAllResourcesAsync(CancellationToken ct = default);
    Task<Resource?> GetResourceByNameAsync(string name, CancellationToken ct = default);
}
