using Book_Management_System.Data;
using Book_Management_System.Models.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Book_Management_System.Models.ViewModels;
using System.Data;
using iTextSharp.text;
using System.Drawing.Printing;
namespace Book_Management_System.Controllers
{
    public class ReportController : Controller
    {
        private IConfiguration _configuration;
        private ApplicationDbContext _context;

        public ReportController( ApplicationDbContext context, IConfiguration configuration)
        {
            _configuration = configuration;
            _context = context;
            
        }

        public async Task<IActionResult> MostReservedBooks(DateTime? startDate, DateTime? endDate, int pageNumber = 1, int pageSize = 3)
        {
            var userId = HttpContext.Session.GetInt32("UserId");

            if (userId == null)
            {
                TempData["ErrorMessage"] = "Unauthorized.";
                return RedirectToAction("Login", "Auth");
            }

            var userRole = HttpContext.Session.GetString("UserRole");
            if (userRole == "Admin")
            {
                ViewBag.UserName = HttpContext.Session.GetString("UserName");
                ViewBag.UserEmail = HttpContext.Session.GetString("UserEmail");
                ViewBag.ProfileImage = HttpContext.Session.GetString("ProfileImage");
                ViewBag.UserRole = userRole;

                try
                {
                    var report = await _context.GetReservedBooksReport(startDate, endDate, "DESC", pageNumber, pageSize);
                    var totalCount = await _context.GetReservedBooksCount(startDate, endDate);
                    var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

                    ViewBag.PageNumber = pageNumber;
                    ViewBag.PageSize = pageSize;
                    ViewBag.TotalPages = totalPages;

                    return View(report);
                }
                catch (Exception ex)
                {
                    TempData["ErrorMessage"] = $"Error generating report: {ex.Message}";
                    return RedirectToAction("Index", "Book");
                }
            }
            else
            {
                TempData["ErrorMessage"] = "Unauthorized";
                return RedirectToAction("Index", "Book");
            }
        }


        public async Task<IActionResult> LeastReservedBooks(DateTime? startDate, DateTime? endDate, string? sortOrder, int pageNumber = 1, int pageSize = 3)
        {
            var userId = HttpContext.Session.GetInt32("UserId");

            if (userId == null)
            {
                TempData["ErrorMessage"] = "Unauthorized.";
                return RedirectToAction("Login", "Auth");
            }

            var userRole = HttpContext.Session.GetString("UserRole");
            if (userRole == "Admin")
            {
                ViewBag.UserName = HttpContext.Session.GetString("UserName");
                ViewBag.UserEmail = HttpContext.Session.GetString("UserEmail");
                ViewBag.ProfileImage = HttpContext.Session.GetString("ProfileImage");
                ViewBag.UserRole = userRole;

                try
                {
                    // Fetch the report with ASC sort order (least reserved books)
                    var report = await _context.GetReservedBooksReport(startDate, endDate, sortOrder: "ASC", pageNumber, pageSize);

                    // Get the total count of reserved books for pagination
                    var totalCount = await _context.GetReservedBooksCount(startDate, endDate);
                    var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

                    ViewBag.PageNumber = pageNumber;
                    ViewBag.PageSize = pageSize;
                    ViewBag.TotalPages = totalPages;

                    return View(report);
                }
                catch (Exception ex)
                {
                    TempData["ErrorMessage"] = $"Error generating report: {ex.Message}";
                    return RedirectToAction("Index", "Book");
                }
            }
            else
            {
                TempData["ErrorMessage"] = "Unauthorized";
                return RedirectToAction("Index", "Book");
            }
        }


