using Book_Management_System.Data;
using Book_Management_System.Models.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Book_Management_System.Controllers
{
    public class PaymentController : Controller
    {
        private readonly ApplicationDbContext _context;

        public PaymentController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            var userId = HttpContext.Session.GetInt32("UserId");

            if (userId == null)
            {
                TempData["ErrorMessage"] = "Unauthorized.";
                return RedirectToAction("Login", "Auth");
            }


            var userRole = HttpContext.Session.GetString("UserRole");
            List<PaymentRecord> paymentRecords;

            if (userRole == "Admin")
            {
    
                paymentRecords = _context.PaymentRecords
                   .Include(pr => pr.Users) 
    .Include(pr => pr.Book) 
                    .ToList();
            }else
            {
                TempData["ErrorMessage"] = "Unauthorized";
                return RedirectToAction("Index","Book");
            }


            ViewBag.TotalRevenue = paymentRecords.Sum(pr => pr.ApplicationFee);
            ViewBag.UserName = HttpContext.Session.GetString("UserName");
            ViewBag.UserEmail = HttpContext.Session.GetString("UserEmail");
            ViewBag.ProfileImage = HttpContext.Session.GetString("ProfileImage");

            ViewBag.UserRole = userRole;

            return View(paymentRecords);
        }
    }
}
