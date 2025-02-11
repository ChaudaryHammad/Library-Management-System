using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using static Book_Management_System.Models.Entities.Enums;

namespace Book_Management_System.Models.Entities
{
    public class User
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        [Required]
        [StringLength(100)]
       public string FirstName { get; set; }
    
        public string LastName { get; set; }

        [Required]
        [EmailAddress]
        [StringLength(150)]
        public string Email { get; set; }

        [Required]
        [DataType(DataType.Password)]
        [StringLength(255, MinimumLength = 5)]
        public string Password { get; set; }

        [Required]
        public DateTime Dob { get; set; }

      
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
        public string RecordStatus { get; set; }
        public string Role { get; set; }

        [NotMapped] 
        public IFormFile ImageFile { get; set; }

        public string? ImagePath { get; set; }
        public ICollection<UserBook> UserBooks { get; set; } = new List<UserBook>();
        public ICollection<PaymentRecord> PaymentRecords { get; set; } = new List<PaymentRecord>();

    }
}
