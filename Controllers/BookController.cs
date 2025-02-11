using Book_Management_System.Data;
using Book_Management_System.Models.Entities;
using Book_Management_System.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;
using Stripe;
using static Book_Management_System.Models.Entities.Enums;
using QRCoder;
using System.Drawing.Imaging;
using iTextSharp.text.pdf.qrcode;
using System.IO;


namespace Book_Management_System.Controllers
{
    public class BookController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly EmailService _emailService;
        private readonly IConfiguration _configuration;

        private readonly string _STRIPE_SECRET;
        private readonly string _STRIPE_PUBLIC;

        public BookController(ApplicationDbContext context, EmailService emailService, IConfiguration configuration)
        {
            _configuration = configuration;
            _emailService = emailService;
            _context = context;

        }
        public IActionResult Index(string searchQuery, int page = 1, int pageSize = 3, string sort = "BookAddedAt", string order = "desc")
        {
            var booksQuery = _context.Books.AsQueryable();

            if (!string.IsNullOrEmpty(searchQuery))
            {
                booksQuery = booksQuery.Where(b => b.Name.Contains(searchQuery) ||
                                 b.Description.Contains(searchQuery) ||
                                 b.Author.Contains(searchQuery)); ;
            }

            switch (sort.ToLower())
            {
                case "name":
                    booksQuery = order == "asc" ? booksQuery.OrderBy(u => u.Name) : booksQuery.OrderByDescending(u => u.Name);
                    break;
                case "price":
                    booksQuery = order == "asc" ? booksQuery.OrderBy(u => u.Price) : booksQuery.OrderByDescending(u => u.Price);
                    break;
                case "rating":
                    booksQuery = order == "asc" ? booksQuery.OrderBy(u => u.Rating) : booksQuery.OrderByDescending(u => u.Rating);
                    break;
                default:
                    booksQuery = order == "asc" ? booksQuery.OrderBy(u => u.BookAddedAt) : booksQuery.OrderByDescending(u => u.BookAddedAt);
                    break;
            }

            var totalBooks = booksQuery.Count();
            var books = booksQuery
     .Skip((page - 1) * pageSize)
     .Take(pageSize)
     .ToList();
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return RedirectToAction("Login", "Auth");
            }

            var users = _context.Users.ToList();
            var userName = HttpContext.Session.GetString("UserName");
            var userEmail = HttpContext.Session.GetString("UserEmail");
            var userRole = HttpContext.Session.GetString("UserRole");
            var userImage = HttpContext.Session.GetString("ProfileImage");
            if (userRole == Role.Admin.ToString())
            {
                ViewData["UserList"] = users;
            }

