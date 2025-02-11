using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Book_Management_System.Data;
using Book_Management_System.Services;
using Microsoft.EntityFrameworkCore;
using System.Text;

public class EmailBackgroundService : BackgroundService
{
    private readonly ILogger<EmailBackgroundService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;

    public EmailBackgroundService(ILogger<EmailBackgroundService> logger, IServiceScopeFactory scopeFactory, IConfiguration configuration)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Checking overdue books and sending emails...");

            try
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    var emailService = scope.ServiceProvider.GetRequiredService<EmailService>();

                    var dayCount = Convert.ToInt32(_configuration["FINE:DAYS_AGO"] ?? "2");
                    var finePerDay = Convert.ToDecimal(_configuration["FINE:FINE_AMOUNT"]);
                    var twoDaysAgo = DateTime.UtcNow.AddDays(-dayCount);

                   
                    var overdueUsers = await dbContext.UserBooks
                        .Where(ub => ub.ReservedAt <= twoDaysAgo && ub.Status == "Reserved")
                        .Include(ub => ub.User)
                        .Include(ub => ub.Book)
                        .GroupBy(ub => ub.User)
                        .Select(group => new
                        {
                            User = group.Key,
                            OverdueBooks = group.Select(ub => new
                            {
                                BookTitle = ub.Book.Name,
                                ReservationDate = ub.ReservedAt,
                                OverdueDays = (DateTime.UtcNow - ub.ReservedAt).Days - 2,
                                Fine = ((DateTime.UtcNow - ub.ReservedAt).Days - 2) * finePerDay
                            }).ToList()
                        })
                        .ToListAsync();

               if (overdueUsers.Any())
            {
                foreach (var user in overdueUsers)
                {
                    var emailBody = new StringBuilder();
                    emailBody.AppendLine("<h1 style='color: #2c3e50;'>Overdue Books Notice</h1>");
                    emailBody.AppendLine("<p style='font-size: 16px;'>The following books are overdue. Please return them as soon as possible.</p>");
                    emailBody.AppendLine("<table border='1' style='border-collapse: collapse; width: 100%;'>");
                    emailBody.AppendLine("<thead style='background-color: #3498db; color: white;'>");
                    emailBody.AppendLine("<tr><th style='padding: 10px;'>Book Name</th><th style='padding: 10px;'>Reservation Date</th><th style='padding: 10px;'>Overdue Days</th><th style='padding: 10px;'>Fine Per Day</th><th style='padding: 10px;'>Total Fine</th></tr>");
                    emailBody.AppendLine("</thead>");
                    emailBody.AppendLine("<tbody>");

                    decimal grandTotal = 0;

                    foreach (var book in user.OverdueBooks)
                    {
                        emailBody.AppendLine($"<tr style='background-color: #ecf0f1;'><td style='padding: 10px;'>{book.BookTitle}</td><td style='padding: 10px;'>{book.ReservationDate:yyyy-MM-dd}</td><td style='padding: 10px;'>{book.OverdueDays}</td><td style='padding: 10px;'>${finePerDay}</td><td style='padding: 10px;'>${book.Fine}</td></tr>");
                        grandTotal += book.Fine;
                    }

                    emailBody.AppendLine($"<tr style='background-color: #bdc3c7;'><td colspan='4' style='text-align:right; padding: 10px;'><strong>Grand Total</strong></td><td style='padding: 10px;'><strong>${grandTotal}</strong></td></tr>");
                    emailBody.AppendLine("</tbody>");
                    emailBody.AppendLine("</table>");

                    emailService.SendEmail(user.User.Email, "Overdue Books and Fine Details", emailBody.ToString());
                }

              
            }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while sending reminder emails.");
            }

            await Task.Delay(TimeSpan.FromHours(24), stoppingToken); 
        }
    }
}
