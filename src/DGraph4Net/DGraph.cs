using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DGraph4Net.Annotations;
using DGraph4Net.Services;
using Grpc.Core;
using Newtonsoft.Json;
using static DGraph4Net.Services.Dgraph;

namespace DGraph4Net
{
    public class DGraph : IAsyncDisposable, IDisposable
    {
        private CancellationTokenSource _cancellationTokenSource;
        private Mutex _mtx;
        private DgraphClient[] _dgraphClients;
        private Jwt _jwt;

        /// <summary>
        /// Check if context is disposed
        /// </summary>
        public bool Disposed { get; private set; }

        private DGraph()
        {
            _cancellationTokenSource = new CancellationTokenSource();
            _mtx = new Mutex();
        }

        /// <summary>
        /// Creates a new Dgraph (client) for interacting with Alphas.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The client is backed by multiple connections to the same or different
        /// servers in a cluster.
        /// </para>
        /// <para>A single Dgraph (client) is thread safe for sharing with multiple routines.</para>
        /// </remarks>
        /// <param name="dgraphClients"></param>
        public DGraph(params DgraphClient[] dgraphClients) : this() =>
            _dgraphClients = dgraphClients;

        /// <summary>
        /// Creates a new Dgraph (client) for interacting with Alphas.
        /// </summary>
        /// <para>
        /// The client is backed by multiple connections to the same or different
        /// servers in a cluster.
        /// </para>
        /// <para>A single Dgraph (client) is thread safe for sharing with multiple routines.</para>
        /// <param name="channels"></param>
        public DGraph(params ChannelBase[] channels) : this(channels.Select(channel => new DgraphClient(channel)).ToArray()) { }

        /// <summary>
        /// Auth userid & password to retrieve Jwt
        /// </summary>
        /// <param name="userid"></param>
        /// <param name="password"></param>
        /// <exception cref="RpcException">If login has failed.</exception>
        /// <exception cref="NotSupportedException">If no Refresh Jwt are defined.</exception>
        /// <exception cref="ObjectDisposedException">If client are disposed.</exception>
        // ReSharper disable once UnusedMember.Global
        public async Task Login(string userid, string password)
        {
            if (Disposed)
                throw new ObjectDisposedException(nameof(DGraph));

            _mtx.WaitOne();

            try
            {
                var dc = AnyClient();

                var loginRequest = new LoginRequest
                {
                    Userid = userid,
                    Password = password
                };

                var response = await dc.LoginAsync(loginRequest);

                _jwt = Jwt.Parser.ParseFrom(response.Json);
            }
            finally
            {
                _mtx.ReleaseMutex();
            }
        }

        /// <summary>
        /// Can be used to do the following by setting various fields of <see cref="Operation"/>:
        /// </summary>
        /// <remarks>
        /// <list type="number">
        /// <item>Modify the schema.</item>
        /// <item>Drop a predicate.</item>
        /// <item>Drop a database.</item>
        /// </list>
        /// </remarks>
        /// <param name="operation"></param>
        /// <exception cref="RpcException">If login has failed.</exception>
        /// <exception cref="NotSupportedException">If no Refresh Jwt are defined.</exception>
        /// <exception cref="ObjectDisposedException">If client are disposed.</exception>
        public async Task<Payload> Alter(Operation operation)
        {
            if (Disposed)
                throw new ObjectDisposedException(nameof(DGraph));

            var dc = AnyClient();

            var co = GetOptions();

            try
            {
                return await dc.AlterAsync(operation, co);
            }
            catch (RpcException err)
            {
                if (!IsJwtExpired(err))
                    throw;

                await RetryLogin();
                co = GetOptions();
                return await dc.AlterAsync(operation, co);
            }
        }

        /// <summary>
        /// DeleteEdges sets the edges corresponding to predicates
        /// on the node with the given uid for deletion.
        /// </summary>
        /// <remarks>
        /// This helper function doesn't run the mutation on the server.
        /// Txn needs to be committed in order to execute the mutation.
        /// </remarks>
        /// <param name="mutation"></param>
        /// <param name="uid"></param>
        /// <param name="predicates"></param>
        // ReSharper disable once UnusedMember.Global
        public static void DeleteEdges(Mutation mutation, string uid, params string[] predicates)
        {
            if (predicates is null || predicates.Length == 0)
                return;

            foreach (var predicate in predicates)
            {
                mutation.Del.Add(new NQuad
                {
                    Subject = uid,
                    Predicate = predicate,
                    ObjectValue = new Value
                    {
                        DefaultVal = "_STAR_ALL"
                    }
                });
            }
        }

