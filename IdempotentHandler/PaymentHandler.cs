using Microsoft.EntityFrameworkCore;

namespace IdempotentHandler;

public class PaymentHandler(IDbContextFactory<AppDbContext> factory)
{
    /// <returns>true if the message was applied, false if it was a duplicate.</returns>
    public async Task<bool> HandleAsync(PaymentReceived msg, CancellationToken ct = default,
        Action? failAfterMarking = null)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        // 1. Claim the message id. A second delivery hits the primary key and stops here.
        db.ProcessedMessages.Add(new ProcessedMessage { MessageId = msg.MessageId, ProcessedAtUtc = DateTime.UtcNow });
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return false;
        }

        // Test hook: lets a test simulate a crash after the id was claimed.
        failAfterMarking?.Invoke();

        // 2. Do the real work in the same transaction.
        var account = await db.Accounts.SingleAsync(a => a.Id == msg.AccountId, ct);
        account.Balance += msg.Amount;
        await db.SaveChangesAsync(ct);

        await tx.CommitAsync(ct);
        return true;
    }
}
