using System.Data;
using System.Threading.Tasks;
using System.Transactions;

using Api;

using Google.Protobuf;
using Xunit;
using Assert = Dgraph4Net.Tests.Assert;

namespace Dgraph4Net.Tests
{
    [Collection("Dgraph4Net")]
    public class ErrorsTest : ExamplesTest
    {
        [Fact(DisplayName = "should have returned ErrFinished")]
        public async Task TestTxnErrFinished()
        {
            await using var dg = GetDgraphClient();

            var op = new Operation
            {
                Schema = "email: string @index(exact) .\n"
            };

            await dg.Alter(op);

            var txn = new Txn(dg);

            var mu = new Mutation
            {
                SetNquads = ByteString.CopyFromUtf8("_:user1 <email> \"user1@company1.io\"."),
                CommitNow = true
            };

            await txn.Mutate(mu);

            await ThrowsAsync<TransactionException>(() => txn.Mutate(mu));
        }

        [Fact(DisplayName = "should have returned ErrReadOnly")]
        public async Task TestTxnErrReadOnly()
        {
            await using var dg = GetDgraphClient();

            var op = new Operation { Schema = "email: string @index(exact) ." };
            await dg.Alter(op);

            var mu = new Mutation
            {
                SetNquads = ByteString.CopyFromUtf8("_:user1 <email> \"user1@company1.io\"."),
                CommitNow = true
            };

            await ThrowsAsync<ReadOnlyException>(() => dg.NewTransaction(true).Mutate(mu));
        }

        [Fact(DisplayName = "2nd transaction should have aborted")]
        public async Task TestTxnErrAborted()
        {
            await using var dg = GetDgraphClient();

            var op = new Operation { Schema = "email: string @index(exact) ." };
            await dg.Alter(op);

            var mu = new Mutation
            {
                SetNquads = ByteString.CopyFromUtf8("_:user1 <email> \"user1@company1.io\"."),
                CommitNow = true
            };

            // Insert first record.
            await dg.NewTransaction().Mutate(mu);

            const string q = @"{
                v as var(func: eq(email, ""user1@company1.io""))
            }";

            mu = new Mutation
            {
                SetNquads = ByteString.CopyFromUtf8(@"uid(v) <email> ""updated1@company1.io""."),
                CommitNow = false
            };

            var txn = dg.NewTransaction();

            var req = new Request { Query = q };

            req.Mutations.Add(mu);

            await txn.Do(req);

            await txn.Abort(req);

            await ThrowsAsync<TransactionAbortedException>(() => txn.Commit());
        }
    }
}
