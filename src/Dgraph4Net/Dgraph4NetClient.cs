using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Api;

using Grpc.Core;

using Newtonsoft.Json;

using static Api.Dgraph;

namespace Api
{
    public sealed partial class Operation
    {
        public bool AlsoDropDgraphSchema { get; set; }
    }
}

namespace Dgraph4Net.SchemaReader
{
    internal class Schema
    {
        [JsonProperty("predicate")]
        internal string Predicate { get; set; }
    }

    internal class Field
    {
        [JsonProperty("name")]
        internal string Name { get; set; }
    }

    internal class Type
    {
        [JsonProperty("fields")]
        internal List<Field> Fields { get; set; }

        [JsonProperty("name")]
        internal string Name { get; set; }
    }

    internal class Root
    {
        [JsonProperty("schema")]
        internal List<Schema> Schema { get; set; }

        [JsonProperty("types")]
        internal List<Type> Types { get; set; }
    }
}

namespace Dgraph4Net
{
    public class Dgraph4NetClient : IDgraph4NetClient
    {
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly Mutex _mtx;
        private readonly DgraphClient[] _dgraphClients;
        private Jwt _jwt;

        private Dgraph4NetClient()
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
        public Dgraph4NetClient(params DgraphClient[] dgraphClients) : this() =>
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
        public Dgraph4NetClient(params ChannelBase[] channels) : this(channels.Select(channel => new DgraphClient(channel)).ToArray()) { }

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
            var dc = AnyClient();

            var co = GetOptions();

            var tries = 3;
            while (tries-- > 0)
            {
                try
                {
                    if (operation.DropAll && !operation.AlsoDropDgraphSchema)
                    {
                        await using var txn = NewTransaction();
                        var resp = await txn.Query("schema{name}");

                        var sr = JsonConvert.DeserializeObject<SchemaReader.Root>(resp.Json.ToStringUtf8());

                        var types = sr.Types.Select(x => x.Name).Where(x => !x.StartsWith("dgraph.")).ToList();
                        var predicates = sr.Schema.Select(x => x.Predicate).Where(x => !x.StartsWith("dgraph.")).ToList();

                        if (types.Count + predicates.Count == 0)
                        {
                            return new Payload
                            {
                                Data = Google.Protobuf.ByteString.CopyFromUtf8(string.Empty)
                            };
                        }

                        var payloads = await
                            Task.WhenAll(types.Distinct().Select(t => new Operation { DropOp = Operation.Types.DropOp.Type, DropValue = t, RunInBackground = true })
                                .Concat(predicates.Distinct().Select(p => new Operation { DropOp = Operation.Types.DropOp.Attr, DropValue = p, RunInBackground = true }))
                                .AsParallel().Select(Alter));

                        var result = payloads.Select(p => p.Data.ToStringUtf8()).Aggregate(new StringBuilder(), (sb, js) => sb.Append(',').Append(js), sb => $"[{sb}]");

                        return new Payload { Data = Google.Protobuf.ByteString.CopyFromUtf8(result) };
                    }

                    return await dc.AlterAsync(operation, co);
                }
                catch (RpcException err) when (err.Message.ToLowerInvariant().Contains("retry operation") && tries > 0)
                {
                    await Task.Delay(5000);
                    continue;
                }
                catch (RpcException err)
                {
                    if (!IsJwtExpired(err))
                        throw;

                    await RetryLogin().ConfigureAwait(false);
                    co = GetOptions();

                    return await dc.AlterAsync(operation, co);
                }
            }

            return null;
        }

        public Task Alter(string schema, bool dropAll = false) =>
            Alter(new Operation { DropAll = dropAll, Schema = schema });

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
            _dgraphClients.Length <= 1 ?
            _dgraphClients.FirstOrDefault() :
            _dgraphClients[new Random().Next(0, _dgraphClients.Length - 1)];

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
        internal static bool IsJwtExpired(RpcException error)
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
            new(this, readOnly, bestEffort, cancellationToken);

        public async Task<object> Alter(object operation) =>
            await Alter(operation as Operation);

        object IDgraph4NetClient.NewTransaction(bool readOnly, bool bestEffort, CancellationToken? cancellationToken) =>
            NewTransaction(readOnly, bestEffort, cancellationToken);
    }
}
