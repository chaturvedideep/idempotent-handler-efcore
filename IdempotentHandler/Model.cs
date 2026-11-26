using Microsoft.EntityFrameworkCore;

namespace IdempotentHandler;

public class Account
{
    public int Id { get; set; }
    public decimal Balance { get; set; }
}

public class ProcessedMessage
{
    public string MessageId { get; set; } = "";
    public DateTime ProcessedAtUtc { get; set; }
}

public record PaymentReceived(string MessageId, int AccountId, decimal Amount);

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<ProcessedMessage> ProcessedMessages => Set<ProcessedMessage>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        // The primary key is what makes the second delivery fail.
        b.Entity<ProcessedMessage>().HasKey(p => p.MessageId);
        b.Entity<Account>().Property(a => a.Balance).HasConversion<double>();
    }
}
