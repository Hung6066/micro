# His.Hope.Messaging.RabbitMq

Durable `IMessagePublisher` adapter backed by the existing RabbitMQ connection implementation. Register it explicitly in a worker or service that owns event publication; outbox delivery remains responsible for retries and at-least-once semantics.