        public async Task<IActionResult> ActiveUsers(DateTime? startDate, DateTime? endDate, int pageNumber = 1, int pageSize = 2)
        {
            var userId = HttpContext.Session.GetInt32("UserId");

            if (userId == null)
            {
                TempData["ErrorMessage"] = "Unauthorized.";
                return RedirectToAction("Login", "Auth");
            }

            var userRole = HttpContext.Session.GetString("UserRole");
            if (userRole == "Admin")
            {
                ViewBag.UserName = HttpContext.Session.GetString("UserName");
                ViewBag.UserEmail = HttpContext.Session.GetString("UserEmail");
                ViewBag.ProfileImage = HttpContext.Session.GetString("ProfileImage");
                ViewBag.UserRole = userRole;

                try
                {
                    startDate ??= DateTime.Now.AddDays(-30);  // Default to 30 days ago
                    endDate ??= DateTime.Now;  // Default to today

                    var report = await _context.GetActiveUsers(startDate, endDate, pageNumber, pageSize);
                    ViewBag.PageNumber = pageNumber;
                    ViewBag.PageSize = pageSize;
                    ViewBag.TotalPages = report.Any() ? report.First().TotalPages : 1;

                    return View(report);
                }
                catch (Exception ex)
                {
                    TempData["ErrorMessage"] = $"Error generating report: {ex.Message}";
                    return RedirectToAction("Index", "Book");
                }
            }
            else
            {
                TempData["ErrorMessage"] = "Unauthorized";
                return RedirectToAction("Index", "Book");
            }
        }


        public async Task<IActionResult> InActiveUsers(DateTime? startDate, DateTime? endDate, int pageNumber = 1, int pageSize = 3)
        {
            var userId = HttpContext.Session.GetInt32("UserId");

            if (userId == null)
            {
                TempData["ErrorMessage"] = "Unauthorized.";
                return RedirectToAction("Login", "Auth");
            }

            var userRole = HttpContext.Session.GetString("UserRole");
            if (userRole == "Admin")
            {
                ViewBag.UserName = HttpContext.Session.GetString("UserName");
                ViewBag.UserEmail = HttpContext.Session.GetString("UserEmail");
                ViewBag.ProfileImage = HttpContext.Session.GetString("ProfileImage");
                ViewBag.UserRole = userRole;

                try
                {
                    startDate ??= DateTime.Now.AddDays(-30);  // Default to 30 days ago
                    endDate ??= DateTime.Now;  // Default to today

                    var report = await _context.GetInActiveUsers(startDate, endDate, pageNumber, pageSize);
                    ViewBag.PageNumber = pageNumber;
                    ViewBag.PageSize = pageSize;
                    ViewBag.TotalPages = report.Any() ? report.First().TotalPages : 1; // Get TotalPages from report
                    return View(report);
                }
                catch (Exception ex)
                {
                    TempData["ErrorMessage"] = $"Error generating report: {ex.Message}";
                    return RedirectToAction("Index", "Book");
                }
            }
            else
            {
                TempData["ErrorMessage"] = "Unauthorized";
                return RedirectToAction("Index", "Book");
            }
        }

        public async Task<IActionResult> FineSummary(DateTime? startDate, DateTime? endDate, decimal? fineAmount, int? daysAgo, int pageNumber = 1, int pageSize = 3)
        {
            var userId = HttpContext.Session.GetInt32("UserId");

            if (userId == null)
            {
                TempData["ErrorMessage"] = "Unauthorized.";
                return RedirectToAction("Login", "Auth");
            }

            var userRole = HttpContext.Session.GetString("UserRole");
            if (userRole == "Admin")
            {
                ViewBag.UserName = HttpContext.Session.GetString("UserName");
                ViewBag.UserEmail = HttpContext.Session.GetString("UserEmail");
                ViewBag.ProfileImage = HttpContext.Session.GetString("ProfileImage");
                ViewBag.UserRole = userRole;

                try
                {

                    fineAmount = Convert.ToDecimal(_configuration["FINE:FINE_AMOUNT"]);
                    daysAgo =Convert.ToInt32(_configuration["FINE:DAYS_AGO"]);


                    // Default to last 30 days if no start or end date
                    startDate ??= DateTime.Now.AddDays(-30);
                    endDate ??= DateTime.Now;

                    var report = await _context.GetFineSummary(startDate, endDate, fineAmount.Value, daysAgo.Value,pageNumber,pageSize);
                    ViewBag.PageNumber = pageNumber;
                    ViewBag.PageSize = pageSize;
                    ViewBag.TotalPages = report.Any() ? report.First().TotalPages : 1;
                    return View(report);
                }
                catch (Exception ex)
                {
                    TempData["ErrorMessage"] = $"Error generating report: {ex.Message}";
                    return RedirectToAction("Index", "Book");
                }
            }
            else
            {
                TempData["ErrorMessage"] = "Unauthorized";
                return RedirectToAction("Index", "Book");
            }
        }


    }
}
