using Dgraph4Net.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var arguments = args.ToList();

#if DEBUG
// set OS environment variable to GRPC_DNS_RESOLVER=native
if (Environment.GetEnvironmentVariable("GRPC_DNS_RESOLVER") != "native")
{
    Environment.SetEnvironmentVariable("GRPC_DNS_RESOLVER", "native");
}

if (arguments.Count <= 1)
{
    arguments.Add("migration");
    arguments.Add("up");
    arguments.Add("-s 192.168.15.8:9080");
    //arguments.Add("add");
    //arguments.Add("Test" + Guid.NewGuid().ToString().Replace("-", string.Empty));
    arguments.Add("--project");
    // get poco location relative to this file using Path class
    arguments.Add(Path.Combine("src", "Dgraph4Net.Core", "Dgraph4Net.Core.csproj"));
}
#endif

var builder = Host.CreateApplicationBuilder(arguments.ToArray());

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

await app.ExecuteAsync(arguments.ToArray());
