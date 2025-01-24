using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Dgraph4Net.ActiveRecords;

namespace Dgraph4Net;

public static class NjcmExtensions
{
    public static IServiceCollection AddDgraphNewtonsoft(this IServiceCollection services) =>
        services.AddDgraphNewtonsoft("DefaultConnection");

    public static IServiceCollection AddDgraphNewtonsoft(this IServiceCollection services, string connectionName) =>
       services.AddDgraphNewtonsoft(sp => sp.GetRequiredService<IConfiguration>().GetConnectionString(connectionName));

    public static IServiceCollection AddDgraphNewtonsoft(this IServiceCollection services, Func<IServiceProvider, string> getConnectionString)
    {
        ClassMapping.ImplClassMapping = new NjClassMapping();

        return services.AddDgraph(getConnectionString);
    }
}
