using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Api;

using Grpc.Core;

using static Api.Dgraph;

namespace Dgraph4Net
{
    public class Dgraph4NetClient : IDgraph4NetClient
    {
        private CancellationTokenSource _cancellationTokenSource;
        private Mutex _mtx;
        private DgraphClient[] _dgraphClients;
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

            try
            {
                return await dc.AlterAsync(operation, co);
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

        public Task Alter(string schema, bool dropAll = false) =>
            Alter(new Operation { /*DropAll = dropAll,*/ Schema = schema });

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
    }
}
