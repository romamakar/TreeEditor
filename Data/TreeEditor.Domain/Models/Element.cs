using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TreeEditor.Domain.Models
{
    public class Element
    {
        [Key]
        public int Id { get; set; }
        public int? ParentId { get; set; }
        [Required]
        public string Value { get; set; } = string.Empty;
        public bool IsDeleted { get; set; }

        [NotMapped]
        public List<Element> Children { get; set; } = new();
    }
}
