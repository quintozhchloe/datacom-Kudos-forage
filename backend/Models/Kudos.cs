using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Kudos.Api.Models;

public class Kudos
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    [BsonElement("toUserId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string ToUserId { get; set; } = string.Empty;

    [BsonElement("fromUserId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string FromUserId { get; set; } = string.Empty;

    [BsonElement("toUserName")]
    public string ToUserName { get; set; } = string.Empty;

    [BsonElement("fromUserName")]
    public string FromUserName { get; set; } = string.Empty;

    [BsonElement("toUserTeam")]
    public string ToUserTeam { get; set; } = string.Empty;

    [BsonElement("fromUserTeam")]
    public string FromUserTeam { get; set; } = string.Empty;

    [BsonElement("message")]
    public string Message { get; set; } = string.Empty;

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; }
}
