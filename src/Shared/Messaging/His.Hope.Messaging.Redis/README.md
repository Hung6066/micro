# His.Hope.Messaging.Redis

Durable Redis implementations of `IIdempotencyStore` and `IDurableJobStore`. The adapter uses atomic Redis commands for request ownership and a sorted-set queue for jobs. It is production-oriented and requires a configured shared `IConnectionMultiplexer`.
