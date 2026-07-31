// using System.Reflection;
// using BuildingBlocks.Integration.Wolverine.Configuration;
//
// namespace BuildingBlocks.Integration.Wolverine.Kafka.Configuration;
//
// public sealed class WolverineKafkaRegistrationOptions
// {
//     public required WolverineCommonOptions Common { get; init; }
//
//     public required WolverineKafkaOptions Kafka { get; init; }
//
//     /// <summary>
//     /// Assemblies to scan for <c>IIntegrationEvent</c> types — used when
//     /// <see cref="WolverineBusOptions.AutoConfigMessagesTopology"/> is <c>true</c>.
//     /// Referenced assemblies are also resolved transitively.
//     /// </summary>
//     public IReadOnlyCollection<Assembly> Assemblies { get; init; } = Array.Empty<Assembly>();
// }