        internal DgraphClient AnyClient() =>
            _dgraphClients[new Random().Next(0, _dgraphClients.Length)];

        /// <summary>
        /// Current Cancellation Token Source
        /// </summary>
        internal CancellationTokenSource GetTokenSource() =>
            _cancellationTokenSource;

        /// <summary>
        /// Get Header Options
        /// </summary>
        /// <returns><see cref="CallOptions"/></returns>
        /// <exception cref="ObjectDisposedException">If client are disposed.</exception>
        internal CallOptions GetOptions()
        {
            if (Disposed)
                throw new ObjectDisposedException(nameof(DGraph));

            _mtx.WaitOne();

            try
            {
                if (string.IsNullOrEmpty(_jwt?.AccessJwt?.Trim()))
                    return new CallOptions();

                var md = new Metadata { { "accessJwt", _jwt.AccessJwt } };

                return new CallOptions(md);
            }
            finally
            {
                _mtx.ReleaseMutex();
            }
        }

        /// <summary>
        /// Check if JWT has expired
        /// </summary>
        /// <param name="error"></param>
        /// <returns>true if the error indicates that the jwt has expired.</returns>
        internal bool IsJwtExpired(RpcException error)
        {
            if (error is null)
                return false;

            return error.StatusCode == StatusCode.Unauthenticated && error.Message.Contains("Token is expired", StringComparison.InvariantCulture);
        }

        /// <summary>
        /// Retry to login
        /// </summary>
        /// <exception cref="RpcException">If login has failed.</exception>
        /// <exception cref="NotSupportedException">If no Refresh Jwt are defined.</exception>
        /// <exception cref="ObjectDisposedException">If client are disposed.</exception>
        internal async Task RetryLogin()
        {
            if (Disposed)
                throw new ObjectDisposedException(nameof(DGraph));

            _mtx.WaitOne();

            try
            {
                if (string.IsNullOrEmpty(_jwt?.RefreshJwt?.Trim()))
                    throw new NotSupportedException("Refresh Jwt should not be empty.");

                var dc = AnyClient();
                var loginRequest = new LoginRequest
                {
                    RefreshToken = _jwt.RefreshJwt,
                };

                var resp = await dc.LoginAsync(loginRequest);

                _jwt = Jwt.Parser.ParseFrom(resp.Json);
            }
            finally
            {
                _mtx.ReleaseMutex();
            }
        }

        /// <summary>
        /// Creates a new transaction.
        /// </summary>
        /// <param name="readOnly"></param>
        /// <param name="bestEffort"></param>
        /// <param name="cancellationToken"></param>
        /// <exception cref="InvalidOperationException">If best effort is true and the transaction is not read-only.</exception>
        /// <returns cref="Txn">Transaction</returns>
        public Txn NewTransaction(bool readOnly = false, bool bestEffort = false, CancellationToken? cancellationToken = null) =>
            new Txn(this, readOnly, bestEffort, cancellationToken);

        #region IDisposable Support
        protected virtual void Dispose(bool disposing)
        {
            if (Disposed)
                return;

            Disposed = true;

            if (disposing)
            {
                _dgraphClients = Array.Empty<DgraphClient>();
                _mtx?.Dispose();
                _cancellationTokenSource?.Cancel(false);
            }

            _jwt = null;
            _mtx = null;
            _cancellationTokenSource = null;
        }

        ~DGraph()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(false);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public ValueTask DisposeAsync()
        {
            return new ValueTask(Task.Run(delegate
            {
                Dispose(true);
            }));
        }
        #endregion

        /// <summary>
        /// Generates mapping
        /// </summary>
        public StringBuilder Map()
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();

            var types = assemblies
                .SelectMany(assembly => assembly.GetTypes()
                    .Where(t => t.GetCustomAttributes()
                        .Any(att => att is DGraphTypeAttribute)));

            var properties =
            types.SelectMany(type => type.GetProperties()
                .Where(prop => prop.GetCustomAttributes()
                    .Any(attr => attr is StringPredicateAttribute ||
                                 attr is CommonPredicateAttribute ||
                                 attr is DateTimePredicateAttribute ||
                                 attr is PasswordPredicateAttribute ||
                                 attr is ReversePredicateAttribute)));

