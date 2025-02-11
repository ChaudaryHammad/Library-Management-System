using Book_Management_System.Data;
using Book_Management_System.Models;
using Book_Management_System.Models.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using static Book_Management_System.Models.Entities.Enums;

namespace Book_Management_System.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;

        public HomeController(ApplicationDbContext context)
        {
            _context = context;
        }


        [HttpGet]
        public IActionResult Index(string searchQuery, string sort = "createdAt", string order = "desc", int page = 1, int pageSize=5)
        {
            if (pageSize <= 0) pageSize = 5;
            var userQuery = _context.Users.AsQueryable();

            if (!string.IsNullOrEmpty(searchQuery))
            {
                userQuery = userQuery.Where(u => u.FirstName.Contains(searchQuery) ||
                                                 u.LastName.Contains(searchQuery) ||
                                                 u.Email.Contains(searchQuery));
            }

            switch (sort.ToLower())
            {
                case "firstname":
                    userQuery = order == "asc" ? userQuery.OrderBy(u => u.FirstName) : userQuery.OrderByDescending(u => u.FirstName);
                    break;
                case "lastname":
                    userQuery = order == "asc" ? userQuery.OrderBy(u => u.LastName) : userQuery.OrderByDescending(u => u.LastName);
                    break;
                case "email":
                    userQuery = order == "asc" ? userQuery.OrderBy(u => u.Email) : userQuery.OrderByDescending(u => u.Email);
                    break;
                default:
                    userQuery = order == "asc" ? userQuery.OrderBy(u => u.CreatedAt) : userQuery.OrderByDescending(u => u.CreatedAt);
                    break;
            }



            var totalUsers = userQuery.Count();
            var users = userQuery.Skip((page - 1) * pageSize).Take(pageSize).ToList();


            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return RedirectToAction("Login", "Auth");
            }

            var userName = HttpContext.Session.GetString("UserName");
            var userEmail = HttpContext.Session.GetString("UserEmail");
            var userRole = HttpContext.Session.GetString("UserRole");
            var userImage = HttpContext.Session.GetString("ProfileImage");
            
            if(userRole == Role.Admin.ToString())
            {
                var totlalBooks = _context.Books.Count();
                var totalReservations = _context.UserBooks.Count();
                var totalPayments = _context.PaymentRecords.Count();

                ViewData["TotalBooks"] = totlalBooks;
                ViewData["TotalReservations"] = totalReservations;
                ViewData["TotalPayments"] = totalPayments;

            }

            ViewBag.UserName = userName;
            ViewBag.UserEmail = userEmail;
            ViewBag.UserRole = userRole;
            ViewBag.ProfileImage = userImage;
            ViewData["TotalUsers"] = totalUsers;
            ViewData["PageSize"] = pageSize;
            ViewData["CurrentPage"] = page;
            ViewData["UsersList"] = users;
            ViewData["SortColumn"] = sort;
            ViewData["SortOrder"] = order;

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Index(int? Id, [Bind("Id,FirstName,LastName,Email,Password,Dob,RecordStatus,Role,ImageFile")] User user)
        {
            if (user.RecordStatus == null)
                user.RecordStatus = Status.Active.ToString();
            if (user.Role == null)
                user.Role = Role.User.ToString();

            if (user.Dob != null && user.Dob > DateTime.Now.AddYears(-10))
            {
                return Json(new { success = false, message = "User must be older than 10 years." });
            }

            if (Id == null || Id == 0) // Create new user
            {
                if (user.ImageFile != null)
                {
                    var fileName = Guid.NewGuid().ToString() + Path.GetExtension(user.ImageFile.FileName);
                    var filePath = Path.Combine("wwwroot/Image", fileName);
                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await user.ImageFile.CopyToAsync(stream);
                    }
                    user.ImagePath = "/Image/" + fileName;
                }

                var existingEmail = await _context.Users.FirstOrDefaultAsync(u => u.Email == user.Email);

                if (existingEmail != null)
                {
                    return Json(new { success = false, message = "Email Already exists!" });
                }


                _context.Users.Add(user);
                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "User created successfully!" });
            }
            else // Update existing user
            {
                var existingUser = await _context.Users.FindAsync(Id);
   
                if (existingUser == null)
                {
                    return Json(new { success = false, message = "User not found!" });
                }
             


                if (user.ImageFile != null)
                {

                    if (!string.IsNullOrEmpty(existingUser.ImagePath))
                    {
                        var existingImagePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", existingUser.ImagePath.TrimStart('/'));
                        if (System.IO.File.Exists(existingImagePath))
                        {
                            System.IO.File.Delete(existingImagePath);
                        }
                    }

                    var fileName = Guid.NewGuid().ToString() + Path.GetExtension(user.ImageFile.FileName);
                    var filePath = Path.Combine("wwwroot/Image", fileName);
                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await user.ImageFile.CopyToAsync(stream);
                    }
                    existingUser.ImagePath = "/Image/" + fileName;
                }

                var emailExistsForAnotherUser = await _context.Users
          .AnyAsync(u => u.Email == user.Email && u.Id != Id);

                if (emailExistsForAnotherUser)
                {
                    return Json(new { success = false, message = "Email already exists for another user!" });
                }
                existingUser.FirstName = user.FirstName;
                existingUser.LastName = user.LastName;
                existingUser.Email = user.Email;
                existingUser.Password = user.Password;
                existingUser.Dob = user.Dob;
                existingUser.RecordStatus = user.RecordStatus;
                existingUser.Role = user.Role;

                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "User updated successfully!" });
            }
        }


        [HttpPost]
        public IActionResult Delete(int id)
        {
       
            var user = _context.Users.FirstOrDefault(u => u.Id == id);

            if (user == null)
            {
                TempData["ErrorMessage"] = "User not found.";
                return RedirectToAction("Index"); 
            }

            _context.Users.Remove(user);
            _context.SaveChanges();

            TempData["SuccessMessage"] = "User successfully blocked.";
            return RedirectToAction("Index"); 
        }


        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
