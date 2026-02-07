using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Kudos.Api.Models;

public class User
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;

    [BsonElement("team")]
    public string Team { get; set; } = string.Empty;

    [BsonElement("externalId")]
    public string ExternalId { get; set; } = string.Empty;
}
