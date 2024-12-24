using System.Threading.Tasks;
using System.Transactions;
using Api;
using Google.Protobuf;
using Grpc.Core;

// generate tests to full coverage of Dgraph4Net.Core
namespace Dgraph4Net.Tests;

public class CoreTests : ExamplesTest
{
    [Fact(DisplayName = "should have returned ErrFinished")]
    public async Task TestTxnErrFinished()
    {
        var dg = GetDgraphClient();

        try
        {
            var op = new Operation
            {
                Schema = "email: string @index(exact) .\n"
            };

            await dg.Alter(op);

            var txn = new Txn(dg);

            var mu = new Mutation
            {
                SetNquads = ByteString.CopyFromUtf8("""_:user1 <email> "test@test.com" """)
            };

            await Assert.ThrowsAsync<RpcException>(() => txn.Mutate(mu));

            await Assert.ThrowsAsync<TransactionAbortedException>(() => txn.Commit());
        }
        finally
        {
            await ClearDb();
        }
    }
}
