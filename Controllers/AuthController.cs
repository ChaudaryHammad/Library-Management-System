using Book_Management_System.Data;
using Book_Management_System.Models.Entities;
using Book_Management_System.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using static Book_Management_System.Models.Entities.Enums;
using MailKit.Net.Smtp;
using MimeKit;
using System.Security.Cryptography;
using System.Text;
using Book_Management_System.Models.ViewModels;

namespace Book_Management_System.Controllers
{
    public class AuthController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly EmailService _emailService;

        public AuthController(ApplicationDbContext context, EmailService emailService)
        {
            _context = context;
            _emailService = emailService;
        }

        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Login(User loginUser)
        {
            var user = _context.Users.FirstOrDefault(u => u.Email == loginUser.Email);

            if (user != null)
            {
                if (user.Password == loginUser.Password)
                {
                    HttpContext.Session.SetInt32("UserId", user.Id);
                    HttpContext.Session.SetString("UserName", user.FirstName);
                    HttpContext.Session.SetString("UserEmail", user.Email);
                    HttpContext.Session.SetString("UserRole", user.Role);
                    HttpContext.Session.SetString("ProfileImage", user.ImagePath);

                    TempData["SuccessMessage"] = "User logged in successfully.";

                    return Json(new { success = true, redirectUrl = user.Role == Role.Admin.ToString() ? Url.Action("Index", "Home") : Url.Action("Index", "Book") });
                }
                else
                {
                    TempData["ErrorMessage"] = "Password is incorrect.";
                    return Json(new { success = false, message = "Password is incorrect." });
                }
            }

            TempData["ErrorMessage"] = "No account found with this email.";
            return Json(new { success = false, message = "No account found with this email." });
        }

        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(User user)
        {
            // Check if email is already registered
            var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == user.Email);
            if (existingUser != null)
            {
                ModelState.AddModelError("Email", "The email address is already taken.");
                TempData["ErrorMessage"] = "The email address is already taken.";
                return View(user);
            }

            // Validate Date of Birth
            if (user.Dob != null && user.Dob > DateTime.Now.AddYears(-10))
            {
                TempData["ErrorMessage"] = "User must be older than 10 years.";
                return View(user);
            }

            // Handle Image Upload
            if (user.ImageFile != null && user.ImageFile.Length > 0)
            {
                try
                {
                    string uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "Image");
                    Directory.CreateDirectory(uploadsFolder); // Ensure directory exists

                    string uniqueFileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(user.ImageFile.FileName);
                    string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await user.ImageFile.CopyToAsync(fileStream);
                    }

                    user.ImagePath = $"/Image/{uniqueFileName}"; 
                }
                catch (Exception ex)
                {
                    TempData["ErrorMessage"] = $"Error uploading image: {ex.Message}";
                    return View(user);
                }
            }

            // Set additional user properties
            user.RecordStatus = Status.Active.ToString();
            user.Role = Role.User.ToString();

            // Save user to the database
            _context.Add(user);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Registration successful!";
            return RedirectToAction("Login", "Auth");
        }



        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login", "Auth");
        }

        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost]
        public IActionResult ForgotPassword(string email)
        {
            var user = _context.Users.FirstOrDefault(u => u.Email == email);
            if (user == null)
            {
                TempData["ErrorMessage"] = "No account found with this email.";
                return View();
            }

            // Encrypt User Id
            var encryptedId = EncryptUserId(user.Id);

            // Generate Reset Password URL
            var resetLink = Url.Action("ResetPassword", "Auth", new { id = encryptedId }, protocol: HttpContext.Request.Scheme);
            var body = $"Please reset your password by clicking the link: <a href='{resetLink}'>Reset Password</a>";

            // Send email
            _emailService.SendEmail(user.Email, "Reset Your Password", body);

            TempData["SuccessMessage"] = "Password reset email sent. Please check your inbox.";
            return RedirectToAction("Login", "Auth");
        }

        [HttpGet]
        public IActionResult ResetPassword(string id)
        {

            var userId = DecryptUserId(id);
            var user = _context.Users.FirstOrDefault(u => u.Id == userId);

            if (user == null)
            {
                TempData["ErrorMessage"] = "Invalid reset link.";
                return RedirectToAction("Login", "Auth");
            }

            return View(new ResetPasswordViewModel { UserId = userId });
        }

        [HttpPost]
        public IActionResult ResetPassword(ResetPasswordViewModel model)
        {
            var user = _context.Users.FirstOrDefault(u => u.Id == model.UserId);

            if (user == null)
            {
                TempData["ErrorMessage"] = "User not found.";
                return View(model);
            }

            user.Password = model.NewPassword;
            _context.Update(user);
            _context.SaveChanges();

            // Send confirmation email
            _emailService.SendEmail(user.Email, "Password Reset Success", "Your password has been successfully reset.");

            TempData["SuccessMessage"] = "Password reset successfully.";
            return RedirectToAction("Login", "Auth");
        }

        // Helper method for encryption
        private string EncryptUserId(int userId)
        {
            using (var aesAlg = Aes.Create())
            {
                aesAlg.Key = Encoding.UTF8.GetBytes("abcdefghijklmnop"); // 16 bytes key
                aesAlg.IV = Encoding.UTF8.GetBytes("IVforAES128Key!!"); // 16 bytes IV

                ICryptoTransform encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV);
                byte[] encrypted = encryptor.TransformFinalBlock(BitConverter.GetBytes(userId), 0, 4);
                return Convert.ToBase64String(encrypted);
            }
        }

        // Helper method for decryption
        private int DecryptUserId(string encryptedId)
        {
            using (var aesAlg = Aes.Create())
            {
                aesAlg.Key = Encoding.UTF8.GetBytes("abcdefghijklmnop");
                aesAlg.IV = Encoding.UTF8.GetBytes("IVforAES128Key!!");

                ICryptoTransform decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV);
                byte[] encryptedBytes = Convert.FromBase64String(encryptedId);
                byte[] decrypted = decryptor.TransformFinalBlock(encryptedBytes, 0, encryptedBytes.Length);
                return BitConverter.ToInt32(decrypted, 0);
            }
        }

        public IActionResult Profile()
        {
            var userName = HttpContext.Session.GetString("UserName");
            var userEmail = HttpContext.Session.GetString("UserEmail");
            var userRole = HttpContext.Session.GetString("UserRole");
            var userImage = HttpContext.Session.GetString("ProfileImage");


            ViewBag.UserName = userName;
            ViewBag.UserEmail = userEmail;
            ViewBag.UserRole = userRole;
            ViewBag.ProfileImage = userImage;
            var userId = HttpContext.Session.GetInt32("UserId");

            var userDetails = _context.Users.FirstOrDefault(u => u.Id == userId);
           
         
            if (userDetails != null)
            {
                return View(userDetails);
            }

            return NotFound();
        }
    }
}
