namespace Book_Management_System.Models.ViewModels
{
    public class BookReservationReportViewModel
    {
        public string BookTitle { get; set; }
        public string Author { get; set; }
        public int TotalReservations { get; set; }
        public DateTime LastReservedDate { get; set; }
    }
}
