using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Overmind.Infrastructure.Tests;

// The API trigger tests fault a write statement; this faults after every statement succeeded
// but before the commit closed. The committing hook lives on DbTransactionInterceptor — the
// connection interceptor exposes none.
public sealed class CommitFaultInterceptor(string marker) : DbTransactionInterceptor
{
    public int CommitAttempts { get; private set; }

    public override InterceptionResult TransactionCommitting(
        DbTransaction transaction, TransactionEventData eventData, InterceptionResult result)
    {
        Fail();
        return result;
    }

    public override ValueTask<InterceptionResult> TransactionCommittingAsync(
        DbTransaction transaction,
        TransactionEventData eventData,
        InterceptionResult result,
        CancellationToken cancellationToken)
    {
        Fail();
        return ValueTask.FromResult(result);
    }

    private void Fail()
    {
        CommitAttempts++;
        throw new InvalidOperationException(marker);
    }
}