            var triples =
            properties.Where(prop => prop.DeclaringType != null &&
                                     !typeof(IDictionary).IsAssignableFrom(prop.DeclaringType) &&
                                     !typeof(KeyValuePair).IsAssignableFrom(prop.DeclaringType) &&
                                     prop.GetCustomAttributes()
                                         .Where(attr => attr is JsonPropertyAttribute)
                                         .OfType<JsonPropertyAttribute>()
                                         .All(jattr =>
                                             jattr.PropertyName?.Contains("|") != true))
                .Select(prop =>
                {
                    var jattr =
                        prop.GetCustomAttributes()
                            .Where(attr => attr is JsonPropertyAttribute)
                            .OfType<JsonPropertyAttribute>().FirstOrDefault() ??
                        new JsonPropertyAttribute(NormalizeName(prop.Name));

                    if (jattr.PropertyName == "dgraph.type" || jattr.PropertyName == "uid")
                        return (null, null, null, null, null);

                    var pAttr = prop
                                    .GetCustomAttributes()
                                    .FirstOrDefault(attr => attr is StringPredicateAttribute ||
                                                            attr is CommonPredicateAttribute ||
                                                            attr is DateTimePredicateAttribute ||
                                                            attr is PasswordPredicateAttribute ||
                                                            attr is ReversePredicateAttribute) ??
                                new CommonPredicateAttribute();

                    var princType = prop.PropertyType.IsGenericType
                        ? prop.PropertyType.GetGenericArguments().First()
                        : prop.PropertyType;

                    var isList = prop.PropertyType != typeof(string) &&
                                 typeof(IEnumerable).IsAssignableFrom(prop.PropertyType);

                    if (!isList && prop.PropertyType.IsGenericType)
                        princType = prop.PropertyType;

                    if (isList && typeof(char[]) == prop.PropertyType)
                    {
                        isList = false;
                        princType = prop.PropertyType;
                    }

                    var propType =
                        princType == typeof(Uid) ||
                        princType == typeof(Uid?) ? "uid" :
                        princType.IsEnum ||
                        princType == typeof(string) ||
                        princType == typeof(char) ||
                        princType == typeof(char[]) ||
                        princType == typeof(char?) ||
                        princType == typeof(TimeSpan) ||
                        princType == typeof(TimeSpan?) ? "string" :
                        princType == typeof(float) ||
                        princType == typeof(double) ||
                        princType == typeof(decimal) ||
                        princType == typeof(float?) ||
                        princType == typeof(double?) ||
                        princType == typeof(decimal?) ? "float" :
                        princType == typeof(int) ||
                        princType == typeof(uint) ||
                        princType == typeof(long) ||
                        princType == typeof(ulong) ||
                        princType == typeof(short) ||
                        princType == typeof(ushort) ||
                        princType == typeof(byte) ||
                        princType == typeof(sbyte) ||
                        princType == typeof(int?) ||
                        princType == typeof(uint?) ||
                        princType == typeof(long?) ||
                        princType == typeof(ulong?) ||
                        princType == typeof(short?) ||
                        princType == typeof(ushort?) ||
                        princType == typeof(byte?) ||
                        princType == typeof(sbyte?) ? "int" :
                        princType == typeof(bool) ||
                        princType == typeof(bool?) ? "bool" :
                        princType == typeof(DateTime) ||
                        princType == typeof(DateTimeOffset) ||
                        princType == typeof(DateTime?) ||
                        princType == typeof(DateTimeOffset?) ? "datetime" :
                        new[]
                        {
                            "Point", "LineString", "Polygon", "MultiPoint", "MultiLineString", "MultiPolygon",
                            "GeometryCollection"
                        }.Contains(princType.Name) ? "geo" :
                        pAttr is ReversePredicateAttribute ? "uid" : "default";

                    var predicate = $"<{jattr.PropertyName}>: ";
                    predicate += isList ? "[{0}]" : "{0}";

                    switch (pAttr)
                    {
                        case ReversePredicateAttribute _ when propType == "uid":
                            propType = "uid";
                            predicate = string.Format(predicate, "uid");

                            predicate += isList ? " @reverse @count ." : " @reverse .";
                            break;
                        case StringPredicateAttribute sa when propType == "string":
                            propType = "string";
                            predicate = string.Format(predicate, "string");
                            if (sa.Fulltext || sa.Trigram || sa.Upsert || sa.Token != StringToken.None)
                            {
                                predicate += " @index(";
                                predicate += sa.Fulltext ? "fulltext" : "";
                                predicate += sa.Trigram
                                    ? predicate.Contains("fulltext") ? ",trigram" : "trigram"
                                    : "";

                                var tk = sa.Token switch
                                {
                                    StringToken.Exact => "exact",
                                    StringToken.Hash => "hash",
                                    StringToken.Term => "term",
                                    _ => ""
                                };

                                predicate += !string.IsNullOrEmpty(tk)
                                    ? predicate.Contains("fulltext") || predicate.Contains("trigram") ? $",{tk}" : tk
                                    : "";

                                predicate += sa.Lang ? ") @lang" : ")";
                                predicate += sa.Upsert ? " @upsert" : "";
                            }
                            else
                            {
                                predicate += sa.Lang ? " @lang" : "";
                            }

                            predicate += isList ? " @count ." : " .";
                            break;

                        case CommonPredicateAttribute pa:
                            predicate = string.Format(predicate, propType);
                            if (pa.Upsert || pa.Index)
                            {
                                predicate += $" @index({propType})";
                                predicate += pa.Upsert ? " @upsert" : "";
                            }

                            predicate += isList ? " @count ." : " .";
                            break;

                        case DateTimePredicateAttribute da:
                            predicate = string.Format(predicate, propType);
                            if (da.Upsert || da.Token != DateTimeToken.None)
                            {
                                if (da.Token == DateTimeToken.None)
                                    da.Token = DateTimeToken.Year;

                                predicate += $" @index({da.Token.ToString().ToLowerInvariant()})";
                                predicate += da.Upsert ? " @upsert" : "";
                            }

                            predicate += isList ? " @count ." : " .";
                            break;

                        case PasswordPredicateAttribute _ when propType == "string":
                            propType = "password";
                            predicate = string.Format(predicate, "password");

                            predicate += " .";
                            break;

                        default:
                            predicate = string.Format(predicate, propType);

                            predicate += isList ? " @count ." : " .";
                            break;
                    }

                    return (jattr.PropertyName, predicate, prop.DeclaringType?.GetCustomAttribute<DGraphTypeAttribute>().Name, propType, prop);
                }).Where(x => x.PropertyName != null);

