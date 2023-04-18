using Dgraph4Net.OpenIddict.Models;

using Grpc.Core;

using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using OpenIddict.Abstractions;
using OpenIddict.Core;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Dgraph4Net.OpenId.Example;

public class Startup
{
    public Startup(IConfiguration configuration)
    {
        Configuration = configuration;
    }

    public IConfiguration Configuration { get; }

    // This method gets called by the runtime. Use this method to add services to the container.
    // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddControllersWithViews();

        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.All;
        });

        services.AddSingleton(Configuration);

        services.AddTransient<ChannelBase>(delegate
        {
            return new Channel("localhost:9080", ChannelCredentials.Insecure);
        });

        services.AddTransient(sp =>
            new Dgraph4NetClient(sp.GetRequiredService<ChannelBase>()));

        ClassFactory.MapAssembly(typeof(OpenIddictDgraphExtensions).Assembly);

        var channel = new Channel("localhost:9080", ChannelCredentials.Insecure);
        var dgraph = new Dgraph4NetClient(channel);

        if (!File.Exists("schema.dgraph"))
            dgraph.Alter(new Api.Operation { DropAll = true }).GetAwaiter().GetResult();

        // sends mapping to Dgraph
        dgraph.Map(typeof(OpenIddictDgraphExtensions).Assembly);

        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie()
            .AddOAuth("oidc", options => {
                options.AuthorizationEndpoint = "/connect/authorize";
            });

        services.AddOpenIddict().AddCore(options => {
            options.UseDgraph();
        })
        // Register the OpenIddict server components.
        .AddServer(options =>
        {
            // Enable the token endpoint.
            options.SetTokenEndpointUris("/connect/token");

            // Enable the client credentials flow.
            options.AllowClientCredentialsFlow();

            // Register the signing and encryption credentials.
            options.AddDevelopmentEncryptionCertificate()
                    .AddDevelopmentSigningCertificate();

            // Register the ASP.NET Core host and configure the ASP.NET Core-specific options.
            options.UseAspNetCore()
                    .EnableTokenEndpointPassthrough();
        })

        // Register the OpenIddict validation components.
        .AddValidation(options =>
        {
            // Import the configuration from the local OpenIddict server instance.
            options.UseLocalServer();

            // Register the ASP.NET Core host.
            options.UseAspNetCore();
        });
    ;

        services.AddMvcCore();
    }

    // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        app.UseRouting();

        app.UseAuthentication();
        app.UseAuthorization();

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();
            endpoints.MapDefaultControllerRoute();
        });

        app.UseWelcomePage();

        InitializeAsync(app.ApplicationServices, CancellationToken.None).GetAwaiter().GetResult();
    }

    private static async Task InitializeAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        // Create a new service scope to ensure the database context is correctly disposed when this methods returns.
        using var scope = services.GetRequiredService<IServiceScopeFactory>().CreateScope();

        var manager = scope.ServiceProvider.GetRequiredService<OpenIddictApplicationManager<OpenIddictDgraphApplication>>();

        if (await manager.FindByClientIdAsync("angular-app", cancellationToken) == null)
        {
            var descriptor = new OpenIddictApplicationDescriptor
            {
                ClientId = "angular-app",
                DisplayName = "Angular Application",
                PostLogoutRedirectUris = { new Uri("https://oidcdebugger.com/debug") },
                RedirectUris = { new Uri("https://oidcdebugger.com/debug") }
            };

            await manager.CreateAsync(descriptor, cancellationToken);
        }
    }
}