            var reservedBooks = _context.UserBooks
               .Where(ub => ub.UserId == userId && ub.Status == "Reserved")
               .Select(ub => ub.BookId)
               .ToList();

      
            ViewBag.UserName = userName;
            ViewBag.UserEmail = userEmail;
            ViewBag.UserRole = userRole;
            ViewBag.ProfileImage = userImage;
            ViewData["UserList"] = users;
            ViewData["BooksList"] = books;
            ViewData["TotalBooks"] = totalBooks;
            ViewData["PageSize"] = pageSize;
            ViewData["CurrentPage"] = page;
            ViewData["ReservedBooks"] = reservedBooks;

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Index(int? id, [Bind("Id,Name,Description,Author,Price,Rating,BookPublichedAt,Status,ImageFile, PdfFilePath,QrCodePath")] Book book)
        {
            if (id == null || id == 0)
            {
                if (book.ImageFile != null)
                {
                    var imagePath = await SaveFileAsync(book.ImageFile, "wwwroot/BookImages");
                    book.ImagePath = "/BookImages/" + imagePath;
                }

                if (Request.Form.Files["PdfFile"] != null)
                {
                    var pdfFile = Request.Form.Files["PdfFile"];
                    var pdfPath = await SaveFileAsync(pdfFile, "wwwroot/BookPdfs");
                    book.PdfFilePath = "/BookPdfs/" + pdfPath;
                }

              
                book.Status = Status.Active.ToString();
                _context.Books.Add(book);
                TempData["SuccessMessage"] = "Book added successfully.";
            }
            else
            {
                var existingBook = await _context.Books.FindAsync(id);
                if (existingBook == null)
                {
                    TempData["ErrorMessage"] = "Book not found.";
                    return Json(new { success = false, message = "Book not found!" });
                }

                // Update Image
                if (book.ImageFile != null)
                {
                    // Delete previous image if exists
                    if (System.IO.File.Exists(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", existingBook.ImagePath.TrimStart('/'))))
                    {
                        System.IO.File.Delete(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", existingBook.ImagePath.TrimStart('/')));
                    }

                    var imagePath = await SaveFileAsync(book.ImageFile, "wwwroot/BookImages");
                    existingBook.ImagePath = "/BookImages/" + imagePath;
                }

                // Update PDF
                if (Request.Form.Files["PdfFile"] != null)
                {
                     if (!string.IsNullOrEmpty(existingBook.PdfFilePath) &&
        System.IO.File.Exists(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", existingBook.PdfFilePath.TrimStart('/'))))
    {
        System.IO.File.Delete(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", existingBook.PdfFilePath.TrimStart('/')));
    }
                    var pdfFile = Request.Form.Files["PdfFile"];
                    var pdfPath = await SaveFileAsync(pdfFile, "wwwroot/BookPdfs");
                    existingBook.PdfFilePath = "/BookPdfs/" + pdfPath;
                }


                if (!string.IsNullOrEmpty(book.QrCodePath))
                {
      
                    existingBook.QrCodePath = book.QrCodePath; 
                }

                // Update other book properties
                existingBook.Name = book.Name;
                existingBook.Description = book.Description;
                existingBook.Author = book.Author;
                existingBook.Price = book.Price;
                existingBook.Rating = book.Rating;
                existingBook.BookPublichedAt = book.BookPublichedAt;
                existingBook.Status = book.Status;

                _context.Books.Update(existingBook);
                TempData["SuccessMessage"] = "Book updated successfully.";
            }

            await _context.SaveChangesAsync();
            return Json(new { success = true, message = "Book saved successfully!" });
        }

        [HttpPost]
        public async Task<IActionResult> GenerateQrCode(string bookName)
        {
            if (string.IsNullOrEmpty(bookName))
            {
                return Json(new { success = false, message = "Book name is required!" });
            }

            // Retrieve the book entry to find the existing QR code path
            var book = await _context.Books.FirstOrDefaultAsync(b => b.Name == bookName);
            if (book == null)
            {
                return Json(new { success = false, message = "Book not found!" });
            }

           
            if (!string.IsNullOrEmpty(book.QrCodePath))
            {
                var existingQrCodePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", book.QrCodePath.TrimStart('/'));
                if (System.IO.File.Exists(existingQrCodePath))
                {
                    System.IO.File.Delete(existingQrCodePath);
                }
            }

          
            var qrCodeFileName = await GenerateQrCodeAsync(bookName);
            var newQrCodePath = $"/BookQRCodes/{qrCodeFileName}";

            
            book.QrCodePath = newQrCodePath;
            _context.Books.Update(book);
            await _context.SaveChangesAsync();

            return Json(new { success = true, qrCodeImage = newQrCodePath });
        }

        private async Task<string> GenerateQrCodeAsync(string data)
        {
            
            using (QRCodeGenerator qrCodeGenerator = new QRCodeGenerator())
            {
         
                using (QRCodeData qrCodeData = qrCodeGenerator.CreateQrCode(data, QRCodeGenerator.ECCLevel.Q))
                {
                   
                    using (PngByteQRCode qrCode = new PngByteQRCode(qrCodeData))
                    {
                        byte[] qrCodeImage = qrCode.GetGraphic(20);

                    
                        var directoryPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "BookQRCodes");
                        if (!Directory.Exists(directoryPath))
                        {
                            Directory.CreateDirectory(directoryPath); 
                        }

                        var qrCodeFileName = Guid.NewGuid().ToString() + ".png";
                        var qrCodePath = Path.Combine(directoryPath, qrCodeFileName);

                    
                        await System.IO.File.WriteAllBytesAsync(qrCodePath, qrCodeImage);

                        return qrCodeFileName; 
                    }
                }
            }
        }

        private async Task<string> SaveFileAsync(IFormFile file, string directoryPath)
        {
            if (!Directory.Exists(directoryPath))
                Directory.CreateDirectory(directoryPath);

            var fileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
            var filePath = Path.Combine(directoryPath, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            return fileName;
        }

        [HttpPost]
        public IActionResult Delete(int id)
        {
            var book = _context.Books.FirstOrDefault(b => b.Id == id);

            if (book == null)
            {
                TempData["ErrorMessage"] = "Book not found.";
                return RedirectToAction("Index");
            }


            book.Status = Status.Blocked.ToString();
            _context.SaveChanges();

            TempData["SuccessMessage"] = "Book successfully blocked.";
            return RedirectToAction("Index");
        }


        [HttpPost]
        public async Task<IActionResult> ReserveBook(int id)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                TempData["ErrorMessage"] = "You need to be logged in to reserve a book.";
                return RedirectToAction("Index");
            }

            var book = await _context.Books.FindAsync(id);
            if (book == null)
            {
                TempData["ErrorMessage"] = "Book not found!";
                return RedirectToAction("Index");
            }
            var existingReservation = await _context.UserBooks
                                                      .FirstOrDefaultAsync(ub => ub.UserId == userId && ub.BookId == id && ub.Status == Enums.Status.Reserved.ToString());
            if (existingReservation != null)
            {
                TempData["ErrorMessage"] = "You have already reserved this book.";
                return RedirectToAction("Index");
            }

            var userBook = new UserBook
            {
                UserId = userId.Value,
                BookId = book.Id,
                ReservedAt = DateTime.UtcNow,
                Status = Enums.Status.Reserved.ToString()
            };

            _context.UserBooks.Add(userBook);
            await _context.SaveChangesAsync();


            //StripeConfiguration.ApiKey = "sk_test_51MtvzuFYlEA8dyD64BageVH6jKMYvutP5gVNhGqXk6w1RvWVDYd69W0kQXRXTAqe4BHh0bkKiJRQFrfchqQc0UcI00iwSWjAHu";

            StripeConfiguration.ApiKey = _configuration["Stripe:SecretKey"];
          
            var applicationFee = Math.Round(book.Price * 0.05m, 2);
            var amountToCharge = (long)(book.Price);
            var paymentIntentOptions = new PaymentIntentCreateOptions
            {
                Amount = amountToCharge, 
                Currency = "usd",
                Description = $"Reserve Book ID: {id}",
                PaymentMethodTypes = new List<string> { "card" },
                Metadata = new Dictionary<string, string>
        {
            { "book_id", book.Id.ToString() },
            { "user_id", userId.Value.ToString() },
            { "userBook_id",userBook.Id.ToString()},
              { "application_fee", applicationFee.ToString() }
                }
            };

            var paymentIntentService = new PaymentIntentService();
            PaymentIntent paymentIntent = await paymentIntentService.CreateAsync(paymentIntentOptions);

      
            return Json(new
            {
                success = true,
                paymentIntentId = paymentIntent.Id,
                clientSecret = paymentIntent.ClientSecret,
                bookName = book.Name,
                bookPrice = book.Price,
                applicationFee = applicationFee,
                userBookId = book.Id
             
            });
        }



        [HttpPost]
        public async Task<IActionResult> SavePayment(string paymentIntentId, decimal amount, decimal applicationFee, int userBookId)
        {

            Console.WriteLine(
                
                paymentIntentId,
                amount,
                applicationFee,
                userBookId

                );

     
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return Json(new { success = false, message = "User not logged in." });
            }

            var userBook = await _context.UserBooks
                                         .FirstOrDefaultAsync(ub => ub.UserId == userId && ub.BookId == userBookId ); 

            if (userBook == null)
            {
                return Json(new { success = false, message = "Reservation not found." });
            }

            var paymentRecord = new PaymentRecord
            {
                UserId = (int)userId,
                BookId = userBookId,
                Amount = amount,
                ApplicationFee = applicationFee,
                PaymentDate = DateTime.UtcNow,
                PaymentIntentId = paymentIntentId,
                Status = Enums.Status.Paid.ToString()
            };

            _context.PaymentRecords.Add(paymentRecord);
            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }

   

    [HttpPost]
        public IActionResult AdminReserveBook(int bookId, int userId)
        {



            if (HttpContext.Session.GetString("UserRole") != Role.Admin.ToString())
            {
                TempData["ErrorMessage"] = "Unauthorized access.";
                return RedirectToAction("Index");
            }

            var book = _context.Books.FirstOrDefault(b => b.Id == bookId);
            if (book == null)
            {
                TempData["ErrorMessage"] = "Book not found!";
                return RedirectToAction("Index");
            }

            // Check if the book is already reserved by the selected user
            var existingReservation = _context.UserBooks
                                              .FirstOrDefault(ub => ub.UserId == userId && ub.BookId == bookId);
            if (existingReservation != null)
            {
                TempData["ErrorMessage"] = "This book is already reserved by the selected user.";
                return RedirectToAction("Index");
            }

            // Create a new reservation entry for the selected user
            var userBook = new UserBook
            {
                UserId = userId,
                BookId = book.Id,
                ReservedAt = DateTime.UtcNow,
                Status = Enums.Status.Reserved.ToString()
            };
            var user = _context.Users.FirstOrDefault(u => u.Id == userId);
            // Add the reservation to the UserBooks table
            _context.UserBooks.Add(userBook);
            _context.SaveChanges();

            TempData["SuccessMessage"] = $"Book reserved successfully for user : {user.Email}.";
            return RedirectToAction("Index");
        }




        public async Task<IActionResult> MyBooks()
        {
            var userId = HttpContext.Session.GetInt32("UserId");

            if (userId == null)
            {
                TempData["ErrorMessage"] = "You need to be logged in to view your reserved books.";
                return RedirectToAction("Login", "Auth");
            }

            var userName = HttpContext.Session.GetString("UserName");
            var userEmail = HttpContext.Session.GetString("UserEmail");
            var userRole = HttpContext.Session.GetString("UserRole");
            var userImage = HttpContext.Session.GetString("ProfileImage");

            ViewBag.UserName = userName;
            ViewBag.UserEmail = userEmail;
            ViewBag.UserRole = userRole;
            ViewBag.ProfileImage = userImage;

            var reservedBooks = await _context.UserBooks
                .Where(ub => ub.UserId == userId && ub.Status == Enums.Status.Reserved.ToString() )
                .Include(ub => ub.Book)
                .Select(ub => new ReservedBookDto
                {
                    Id = ub.Book.Id,
                    Name = ub.Book.Name,
                    Author = ub.Book.Author,
                    Price = ub.Book.Price,
                    Rating = ub.Book.Rating,
                    BookPublichedAt = ub.Book.BookPublichedAt,
                    BookAddedAt = ub.Book.BookAddedAt,
                    Status = ub.Status,
                    ReservedAt = ub.ReservedAt
                }).OrderByDescending(u => u.ReservedAt)
                .ToListAsync();


            return View(reservedBooks);
        }


        public async Task<IActionResult> SendReminders()
        {
            if (HttpContext.Session.GetString("UserRole") != Role.Admin.ToString())
            {
                TempData["ErrorMessage"] = "Unauthorized access.";
                return RedirectToAction("Index");
            }

          
           var dayCount = Convert.ToInt32(_configuration["FINE:DAYS_AGO"] ?? "2");

            var twoDaysAgo = DateTime.UtcNow.AddDays(-(dayCount));

            // Fetch users with reservations older than 2 days along with the book details
            var overdueBooks = await _context.UserBooks
                .Where(ub => ub.ReservedAt <= twoDaysAgo && ub.Status == Enums.Status.Reserved.ToString())
                .Include(ub => ub.User)
                .Include(ub => ub.Book) // Ensure you include the Book entity
                .Select(ub => new
                {
                    Email = ub.User.Email,
                    BookTitle = ub.Book.Name// Assuming Book has a Title property

                })
                .ToListAsync();



            if (overdueBooks.Any())
            {



                foreach (var reminder in overdueBooks)
                {

                    var message = $"Please return your reserved books soon: {reminder.BookTitle}";

                    // Send email
                    _emailService.SendEmail(reminder.Email, "Book Reservation Reminder", message);
                }

                TempData["SuccessMessage"] = "Reminders sent successfully.";
            }
            else
            {
                TempData["InfoMessage"] = "No users with reservations older than 2 days.";
            }

            return RedirectToAction("Index");
        }


        [HttpPost]
        public async Task<IActionResult> SendFineEmails()
        {
            if (HttpContext.Session.GetString("UserRole") != Role.Admin.ToString())
            {
                TempData["ErrorMessage"] = "Unauthorized access.";
                return RedirectToAction("Index");
            }
            var dayCount = Convert.ToInt32(_configuration["FINE:DAYS_AGO"] ?? "2");
            var finePerDay = Convert.ToDecimal(_configuration["FINE:FINE_AMOUNT"]);


            var twoDaysAgo = DateTime.UtcNow.AddDays(-(dayCount));
           

            // Fetch users with overdue books, grouping by user
            var overdueUsers = await _context.UserBooks
                .Where(ub => ub.ReservedAt <= twoDaysAgo && ub.Status == Enums.Status.Reserved.ToString())
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

                    _emailService.SendEmail(user.User.Email, "Overdue Books and Fine Details", emailBody.ToString());
                }

                TempData["SuccessMessage"] = "Fine emails sent successfully.";
            }
            else
            {
                TempData["InfoMessage"] = "No users with overdue books.";
            }

            return RedirectToAction("Index");
        }


        [HttpPost]
        public IActionResult FreeBook(int bookId)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                TempData["ErrorMessage"] = "You need to be logged in to free a book.";
                return RedirectToAction("MyBooks");
            }

            var userBook = _context.UserBooks.FirstOrDefault(ub => ub.UserId == userId && ub.BookId == bookId && ub.Status == Enums.Status.Reserved.ToString());
            if (userBook == null)
            {
                TempData["ErrorMessage"] = "No such reserved book found.";
                return RedirectToAction("MyBooks", "Book");
            }

            _context.UserBooks.Remove(userBook);

            _context.SaveChanges();

            TempData["SuccessMessage"] = "Book freed successfully.";
            return RedirectToAction("MyBooks", "Book");
        }



      



    }





}