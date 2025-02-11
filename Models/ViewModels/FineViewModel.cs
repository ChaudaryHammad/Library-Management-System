namespace Book_Management_System.Models.ViewModels
{
    public class FineViewModel
    {
        public string UserName { get; set; }
        public string BookTitle { get; set; }

        public decimal FineAmount { get; set; }

        public int? TotalPages  { get; set; }
        public string PaymentStatus { get; set; }
    }
}
