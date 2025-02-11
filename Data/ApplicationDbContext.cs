using Book_Management_System.Models.Entities;
using Book_Management_System.Models.ViewModels;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace Book_Management_System.Data
{
    public class ApplicationDbContext:DbContext
    {
        internal readonly object BookReservationReportViewModel;
        private string? _connectionString;

        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options , IConfiguration configuration):base(options){

            _connectionString = configuration.GetConnectionString("BookManagementSystem");

        }

        public DbSet<User> Users { get; set; }
        public DbSet<Book> Books { get; set; }
        public DbSet<UserBook> UserBooks { get; set; }
        public DbSet<PaymentRecord> PaymentRecords { get; set; }


        private IDbConnection CreateConnection()
        {
            return new SqlConnection(_connectionString);
        }



        public async Task<int> GetReservedBooksCount(DateTime? startDate, DateTime? endDate)
        {
            var query = from ub in UserBooks
                        join b in Books on ub.BookId equals b.Id
                        where ub.Status == "Reserved" &&
                              (startDate == null || EF.Functions.DateDiffDay(startDate, ub.ReservedAt) <= 0) &&
                              (endDate == null || EF.Functions.DateDiffDay(ub.ReservedAt, endDate) <= 0)
                        select ub.BookId;

            return await query.Distinct().CountAsync();
        }

       
        public async Task<IEnumerable<BookReservationReportViewModel>> GetReservedBooksReport(DateTime? startDate, DateTime? endDate, string sortOrder, int pageNumber, int pageSize)
        {
            var offset = (pageNumber - 1) * pageSize;

            var query = from ub in UserBooks
                        join b in Books on ub.BookId equals b.Id
                        where ub.Status == "Reserved" &&
                              (startDate == null || EF.Functions.DateDiffDay(startDate, ub.ReservedAt) <= 0) &&
                              (endDate == null || EF.Functions.DateDiffDay(ub.ReservedAt, endDate) <= 0)
                        group ub by new { b.Name, b.Author } into g
                        orderby sortOrder == "ASC" ? g.Count() : -g.Count()
                        select new BookReservationReportViewModel
                        {
                            BookTitle = g.Key.Name,
                            Author = g.Key.Author,
                            TotalReservations = g.Count(),
                            LastReservedDate = g.Max(ub => ub.ReservedAt)
                        };

            return await query.Skip(offset).Take(pageSize).ToListAsync();
        }
    
    public async Task<List<UserReportViewModel>> GetActiveUsers(DateTime? startDate, DateTime? endDate, int pageNumber = 1,
    int pageSize = 3)
        {
            using var connection = CreateConnection();

            var parameters = new DynamicParameters();
            parameters.Add("@StartDate", startDate, DbType.DateTime);
            parameters.Add("@EndDate", endDate, DbType.DateTime);
            parameters.Add("@PageNumber", pageNumber, DbType.Int32);
            parameters.Add("@PageSize", pageSize, DbType.Int32);

            var result = await connection.QueryAsync<UserReportViewModel>(
                "ActiveUsersReport",
                parameters,
                commandType: CommandType.StoredProcedure
            );

            return result.ToList();
        }

        public async Task<List<UserReportViewModel>> GetInActiveUsers(DateTime? startDate, DateTime? endDate, int pageNumber = 1,
    int pageSize = 3)
        {
            using var connection = CreateConnection();

            var parameters = new DynamicParameters();
            parameters.Add("@StartDate", startDate, DbType.DateTime);
            parameters.Add("@EndDate", endDate, DbType.DateTime);
            parameters.Add("@PageNumber", pageNumber, DbType.Int32);
            parameters.Add("@PageSize", pageSize, DbType.Int32);

            var result = await connection.QueryAsync<UserReportViewModel>(
                "InactiveUsersReport",
                parameters,
                commandType: CommandType.StoredProcedure
            );

            return result.ToList();
        }

        public async Task<List<FineViewModel>> GetFineSummary(DateTime? startDate, DateTime? endDate, decimal fineAmount, int daysAgo, int pageNumber = 1,
    int pageSize = 3)
        {
            using var connection = CreateConnection();

            var parameters = new DynamicParameters();
            parameters.Add("@StartDate", startDate, DbType.DateTime);
            parameters.Add("@EndDate", endDate, DbType.DateTime);
            parameters.Add("@FineAmount", fineAmount, DbType.Decimal);
            parameters.Add("@DaysAgo", daysAgo, DbType.Int32);
            parameters.Add("@PageNumber", pageNumber, DbType.Int32);
            parameters.Add("@PageSize", pageSize, DbType.Int32);
            var result = await connection.QueryAsync<FineViewModel>(
                "FineSummaryReports",
                parameters,
                commandType: CommandType.StoredProcedure
            );

            return result.ToList();
        }





    }
}
