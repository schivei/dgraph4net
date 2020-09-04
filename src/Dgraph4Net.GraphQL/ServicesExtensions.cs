using System;

using Dgraph4Net;

using GraphiQl;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.WebSockets;
using Microsoft.Extensions.Configuration;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class ServicesExtensions
    {
        public static IServiceCollection AddDgraphQL(this IServiceCollection services,
            IConfiguration configuration,
            Action<DGraphQLOptions> optionsBuilder = null)
        {
            optionsBuilder ??= delegate { };

            var defaultOptions = new DGraphQLOptions();

            optionsBuilder(defaultOptions);

            configuration.Bind("DGraphQLOptions", defaultOptions);

            services.AddSingleton(defaultOptions);

            services.AddWebSockets(defaultOptions.WebSockets);

            var proxy = services.AddReverseProxy();

            proxy.LoadFromConfig(configuration.GetSection(defaultOptions.Proxy.ConfigSection));

            services.AddGraphiQl(options => options.IsAuthenticated = defaultOptions.IsAuthenticated);

            return services;
        }

        public static IApplicationBuilder UseDGraphQLUI(this IApplicationBuilder app)
        {
            var dqlo = app.ApplicationServices.GetRequiredService<DGraphQLOptions>();

            if (dqlo.AdminGraphiQLOptions.Enabled)
            {
                app.UseGraphiQl(dqlo.AdminGraphiQLOptions.GraphQlUIPath, dqlo.AdminGraphiQLOptions.GraphQlEndpoint);
            }

            if (dqlo.PublicGraphiQLOptions.Enabled)
            {
                app.UseGraphiQl(dqlo.PublicGraphiQLOptions.GraphQlUIPath, dqlo.PublicGraphiQLOptions.GraphQlEndpoint);
            }

            return app;
        }

        public static IEndpointRouteBuilder UseDGraphQL(this IEndpointRouteBuilder endpoints, Action<IApplicationBuilder> configureApp = null)
        {
            configureApp ??= _ => { };

            endpoints.MapReverseProxy(configureApp);

            return endpoints;
        }
    }
}
