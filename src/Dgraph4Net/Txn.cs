using System.Data;
using System.Transactions;

using Api;

using Grpc.Core;

using static Api.Dgraph;

namespace Dgraph4Net;

/// <summary>
/// Txn is a single atomic transaction.
/// </summary>
/// <remarks>
/// <para>
/// A transaction lifecycle is as follows:
/// <list type="number">
/// <item>Created using NewTxn.</item>
/// <item>Various Query and Mutate calls made.</item>
/// <item>Commit or Discard used. If any mutations have been made, It's important
/// that at least one of these methods is called to clean up resources. Discard
/// is a no-op if Commit has already been called, so it's safe to defer a call
/// to Discard immediately after NewTxn.</item>
/// </list>
/// </para>
/// </remarks>
public sealed class Txn : IAsyncDisposable, IDisposable
{
    /// <summary>
    /// Is returned when an operation is performed on already committed or discarded transaction.
    /// </summary>
    public static TransactionException ErrFinished =>
        new("Transaction has already been committed or discarded");

    /// <summary>
    /// Is returned when a write/update is performed on a readonly transaction.
    /// </summary>
    public static ReadOnlyException ErrReadOnly =>
        new("Readonly transaction cannot run mutations or be committed");

    /// <summary>
    /// Is returned when an operation is performed on an aborted transaction.
    /// </summary>
    public static TransactionAbortedException ErrAborted =>
        new("Transaction has been aborted. Please retry");

    private readonly bool _readOnly;

    private TxnContext _context;
    private Dgraph4NetClient _dgraph;
    private DgraphClient _dgraphClient;
    private bool _finished;
    private bool _mutated;
    private bool _bestEffort;
    private bool _disposed;

    private CancellationTokenSource _cancellationTokenSource;

    /// <summary>
    /// Creates a new transaction.
    /// </summary>
    /// <param name="Dgraph"></param>
    /// <param name="readOnly"></param>
    /// <param name="bestEffort"></param>
    /// <param name="cancellationToken"></param>
    /// <exception cref="InvalidOperationException">If best effort is true and the transaction is not read-only.</exception>
    public Txn(Dgraph4NetClient Dgraph, bool readOnly = false, bool bestEffort = false, CancellationToken? cancellationToken = null) : this(Dgraph, cancellationToken)
    {
        _dgraphClient = _dgraph.AnyClient();
        _context = new TxnContext();
        _readOnly = readOnly;
        if (bestEffort)
            BestEffort();
    }

    public Txn(Dgraph4NetClient Dgraph, CancellationToken? cancellationToken)
    {
        _dgraph = Dgraph;
        LinkTokens(Dgraph.GetTokenSource());

        if (cancellationToken is null)
            return;

        _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(_cancellationTokenSource.Token, cancellationToken.Value);
    }

    /// <summary>
    /// Link the cancellation tokens
    /// </summary>
    internal void LinkTokens(CancellationTokenSource _) =>
        _cancellationTokenSource = /*tokenSource ??*/ new CancellationTokenSource();

    /// <summary>
    /// Enables best effort in read-only queries.
    /// </summary>
    /// <remarks>
    /// This will ask the Dgraph Alpha
    /// to try to get timestamps from memory in a best effort to reduce the number of outbound
    /// requests to Zero. This may yield improved latencies in read-bound datasets.
    /// </remarks>
    /// <exception cref="InvalidOperationException">If the transaction is not read-only.</exception>
    /// <exception cref="ObjectDisposedException">If current context is disposed.</exception>
    /// <returns><see cref="Txn"/></returns>
    public Txn BestEffort()
    {
        ObjectDisposedException.ThrowIf(_disposed, typeof(Txn));

        if (!_readOnly)
            throw new InvalidOperationException("Best effort only works for read-only queries.");

        _bestEffort = true;
        return this;
    }

    /// <summary>
    /// Query sends a query to one of the connected Dgraph instances.
    /// </summary>
    /// <remarks>
    /// If no mutations need to be made in the same transaction, it's convenient to
    /// chain the method, e.g. NewTxn().Query(ctx, "...").</remarks>
    /// <param name="query"></param>
    /// <returns><see cref="Response"/></returns>
    /// <exception cref="RpcException">If the mutation fails, then the transaction is discarded and all future operations on it will fail.</exception>
    /// <exception cref="ErrAborted">When an operation is performed on an aborted transaction.</exception>
    /// <exception cref="ErrFinished">When an operation is performed on already committed or discarded transaction.</exception>
    /// <exception cref="ErrReadOnly">When a write/update is performed on a readonly transaction.</exception>
    /// <exception cref="ContextMarshalException">If current context StartTs is not equals to source StartTs.</exception>
    /// <exception cref="ObjectDisposedException">If current context is disposed.</exception>
    public Task<Response> Query(string query) =>
        QueryWithVars(query, null);

