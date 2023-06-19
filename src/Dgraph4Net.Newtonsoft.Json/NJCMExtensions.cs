using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dgraph4Net.ActiveRecords;

public static class NJCMExtensions
{
    public static IServiceCollection AddDgraphNewtonsoft(this IServiceCollection services) =>
        services.AddDgraphNewtonsoft("DefaultConnection");

    public static IServiceCollection AddDgraphNewtonsoft(this IServiceCollection services, string connectionName) =>
       services.AddDgraphNewtonsoft(sp => sp.GetRequiredService<IConfiguration>().GetConnectionString(connectionName));

    public static IServiceCollection AddDgraphNewtonsoft(this IServiceCollection services, Func<IServiceProvider, string> getConnectionString)
    {
        ClassMapping.ImplClassMapping = new NJClassMapping();

        return services.AddDgraph(getConnectionString);
    }
}
