# idempotent-handler-efcore

A small sample of an idempotent message handler with EF Core. If the same message is delivered more than once, the data changes only once.

It is a made-up payments example, not a real production system.

## How it works

Every message has an id. The handler stores that id in a `ProcessedMessages` table where the id is the primary key, and applies the real change in the same transaction. A duplicate fails on the primary key and is skipped.

## Run it

You need the .NET 8 or .NET 10 SDK.

```
dotnet test IdempotentHandler.Tests -f net8.0
dotnet test IdempotentHandler.Tests -f net10.0
```

The tests check three things:

1. The same message delivered twice is applied once.
2. Ten parallel deliveries of the same message are applied once.
3. A crash after the id is claimed rolls back, so the retry still works.

## Not covered

Side effects outside the database (emails, calls to other services), cleaning up old ids, and database-specific duplicate-key errors. The sample uses SQLite as a stand-in.
