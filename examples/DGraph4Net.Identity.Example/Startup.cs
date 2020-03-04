using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Grpc.Core;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Dgraph4Net.Identity.Example
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "<Pending>")]
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
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

            var channel = new Channel("localhost:9080", ChannelCredentials.Insecure);
            var dgraph = new Dgraph4NetClient(channel);
            // sends mapping to Dgraph
            var types = dgraph.Map(typeof(DUser).Assembly);

            services.AddIdentity<DUser, DRole>(options => options.SignIn.RequireConfirmedAccount = true)
                .AddRoleStore<RoleStore>()
                .AddUserStore<UserStore>()
                .AddDefaultTokenProviders()
                .AddDefaultUI();
            services.AddRazorPages();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        // ReSharper disable once UnusedMember.Global
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapRazorPages();
            });
        }
    }
}