    /// <summary>
    /// Is like Query, but allows a variable map to be used.
    /// </summary>
    /// <remarks>This can provide safety against injection attacks.</remarks>
    /// <param name="query"></param>
    /// <param name="vars"></param>
    /// <returns><see cref="Response"/></returns>
    /// <exception cref="RpcException">If the mutation fails, then the transaction is discarded and all future operations on it will fail.</exception>
    /// <exception cref="ErrAborted">When an operation is performed on an aborted transaction.</exception>
    /// <exception cref="ErrFinished">When an operation is performed on already committed or discarded transaction.</exception>
    /// <exception cref="ErrReadOnly">When a write/update is performed on a readonly transaction.</exception>
    /// <exception cref="ContextMarshalException">If current context StartTs is not equals to source StartTs.</exception>
    /// <exception cref="ObjectDisposedException">If current context is disposed.</exception>
    public Task<Response> QueryWithVars(string query, Dictionary<string, string> vars)
    {
        var req = new Request
        {
            Query = query,
            ReadOnly = _readOnly,
            BestEffort = _bestEffort
        };

        if (vars is not null)
        {
            foreach (var v in vars)
                req.Vars.Add(v.Key, v.Value);
        }

        return Do(req);
    }

    // Mutate allows data stored on Dgraph instances to be modified.
    // 
    /// <summary>
    /// Allows data stored on Dgraph instances to be modified.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The fields in api.Mutation come in pairs, set and delete.
    /// Mutations can either be encoded as JSON or as RDFs.
    /// </para>
    /// <para>
    /// If CommitNow is set, then this call will result in the transaction
    /// being committed. In this case, an explicit call to Commit doesn't
    /// need to be made subsequently.
    /// </para>
    /// </remarks>
    /// <param name="mutation"></param>
    /// <returns><see cref="Response"/></returns>
    /// <exception cref="RpcException">If the mutation fails, then the transaction is discarded and all future operations on it will fail.</exception>
    /// <exception cref="ErrAborted">When an operation is performed on an aborted transaction.</exception>
    /// <exception cref="ErrFinished">When an operation is performed on already committed or discarded transaction.</exception>
    /// <exception cref="ErrReadOnly">When a write/update is performed on a readonly transaction.</exception>
    /// <exception cref="ContextMarshalException">If current context StartTs is not equals to source StartTs.</exception>
    /// <exception cref="ObjectDisposedException">If current context is disposed.</exception>
    public Task<Response> Mutate(Mutation mutation)
    {
        var req = new Request
        {
            CommitNow = mutation.CommitNow,
        };

        req.Mutations.Add(mutation);

        return Do(req);
    }

