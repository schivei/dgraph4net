using System;
using System.Data;
using System.Threading.Tasks;
using System.Transactions;
using DGraph4Net.Services;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Xunit;

namespace DGraph4Net.Tests
{
    public class ErrorsTest : ExamplesTest
    {
        [Fact(DisplayName = "should have returned ErrFinished")]
        public async Task TestTxnErrFinished()
        {
            using var dg = GetDgraphClient();

            Txn txn = null;

            var mu = new Mutation
            {
                SetNquads = ByteString.CopyFromUtf8("_:user1 <email> \"user1@company1.io\"."),
                CommitNow = true
            };

            await dg.Alter(new Operation { DropAll = true });

            var op = new Operation
            {
                Schema = "email: string @index(exact) .\n"
            };

            await dg.Alter(op);

            txn = new Txn(dg);

            await txn.Mutate(mu);

            await Assert.ThrowsAsync<TransactionException>(() => txn.Mutate(mu));
        }

        [Fact(DisplayName = "should have returned ErrReadOnly")]
        public async Task TestTxnErrReadOnly()
        {
            using var dg = GetDgraphClient();

            await dg.Alter(new Operation { DropAll = true });

            var op = new Operation { Schema = "email: string @index(exact) ." };
            await dg.Alter(op);

            var mu = new Mutation
            {
                SetNquads = ByteString.CopyFromUtf8("_:user1 <email> \"user1@company1.io\"."),
                CommitNow = true
            };

            await Assert.ThrowsAsync<ReadOnlyException>(() => dg.NewTransaction(true).Mutate(mu));
        }

        [Fact(DisplayName = "2nd transaction should have aborted")]
        public async Task TestTxnErrAborted()
        {
            using var dg = GetDgraphClient();

            await dg.Alter(new Operation { DropAll = true });

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

            var tx1 = dg.NewTransaction();

            var tx2 = dg.NewTransaction();

            var req = new Request { Query = q };

            req.Mutations.Add(mu);

            await tx1.Do(req);

            await tx2.Do(req);

            await tx1.Commit();

            await Assert.ThrowsAsync<TransactionAbortedException>(() => tx2.Commit());
        }
    }
}
