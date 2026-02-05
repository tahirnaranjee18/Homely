using Microsoft.AspNetCore.Mvc;
using Homely_Web.Models;
using Google.Cloud.Firestore;
using System.Collections.Generic;
using ClosedXML.Excel;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;
using System.IO;
using System.Text;
using iText.Kernel.Font;
using iText.IO.Font.Constants;
using System.Threading.Tasks;
using System.Linq;
using System.Text.Json;
using FirebaseAdmin;
using FirebaseAdmin.Auth;
using System.Net.Http;
using Microsoft.Extensions.Configuration;

namespace Homely_Web.Controllers
{
    public class ChartData
    {
        public string Label { get; set; }
        public double Value { get; set; }
    }

    public class AdminController : Controller
    {
        private readonly FirestoreDb _db;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly string _firebaseApiKey; 
        private readonly string _userCollection = "users";
        private readonly string _reportCollection = "reports";
        private readonly string _billCollection = "bills";


        public AdminController(FirestoreDb db, IHttpClientFactory httpClientFactory, IConfiguration config)
        {
            _db = db;
            _httpClientFactory = httpClientFactory;
            _firebaseApiKey = config["Firebase:WebApiKey"];
        }

        private string GetCurrentUserId() { return HttpContext.Session.GetString("UserId"); }
        private string GetCurrentUserName() { return HttpContext.Session.GetString("UserName") ?? "Admin User"; }

        #region Auth Actions
        [HttpGet]
        public IActionResult Login() => View();