    /// <summary>
    /// Executes many queries followed by one or more than one mutations.
    /// </summary>
    /// <param name="requests"></param>
    /// <returns><see cref="Response"/></returns>
    /// <exception cref="RpcException">If the mutation fails, then the transaction is discarded and all future operations on it will fail.</exception>
    /// <exception cref="ErrAborted">When an operation is performed on an aborted transaction.</exception>
    /// <exception cref="ErrFinished">When an operation is performed on already committed or discarded transaction.</exception>
    /// <exception cref="ErrReadOnly">When a write/update is performed on a readonly transaction.</exception>
    /// <exception cref="ContextMarshalException">If current context StartTs is not equals to source StartTs.</exception>
    /// <exception cref="ObjectDisposedException">If current context is disposed.</exception>
    public async Task<IEnumerable<Response>> Do(IEnumerable<Request> requests)
    {
        ObjectDisposedException.ThrowIf(_disposed, typeof(Txn));

        if (_finished)
            throw ErrFinished;

        var reqs = requests as Request[] ?? requests.ToArray();
        if (reqs.Length == 0)
        {
            _finished = true;
            return [new Response { Txn = _context, Latency = new Latency() }];
        }

        if (reqs.Any(x => x.CommitNow))
        {
            if (_readOnly)
                throw ErrReadOnly;

            foreach (var req in reqs)
                req.CommitNow = true;

            _mutated = true;
        }

        var co = _dgraph.GetOptions();

        foreach (var req in reqs.Where(r => r.StartTs == 0))
            req.StartTs = _context.StartTs;

        var responses = await Task.WhenAll(reqs.Select(async request =>
        {
            Response resp = null;

            try
            {
                resp = await _dgraphClient.QueryAsync(request, co.Headers, cancellationToken: _cancellationTokenSource.Token);

                if (resp.Uids is not null)
                    TxnExtensions.Resolve(resp.Uids);
            }
            catch (RpcException err) when (Dgraph4NetClient.IsJwtExpired(err))
            {
                await _dgraph.RetryLogin().ConfigureAwait(false);

                resp = await _dgraphClient.QueryAsync(request, co.Headers, cancellationToken: _cancellationTokenSource.Token);

                if (resp.Uids is not null)
                    TxnExtensions.Resolve(resp.Uids);
            }
            catch
            {
                _finished = false;
                try
                {
                    await Abort(resp?.Txn, request, true).ConfigureAwait(false);
                }
                catch
                {
                    // ignore
                }
                throw;
            }
            finally
            {
                _finished = false;
            }

            if (reqs.All(r => r.CommitNow))
            {
                MergeContext(resp?.Txn ?? _context);
            }
            try
            {
                await Abort(resp?.Txn, request).ConfigureAwait(false);
            }
            catch
            {
                // ignore
            }
            return resp;
        })).ConfigureAwait(false);

        if (reqs.All(r => r.CommitNow))
            _finished = true;

        return responses;
    }

    /// <summary>
    /// Executes a query followed by one or more than one mutations.
    /// </summary>
    /// <param name="request"></param>
    /// <returns><see cref="Response"/></returns>
    /// <exception cref="RpcException">If the mutation fails, then the transaction is discarded and all future operations on it will fail.</exception>
    /// <exception cref="ErrAborted">When an operation is performed on an aborted transaction.</exception>
    /// <exception cref="ErrFinished">When an operation is performed on already committed or discarded transaction.</exception>
    /// <exception cref="ErrReadOnly">When a write/update is performed on a readonly transaction.</exception>
    /// <exception cref="ContextMarshalException">If current context StartTs is not equals to source StartTs.</exception>
    /// <exception cref="ObjectDisposedException">If current context is disposed.</exception>
    public async Task<Response> Do(Request request)
    {
        var responses = await Do([request])
            .ConfigureAwait(false);

        return responses.First();
    }

    public Task Abort(Request req)
    {
        _context.Aborted = true;
        return Abort(_context, req, true);
    }

    private async Task Abort(TxnContext txn, Request request, bool force = false)
    {
        var finished = _finished;
        try
        {
            await Discard(txn ?? _context, request).ConfigureAwait(false);
        }
        catch (RpcException err)
        {
            if (err.StatusCode == StatusCode.Aborted)
                throw ErrAborted;
            throw;
        }
        finally
        {
            if (force)
                _context.Aborted = true;

            if (request.Mutations.Count == 0 || request.Mutations.All(x => !x.CommitNow))
                _finished = finished;
        }
    }

    /// <summary>
    /// Discard cleans up the resources associated with an uncommitted transaction that contains mutations.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It is a no-op on transactions that have already
    /// been committed or don't contain mutations. Therefore, it is safe (and recommended)
    /// to call as a deferred function immediately after a new transaction is created.
    /// </para>
    /// <para>
    /// In some cases, the transaction can't be discarded, e.g. the grpc connection
    /// is unavailable. In these cases, the server will eventually do the
    /// transaction clean up itself without any intervention from the client.
    /// </para>
    /// </remarks>
    /// <exception cref="RpcException">If the mutation fails, then the transaction is discarded and all future operations on it will fail.</exception>
    /// <exception cref="ErrAborted">When an operation is performed on an aborted transaction.</exception>
    /// <exception cref="ErrFinished">When an operation is performed on already committed or discarded transaction.</exception>
    /// <exception cref="ErrReadOnly">When a write/update is performed on a readonly transaction.</exception>
    /// <exception cref="ObjectDisposedException">If current context is disposed.</exception>
    public Task Discard(TxnContext txn, Request request)
    {
        ObjectDisposedException.ThrowIf(_disposed, typeof(Txn));

        if (txn is not null)
            _context = txn;

        if (_context.StartTs != 0 && request.StartTs == 0)
            request.StartTs = _context.StartTs;

        return request.CommitNow ? CommitOrAbort() : Task.CompletedTask;
    }

