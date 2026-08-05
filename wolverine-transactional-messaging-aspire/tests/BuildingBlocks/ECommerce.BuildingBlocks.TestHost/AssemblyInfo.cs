using Wolverine.Attributes;

// Marks this assembly for Wolverine handler discovery.
// The RabbitMQ building block does NOT forward `assemblies` to
// AddWolverineMessaging (unlike the Kafka one), so conventional routing
// relies on this attribute to find the test handlers.
[assembly: WolverineModule]
