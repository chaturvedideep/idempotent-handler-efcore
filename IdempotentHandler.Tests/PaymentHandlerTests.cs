using Xunit;
using IdempotentHandler;
using Microsoft.EntityFrameworkCore;

namespace IdempotentHandler.Tests;

public class PaymentHandlerTests : IDisposable
{
    private readonly string _file = Path.Combine(Path.GetTempPath(), $"idem-{Guid.NewGuid():N}.db");
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly PaymentHandler _handler;

    public PaymentHandlerTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={_file}").Options;
        _factory = new TestFactory(options);
        using var db = _factory.CreateDbContext();
        db.Database.EnsureCreated();
        db.Accounts.Add(new Account { Id = 1, Balance = 100m });
        db.SaveChanges();
        _handler = new PaymentHandler(_factory);
    }

    private decimal Balance()
    {
        using var db = _factory.CreateDbContext();
        return db.Accounts.Single().Balance;
    }

    [Fact]
    public async Task Same_message_delivered_twice_is_applied_once()
    {
        var msg = new PaymentReceived("pay-001", 1, 25m);

        Assert.True(await _handler.HandleAsync(msg));
        Assert.False(await _handler.HandleAsync(msg));

        Assert.Equal(125m, Balance());
    }

    [Fact]
    public async Task Ten_parallel_deliveries_of_the_same_message_apply_once()
    {
        var msg = new PaymentReceived("pay-002", 1, 10m);

        var results = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => Task.Run(() => _handler.HandleAsync(msg))));

        Assert.Equal(1, results.Count(r => r));
        Assert.Equal(110m, Balance());
    }

    [Fact]
    public async Task A_crash_after_claiming_the_id_does_not_block_the_retry()
    {
        var msg = new PaymentReceived("pay-003", 1, 40m);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _handler.HandleAsync(msg, failAfterMarking: () => throw new InvalidOperationException("boom")));
        Assert.Equal(100m, Balance());

        Assert.True(await _handler.HandleAsync(msg));
        Assert.Equal(140m, Balance());
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (File.Exists(_file)) File.Delete(_file);
    }

    private class TestFactory(DbContextOptions<AppDbContext> o) : IDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext() => new(o);
    }
}
