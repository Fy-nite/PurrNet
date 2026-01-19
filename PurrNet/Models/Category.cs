using System.ComponentModel.DataAnnotations;

namespace Purrnet.Models
{
    public class Category
    {
        public int Id { get; set; }

        [Required]
        public string Name { get; set; } = string.Empty;

        // Navigation
        public List<Package> Packages { get; set; } = new();
    }
}
