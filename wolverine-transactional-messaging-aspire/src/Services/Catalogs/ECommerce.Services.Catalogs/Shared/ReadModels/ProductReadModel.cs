using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ECommerce.Services.Catalogs.Shared.ReadModels;

public sealed record ProductReadModel(
    [property: BsonId, BsonGuidRepresentation(GuidRepresentation.Standard)] Guid Id,
    string Code,
    string Name,
    decimal Price,
    DateTime CreatedAtUtc,
    DateTime ProjectedAtUtc
);
