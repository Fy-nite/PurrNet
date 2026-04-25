using System.ComponentModel.DataAnnotations;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Purrnet.Models
{
    public class Category
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

        [Required]
        public string Name { get; set; } = string.Empty;

        // Navigation — not persisted in MongoDB
        [BsonIgnore]
        public List<Package> Packages { get; set; } = new();
    }
}