            var ambiguous = triples.GroupBy(x => x.PropertyName)
                .Where(x => x.Select(y => y.predicate).Distinct().Count() > 1);

            var imps = ambiguous.Select(s => s.Key).ToArray();

            if (imps.Length > 1)
                throw new AmbiguousImplementationException($"There are two or more different implementations of: {string.Join(',', imps).Trim(',').Replace(",", ", ")}.");

            var sb = new StringBuilder();

            foreach (var (_, predicate, _, _, _) in triples)
                sb.AppendLine(predicate);

            sb.AppendLine();

            var ts =
            triples.GroupBy(triple => triple.Name)
                .Select(tp =>
                {
                    var typename = tp.Key;
                    var typeProperties = tp.Select(pred =>
                    {
                        var (propertyName, predicate, _, _, prop) = pred;
                        var nm = predicate.Contains("@reverse") ?
                            $"<~{propertyName}>" :
                            $"{propertyName}";

                        var r =
                        prop.GetCustomAttributes()
                            .Where(a => a is PredicateReferencesToAttribute)
                            .OfType<PredicateReferencesToAttribute>()
                            .FirstOrDefault();

                        if (r == null) return $"{nm}";

                        var tn = r.RefType.GetCustomAttribute<DGraphTypeAttribute>().Name;

                        if (predicate.Contains("["))
                            tn = $"[{tn}]";

                        return $"{nm}: {tn}";
                    });

                    return $@"type {typename} {{
{string.Join("\r\n", typeProperties.Select(s => $"  {s}"))}
}}";
                });

            foreach (var type in ts)
                sb.AppendLine(type);

            var schema = sb.ToString();

            var op = new Operation
            {
                DropAll = false,
                Schema = schema
            };

            Alter(op).GetAwaiter().GetResult();

            return sb;
        }

        private static string NormalizeName(string propName)
            => string.Join("", propName
                    .Select(c => char.IsUpper(c) ? $"_{char.ToLowerInvariant(c)}" : c.ToString()))
                .Trim('_');
    }
}
