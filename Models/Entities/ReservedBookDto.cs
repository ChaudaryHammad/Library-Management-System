using static Book_Management_System.Models.Entities.Enums;

namespace Book_Management_System.Models.Entities
{
    public class ReservedBookDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Author { get; set; }
        public decimal Price { get; set; }
        public int Rating { get; set; }
        public DateTime BookPublichedAt { get; set; }
        public DateTime BookAddedAt { get; set; }
        public string Status { get; set; }
        public DateTime ReservedAt { get; set; }
    }
}
