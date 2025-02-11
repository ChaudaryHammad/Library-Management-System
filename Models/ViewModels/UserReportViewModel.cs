namespace Book_Management_System.Models.ViewModels
{
    public class UserReportViewModel
    {
        public string FirstName { get; set; }

        public int TotalBooksReserved { get; set; }

        public string? ReservedBooks { get; set; }  
        
        public int? TotalPages { get; set; }
        public DateTime? LastReservedDate { get; set; }
    }
}
