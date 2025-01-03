using Dgraph4Net.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var arguments = args.ToList();

#if DEBUG
if (Environment.GetEnvironmentVariable("GRPC_DNS_RESOLVER") != "native")
{
    Environment.SetEnvironmentVariable("GRPC_DNS_RESOLVER", "native");
}

if (arguments.Count <= 1)
{
    arguments.Add("migration");
    arguments.Add("up");
    arguments.Add("-s 127.0.0.1:9080");
    arguments.Add("--project");
    arguments.Add(Path.Combine("src", "Dgraph4Net.Core", "Dgraph4Net.Core.csproj"));
}
#endif

var builder = Host.CreateApplicationBuilder([.. arguments]);

typeof(Application).Assembly.GetTypes()
    .Where(t => t is { IsClass: true, IsAbstract: false }
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

await app.ExecuteAsync([.. arguments]);
