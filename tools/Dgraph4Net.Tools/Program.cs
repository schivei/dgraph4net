using Dgraph4Net.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);

typeof(Application).Assembly.GetTypes()
    .Where(t => t.IsClass && !t.IsAbstract
             && !string.IsNullOrEmpty(t.Namespace) && t.Namespace.StartsWith("Dgraph4Net.Tools"))
    .ToList()
    .ForEach(t => {
        try
        {
            builder.Services.AddSingleton(t);
        }
        catch
        {
            // ignored
        }
    });

builder.Logging.AddConsole();

var host = builder.Build();

var app = host.Services.GetRequiredService<Application>();

var arguments = args.ToList();

#if DEBUG
if (arguments.Count <= 1)
{
    arguments.Add("migration");
    arguments.Add("add");
    arguments.Add("Test" + Guid.NewGuid().ToString().Replace("-", string.Empty));
    arguments.Add("--project");
    arguments.Add(@"C:\Users\EltonSchiveiCosta\source\repos\schivei\dgraph4net\examples\PocoMapping\PocoMapping.csproj");
}
#endif

await app.ExecuteAsync(arguments.ToArray());