    /// <summary>
    /// Commit commits any mutations that have been made in the transaction.
    /// </summary>
    /// <remarks>
    /// <para>Once Commit has been called, the lifespan of the transaction is complete.</para>
    /// <para>
    /// Errors could be returned for various reasons. Notably, ErrAborted could be
    /// returned if transactions that modify the same data are being run concurrently.
    /// It's up to the user to decide if they wish to retry.
    /// In this case, the user should create a new transaction.
    /// </para>
    /// </remarks>
    /// <exception cref="RpcException">If the mutation fails, then the transaction is discarded and all future operations on it will fail.</exception>
    /// <exception cref="ErrAborted">When an operation is performed on an aborted transaction.</exception>
    /// <exception cref="ErrFinished">When an operation is performed on already committed or discarded transaction.</exception>
    /// <exception cref="ErrReadOnly">When a write/update is performed on a readonly transaction.</exception>
    /// <exception cref="ObjectDisposedException">If current context is disposed.</exception>
    public async Task Commit()
    {
        ObjectDisposedException.ThrowIf(_disposed, typeof(Txn));

        if (_readOnly)
            throw ErrReadOnly;

        if (_finished)
            throw ErrFinished;

        if (_context.Aborted)
            throw ErrAborted;

        try
        {
            await CommitOrAbort().ConfigureAwait(false);
        }
        catch (RpcException err)
        {
            if (err.StatusCode == StatusCode.Aborted)
                throw ErrAborted;
            throw;
        }
    }

    /// <summary>
    /// Merges the provided Transaction Context into the current one.
    /// </summary>
    /// <param name="src"></param>
    /// <exception cref="ContextMarshalException">If current context StartTs is not equals to source StartTs.</exception>
    /// <exception cref="ObjectDisposedException">If current context is disposed.</exception>
    private void MergeContext(TxnContext src)
    {
        ObjectDisposedException.ThrowIf(_disposed, typeof(Txn));

        if (src is null)
            return;

        if (_context.StartTs == 0)
            _context.StartTs = src.StartTs;

        if (_context.StartTs != src.StartTs)
            throw new ContextMarshalException("StartTs mismatch.");

        _context.Keys.AddRange([.. src.Keys]);

        _context.Preds.AddRange([.. src.Preds]);
    }

    /// <summary>
    /// Commit or abort transaction
    /// </summary>
    /// <exception cref="RpcException">If the mutation fails, then the transaction is discarded and all future operations on it will fail.</exception>
    /// <exception cref="ErrAborted">When an operation is performed on an aborted transaction.</exception>
    /// <exception cref="ErrFinished">When an operation is performed on already committed or discarded transaction.</exception>
    /// <exception cref="ErrReadOnly">When a write/update is performed on a readonly transaction.</exception>
    /// <exception cref="ObjectDisposedException">If current context is disposed.</exception>
    private async Task CommitOrAbort()
    {
        ObjectDisposedException.ThrowIf(_disposed, typeof(Txn));

        if (_finished)
            return;

        if (!_mutated)
        {
            _finished = true;
            return;
        }

        try
        {
            var co = _dgraph.GetOptions();
            var ctx = await _dgraphClient.CommitOrAbortAsync(_context, co.Headers, cancellationToken: _cancellationTokenSource.Token);
            _context.Aborted = ctx.Aborted;

            _finished = true;
        }
        catch (RpcException err) when (Dgraph4NetClient.IsJwtExpired(err))
        {
            await _dgraph.RetryLogin().ConfigureAwait(false);

            var co = _dgraph.GetOptions();
            var ctx = await _dgraphClient.CommitOrAbortAsync(_context, co.Headers, cancellationToken: _cancellationTokenSource.Token);
            _context.Aborted = ctx.Aborted;

            _finished = true;
            throw;
        }
    }

    #region IDisposable Support
    private void Dispose(bool disposing)
    {
        try
        {
            if (_disposed)
                return;

            _disposed = true;
            _finished = true;

            if (disposing)
            {
                _context = null;
                _dgraph = null;
                _dgraphClient = null;
                _cancellationTokenSource?.Cancel(false);
            }

            _cancellationTokenSource?.Dispose();
        }
        catch
        {
            // ignore
        }
    }

    ~Txn()
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

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        var tsk = new ValueTask(Task.Run(delegate
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }));

        return tsk;
    }
    #endregion
}
