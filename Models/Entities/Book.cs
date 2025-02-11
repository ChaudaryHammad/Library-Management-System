using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using static Book_Management_System.Models.Entities.Enums;

namespace Book_Management_System.Models.Entities
{
  
    public class Book
    {

        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required(ErrorMessage = "Name is required.")]
        public string Name { get; set; }
        public string Description { get; set; }
        

        [Required(ErrorMessage = "Author is required.")]
        public string Author { get; set; }

        [Required(ErrorMessage = "Price is required.")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Price must be greater than 0.")]
        public decimal Price { get; set; }


        [Required(ErrorMessage = "Rating is required.")]
        [Range(1, 10, ErrorMessage = "Rating must be between 1 and 10.")]
        public int Rating { get; set; }

        [Required(ErrorMessage = "Public date is required.")]
        public DateTime BookPublichedAt { get; set; }

        [Required(ErrorMessage = "Added date is required.")]
        public DateTime BookAddedAt { get; set; } = DateTime.UtcNow;
        public string Status { get; set; }
       
        [NotMapped]
        public IFormFile ImageFile { get; set; }
        public string? ImagePath { get; set; }

        public string? PdfFilePath { get; set; }

        [NotMapped]
        public IFormFile PdfFile { get; set; }

        public string? QrCodePath { get; set; }

        public ICollection<UserBook> UserBooks { get; set; } = new List<UserBook>();

    }
}