        [HttpPost]
        public async Task<IActionResult> Login(AdminLogin model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                var client = _httpClientFactory.CreateClient();
                var requestUrl = $"https://identitytoolkit.googleapis.com/v1/accounts:signInWithPassword?key={_firebaseApiKey}";

                var payload = new { email = model.Username, password = model.Password, returnSecureToken = true };
                var jsonPayload = JsonSerializer.Serialize(payload);
                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                var response = await client.PostAsync(requestUrl, content);

                if (response.IsSuccessStatusCode)
                {
                    var responseStream = await response.Content.ReadAsStreamAsync();
                    var authResponse = await JsonSerializer.DeserializeAsync<FirebaseAuthResponse>(responseStream);

                    var userDoc = await _db.Collection(_userCollection).Document(authResponse.LocalId).GetSnapshotAsync();
                    if (!userDoc.Exists)
                    {
                        ModelState.AddModelError("", "Authentication successful, but user record not found in database.");
                        return View(model);
                    }

                    var user = userDoc.ConvertTo<UserModel>();


                    string roleToSet = null;
                    string redirectController = null;
                    string redirectAction = "Dashboard";

                    if (user.Role == "landowner")
                    {
                        roleToSet = "PropertyManager";
                        redirectController = "PropertyManager";
                    }
                    else if (user.Role == "admin")
                    {
                        roleToSet = "Admin";
                        redirectController = "Admin";
                    }

                    if (roleToSet != null)
                    {
                        HttpContext.Session.SetString("UserName", user.FullName);
                        HttpContext.Session.SetString("Role", roleToSet);
                        HttpContext.Session.SetString("UserId", user.Uid);
                        return RedirectToAction(redirectAction, redirectController);
                    }
                    else
                    {
                        ModelState.AddModelError("", "You do not have permission to access this portal.");
                        return View(model);
                    }
                }
                else
                {
                    ModelState.AddModelError("", "Invalid username or password.");
                    return View(model);
                }
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"An unexpected error occurred: {ex.Message}");
                return View(model);
            }
        }

        [HttpGet]
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login");
        }

        [HttpGet]
        public IActionResult Register() => View();

        [HttpPost]
        public async Task<IActionResult> Register(AdminRegister model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            if (model.Password != model.ConfirmPassword)
            {
                ModelState.AddModelError("ConfirmPassword", "The passwords do not match.");
                return View(model);
            }

            try
            {
                var existingUser = await _db.Collection(_userCollection).WhereEqualTo("email", model.Email).Limit(1).GetSnapshotAsync();

                if (existingUser.Count > 0)
                {
                    ModelState.AddModelError("Email", "An account with this email address already exists.");
                    return View(model);
                }

                UserRecordArgs args = new UserRecordArgs()
                {
                    Email = model.Email,
                    Password = model.Password,
                    DisplayName = model.FullName,
                    Disabled = false,
                };
                UserRecord userRecord = await FirebaseAuth.DefaultInstance.CreateUserAsync(args);

                var newUser = new UserModel
                {
                    Uid = userRecord.Uid,
                    FullName = model.FullName,
                    Email = model.Email,
                    Role = "landowner",
                    Status = "Active"
                };

                DocumentReference docRef = _db.Collection(_userCollection).Document(userRecord.Uid);
                await docRef.SetAsync(newUser);

                TempData["SuccessMessage"] = "Registration successful! You can now log in.";
                return RedirectToAction("Login");
            }
            catch (FirebaseAuthException ex)
            {
                if (ex.AuthErrorCode == AuthErrorCode.EmailAlreadyExists)
                {
                    ModelState.AddModelError("Email", "An account with this email address already exists.");
                }
                else
                {
                    ModelState.AddModelError(string.Empty, "An unknown error occurred during registration: " + ex.Message);
                }
                return View(model);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, "An unexpected error occurred: " + ex.Message);
                return View(model);
            }
        }

        [HttpGet]
        public IActionResult ForgotPassword() => View();

        [HttpPost]
        public async Task<IActionResult> ForgotPassword(AdminForgotPassword model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    await FirebaseAuth.DefaultInstance.GeneratePasswordResetLinkAsync(model.Email);
                    TempData["Message"] = "A password reset link has been sent to your email.";
                    return RedirectToAction("Login");
                }
                catch (FirebaseAuthException ex)
                {
                    if (ex.AuthErrorCode == AuthErrorCode.UserNotFound)
                    {

                        TempData["Message"] = "A password reset link has been sent to your email.";
                        return RedirectToAction("Login");
                    }
                    ModelState.AddModelError("", $"An error occurred: {ex.Message}");
                    return View(model);
                }
            }
            return View(model);
        }
        #endregion


        #region Dashboard
        [HttpGet]
        public IActionResult Dashboard()
        {
            ViewBag.ActivePage = "Dashboard";
            var recentActivities = new List<dynamic> { new { User = "john.doe@company.com", Action = "Password reset", Time = "10:25 AM", Status = "Completed" } };
            ViewBag.AdminName = GetCurrentUserName();
            ViewBag.TotalUsers = 1248;
            ViewBag.PendingActions = 23; 
            ViewBag.SystemHealth = 98;
            ViewBag.ActiveSessions = 42; 
            ViewBag.RecentActivities = recentActivities;
            return View();
        }

        [HttpGet]
        public IActionResult ActivityLog()
        {
            var allActivities = new List<dynamic> { new { User = "john.doe@company.com", Action = "Password reset", Time = "10:25 AM", Status = "Completed" } };
            ViewBag.AdminName = GetCurrentUserName();
            ViewBag.AllActivities = allActivities;
            return View();
        }
        #endregion


        #region User Management
        [HttpGet]
        public async Task<IActionResult> UserManagement(string search = "", string role = "All", string status = "All")
        {
            ViewBag.ActivePage = "UserManagement";
            ViewBag.AdminName = GetCurrentUserName();
            Query query = _db.Collection(_userCollection);
            if (role != "All" && !string.IsNullOrWhiteSpace(role))
                query = query.WhereEqualTo("role", role);
            if (status != "All" && !string.IsNullOrWhiteSpace(status))
                query = query.WhereEqualTo("status", status);
            var snapshot = await query.GetSnapshotAsync();
            var users = snapshot.Documents.Select(d => d.ConvertTo<UserModel>()).ToList();
            if (!string.IsNullOrWhiteSpace(search))
            {
                users = users.Where(u => (u.FullName != null && u.FullName.Contains(search, StringComparison.OrdinalIgnoreCase)) ||
                                         (u.Email != null && u.Email.Contains(search, StringComparison.OrdinalIgnoreCase))).ToList();
            }
            ViewBag.Users = users;
            return View();
        }
        [HttpPost]
        public async Task<IActionResult> Activate(string id)
        {
            var userRef = _db.Collection(_userCollection).Document(id);
            var updates = new Dictionary<string, object> { { "status", "Active" } };
            await userRef.UpdateAsync(updates);
            return RedirectToAction("UserManagement");
        }
        [HttpPost]
        public async Task<IActionResult> Deactivate(string id)
        {
            var userRef = _db.Collection(_userCollection).Document(id);
            var updates = new Dictionary<string, object> { { "status", "Inactive" } };
            await userRef.UpdateAsync(updates);
            return RedirectToAction("UserManagement");
        }
        [HttpPost]
        public async Task<IActionResult> Edit(string id, string name, string role)
        {
            var userRef = _db.Collection(_userCollection).Document(id);
            var updates = new Dictionary<string, object> { { "fullName", name }, { "role", role } };
            await userRef.UpdateAsync(updates);
            return RedirectToAction("UserManagement");
        }
        [HttpGet]
        public async Task<IActionResult> GetUser(string id)
        {
            var doc = await _db.Collection(_userCollection).Document(id).GetSnapshotAsync();
            if (!doc.Exists) return NotFound();
            return Json(doc.ConvertTo<UserModel>());
        }
        #endregion


        #region Reports
        [HttpGet]
        public IActionResult Reports()
        {
            ViewBag.AdminName = GetCurrentUserName();
            ViewBag.ReportGenerated = false;
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Reports(string dateRange, string reportType)
        {
            ViewBag.AdminName = GetCurrentUserName();
            ViewBag.ReportGenerated = true;
            ViewBag.SelectedRange = dateRange;
            ViewBag.ReportType = reportType;

            List<ChartData> chartData = await GetChartData(reportType, dateRange);
            ViewBag.ChartData = chartData;

            HttpContext.Session.SetString("LastReportData", JsonSerializer.Serialize(chartData));
            HttpContext.Session.SetString("LastReportType", reportType);

            return View();
        }

        public async Task<IActionResult> ExportExcel()
        {
            var json = HttpContext.Session.GetString("LastReportData");
            var data = JsonSerializer.Deserialize<List<ChartData>>(json) ?? new List<ChartData>();

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Report Data");
            worksheet.Cell(1, 1).Value = "Label";
            worksheet.Cell(1, 2).Value = "Value";

            for (int i = 0; i < data.Count; i++)
            {
                worksheet.Cell(i + 2, 1).Value = data[i].Label;
                worksheet.Cell(i + 2, 2).Value = data[i].Value;
            }

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            stream.Seek(0, SeekOrigin.Begin);
            return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "Report.xlsx");
        }

        public async Task<IActionResult> ExportPDF()
        {
            var json = HttpContext.Session.GetString("LastReportData");
            var reportType = HttpContext.Session.GetString("LastReportType") ?? "Report";
            var data = JsonSerializer.Deserialize<List<ChartData>>(json) ?? new List<ChartData>();

            using var stream = new MemoryStream();
            var writer = new PdfWriter(stream);
            var pdf = new PdfDocument(writer);
            var document = new Document(pdf);

            var boldFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);
            document.Add(new Paragraph($"{reportType} Summary").SetFont(boldFont).SetFontSize(16));
            document.Add(new Paragraph($"Generated on: {DateTime.Now:dd MMM yyyY}\n\n"));

            foreach (var item in data)
            {
                if (reportType == "Financial Summary")
                {
                    document.Add(new Paragraph($"{item.Label}: R{item.Value:N2}"));
                }
                else
                {
                    document.Add(new Paragraph($"{item.Label}: {item.Value}"));
                }
            }

            document.Close();
            var pdfBytes = stream.ToArray();
            return File(pdfBytes, "application/pdf", "Report.pdf");
        }
        #endregion


        #region Settings
        [HttpGet]
        public async Task<IActionResult> Settings()
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return RedirectToAction("Login");

            var doc = await _db.Collection(_userCollection).Document(userId).GetSnapshotAsync();
            if (!doc.Exists) return RedirectToAction("Login");

            var user = doc.ConvertTo<UserModel>();

            ViewBag.DisplayName = user.FullName;
            ViewBag.Email = user.Email;
            ViewBag.ProfilePic = HttpContext.Session.GetString("ProfilePic") ?? "/images/default-profile.png";

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> UpdateProfile(string displayName, string email, IFormFile profilePicture)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return RedirectToAction("Login");

            string imageFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images");
            if (!Directory.Exists(imageFolder))
                Directory.CreateDirectory(imageFolder);

            string profilePath = HttpContext.Session.GetString("ProfilePic") ?? "/images/default-profile.png";

            if (profilePicture != null && profilePicture.Length > 0)
            {
                string fileName = $"{Guid.NewGuid()}{Path.GetExtension(profilePicture.FileName)}";
                string fullPath = Path.Combine(imageFolder, fileName);
                using (var stream = new FileStream(fullPath, FileMode.Create))
                {
                    await profilePicture.CopyToAsync(stream);
                }
                profilePath = $"/images/{fileName}";
            }

            var userRef = _db.Collection(_userCollection).Document(userId);
            var updates = new Dictionary<string, object>
            {
                { "fullName", displayName },
                { "email", email }
            };
            await userRef.UpdateAsync(updates);

            HttpContext.Session.SetString("UserName", displayName);
            HttpContext.Session.SetString("Email", email);
            HttpContext.Session.SetString("ProfilePic", profilePath);

            string settingsFile = Path.Combine(imageFolder, "adminProfile.json");
            var data = new { DisplayName = displayName, Email = email, ProfilePic = profilePath };
            await System.IO.File.WriteAllTextAsync(settingsFile, System.Text.Json.JsonSerializer.Serialize(data));

            TempData["Message"] = "Profile updated successfully!";
            return RedirectToAction("Settings");
        }
        #endregion

 
        #region Helpers
        private async Task<List<ChartData>> GetChartData(string reportType, string dateRange)
        {
            var data = new List<ChartData>();

            Timestamp? startDate = null;

            switch (reportType)
            {
                case "User Activity":
                    Query userQuery = _db.Collection(_userCollection);
                    var userSnapshot = await userQuery.GetSnapshotAsync();
                    data = userSnapshot.Documents
                        .GroupBy(doc => doc.GetValue<string>("role"))
                        .Select(g => new ChartData { Label = g.Key ?? "Unknown", Value = g.Count() })
                        .ToList();
                    break;

                case "Maintenance Requests":
                    Query reportQuery = _db.Collection(_reportCollection);
                    if (startDate.HasValue)
                        reportQuery = reportQuery.WhereGreaterThan("timestamp", startDate);

                    var reportSnapshot = await reportQuery.GetSnapshotAsync();
                    data = reportSnapshot.Documents
                        .GroupBy(doc => doc.GetValue<string>("status"))
                        .Select(g => new ChartData { Label = g.Key ?? "Unknown", Value = g.Count() })
                        .ToList();
                    break;

                case "Financial Summary":
                    Query billQuery = _db.Collection(_billCollection).WhereEqualTo("status", "PAID");
                    if (startDate.HasValue)
                        billQuery = billQuery.WhereGreaterThan("dueDate", startDate);

                    var billSnapshot = await billQuery.GetSnapshotAsync();
                    data = billSnapshot.Documents
                        .Select(d => d.ConvertTo<BillModel>())
                        .GroupBy(b => b.DueDate.ToDateTime().ToString("yyyy-MM"))
                        .Select(g => new ChartData
                        {
                            Label = g.Key,
                            Value = (double)g.Sum(b => decimal.TryParse(b.Amount, out var amt) ? amt : 0)
                        })
                        .OrderBy(x => x.Label)
                        .ToList();
                    break;
            }
            return data;
        }
        #endregion
    }
}