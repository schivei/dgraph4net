using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

using Castle.DynamicProxy;
using Castle.DynamicProxy.Internal;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Dgraph4Net
{
    public class DGraphQLOptions
    {
        public GraphiQLOptions AdminGraphiQLOptions { get; } = new GraphiQLOptions
        {
            Enabled = true,
            GraphQlUIPath = "/admin-graphiql",
            GraphQlEndpoint = "/admin"
        };

        public GraphiQLOptions PublicGraphiQLOptions { get; } = new GraphiQLOptions
        {
            Enabled = true,
            GraphQlUIPath = "/graphiql",
            GraphQlEndpoint = "/graphql"
        };

        public ReflectionOptions Mapping { get; } = new ReflectionOptions();

        public ProxyOptions Proxy { get; } = new ProxyOptions();

        public Action<WebSocketOptions> WebSockets { get; set; } = delegate { };

        public Func<HttpContext, Task<bool>> IsAuthenticated { get; set; } = delegate { return Task.FromResult(true); };

        internal bool UsingIdentity { get; private set; }

        internal object? Security { get; private set; }

        public DGraphQLOptions UseIdentity<TSU, TUM, TUser>(SymmetricSecurityKey issuerSigningKey)
            where TSU : SignInManager<TUser>
            where TUM : UserManager<TUser>
            where TUser : class
        {
            UsingIdentity = true;
            Security = new Security<TSU, TUM, TUser>(issuerSigningKey);
            return this;
        }

        internal DGraphQLOptions() { }
    }

    internal class Security<TSU, TUM, TUser>
            where TSU : SignInManager<TUser>
            where TUM : UserManager<TUser>
            where TUser : class
    {
        public SymmetricSecurityKey IssuerSigningKey { get; }

        public TSU? SignInManager { get; private set; }

        public TUM? UserManager { get; private set; }

        public Security(SymmetricSecurityKey issuerSigningKey)
        {
            IssuerSigningKey = issuerSigningKey;
        }

        public void Load(IServiceProvider sp)
        {
            SignInManager = sp.GetRequiredService<TSU>();
            UserManager = sp.GetRequiredService<TUM>();
        }
    }

    public class GraphiQLOptions
    {
        internal GraphiQLOptions() { }

        public bool Enabled { get; set; } = true;

        public PathString GraphQlUIPath { get; set; }

        public PathString GraphQlEndpoint { get; set; }
    }

    public class ProxyOptions
    {
        internal ProxyOptions() { }

        public string ConfigSection { get; set; } = "ReverseProxy";
    }

    public class ReflectionOptions
    {
        internal ReflectionOptions()
        {
            Assemblies = new[] { Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly() };
        }

        internal Assembly[] Assemblies { get; private set; }

        public ReflectionOptions DefineForInterfaces<TInterfaces>(bool intercepts = false)
        {
            var ti = typeof(TInterfaces);
            Assemblies = new[] { ti.Assembly };

            InterfacesNamespaces.Add(ti.Namespace ?? throw new InvalidOperationException($"Can not reach {ti.Name} Namespace."));

            UseInterfaces = true;

            if (!intercepts)
            {
                return this;
            }

            Type[] interfaces = Types.Where(x => x.IsInterface).ToArray();

            Type[] interceptors = Assemblies.SelectMany(x => x.GetTypes())
                .Where(x => !x.IsAbstract && !x.IsSealed &&
                x.IsClass && x.GetConstructors()
                .Any(c => c.IsPublic && !c.IsStatic && c.GetParameters().Length == 0) &&
                (x.IsAssignableFrom(typeof(IInterceptor)) ||
                x.IsAssignableFrom(typeof(DGraphQLInterceptor<>)))).ToArray();

            foreach (Type interceptor in interceptors)
            {
                foreach (Type @interface in interfaces)
                {
                    var di = typeof(DGraphQLInterceptor<>)
                        .MakeGenericType(@interface);

                    if (interceptor.IsAssignableFrom(di))
                    {
                        var instance = interceptor.GetConstructors()
                            .First(c => c.IsPublic && !c.IsStatic && c.GetParameters().Length == 0)
                            .Invoke(parameters: null);

                        if (instance is IInterceptor i)
                        {
                            Interceptions.AddOrUpdate(@interface, i, delegate
                            {
                                return i;
                            });
                        }
                    }
                }
            }

            return this;
        }

        public ReflectionOptions DefineWith<TClasses, TInterfaces>(bool useDefinedNamespaces = true)
        {
            var tc = typeof(TClasses);
            var ti = typeof(TInterfaces);
            Assemblies = new[] { tc.Assembly, ti.Assembly }.Distinct().ToArray();

            if (useDefinedNamespaces)
            {
                ClassesNamespaces.Add(tc.Namespace ?? throw new InvalidOperationException($"Can not reach {tc.Name} Namespace."));
                InterfacesNamespaces.Add(ti.Namespace ?? throw new InvalidOperationException($"Can not reach {ti.Name} Namespace."));
            }

            return this;
        }

        public ReflectionOptions MapAssembly(params Assembly[] assemblies)
        {
            Assemblies = assemblies;
            return this;
        }

        public bool UseInterfaces { get; private set; }

        public string ClassPrefix { get; set; } = string.Empty;

        public string ClassSuffix { get; set; } = string.Empty;

        public string InterfacePrefix { get; set; } = "I";

        public string InterfaceSuffix { get; set; } = string.Empty;

        public ICollection<string> ClassesNamespaces { get; } = new List<string>();

        public ICollection<string> InterfacesNamespaces { get; } = new List<string>();

        internal IEnumerable<Type> Types => UseInterfaces ?
            Assemblies.SelectMany(a => a.GetTypes()).Where(t => (t.IsEnum || t.IsInterface)
                && t.IsPublic && !t.IsGenericType) :
            Assemblies.SelectMany(a => a.GetTypes()).Where(t => (t.IsEnum || t.IsInterface ||
                (t.IsClass && !t.IsAbstract && !t.IsSealed)) && t.IsPublic && !t.IsGenericType);

        internal ConcurrentDictionary<Type, IInterceptor> Interceptions { get; }
            = new ConcurrentDictionary<Type, IInterceptor>();

        public ReflectionOptions Intercept<TI, T>()
            where T : DGraphQLInterceptor<TI>, IInterceptor, new()
        {
            if (!UseInterfaces)
            {
                throw new ArgumentException("To use interception, you need to only map interfaces and enumerations.");
            }

            var ti = typeof(TI);

            if (!ti.IsInterface)
            {
                throw new ArgumentException("TI is not an interface.");
            }

            var interceptor = new T();

            Interceptions.AddOrUpdate(ti, interceptor, delegate
            {
                return interceptor;
            });

            return this;
        }
    }

    public abstract class DGraphQLInterceptor<T> : IInterceptor
    {
        public void Intercept(IInvocation invocation)
        {
            ExecuteBefore(invocation);

            invocation.Proceed();

            ExecuteAfter(invocation);
        }

        protected abstract void ExecuteAfter(IInvocation invocation);

        protected abstract void ExecuteBefore(IInvocation invocation);
    }
}
