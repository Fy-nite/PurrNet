using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Purrnet.Models
{
    public class Category
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Required]
        public string Name { get; set; } = string.Empty;

        // Navigation
        [NotMapped]
        public List<Package> Packages { get; set; } = new();
    }
}
