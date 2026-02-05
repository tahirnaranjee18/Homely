using Homely_Web.Models;
using Microsoft.AspNetCore.Mvc;
using Google.Cloud.Firestore;
using System.Linq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO;
using Microsoft.AspNetCore.Hosting;

namespace Homely_Web.Controllers
{
    public class HomeController : Controller
    {
        private readonly FirestoreDb _db;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public HomeController(FirestoreDb db, IWebHostEnvironment webHostEnvironment)
        {
            _db = db;
            _webHostEnvironment = webHostEnvironment;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string searchString, int bedrooms = 0, int bathrooms = 0, int garages = 0, string priceRange = "All")
        {
            var properties = new List<PropertyModel>();
            Query query = _db.Collection("properties").WhereEqualTo("rented", false);
            var snapshot = await query.GetSnapshotAsync();
            var allProperties = snapshot.Documents.Select(d => d.ConvertTo<PropertyModel>()).ToList();

            var filteredProperties = allProperties.AsEnumerable();

            if (!string.IsNullOrEmpty(searchString))
            {
                filteredProperties = filteredProperties.Where(p =>
                    (p.Title != null && p.Title.Contains(searchString, StringComparison.OrdinalIgnoreCase)) ||
                    (p.Location != null && p.Location.Contains(searchString, StringComparison.OrdinalIgnoreCase))
                );
            }

            if (bedrooms > 0)
            {
                if (bedrooms == 4)
                {
                    filteredProperties = filteredProperties.Where(p => p.Bedrooms >= 4);
                }
                else
                {
                    filteredProperties = filteredProperties.Where(p => p.Bedrooms == bedrooms);
                }
            }

            if (bathrooms > 0)
            {
                if (bathrooms == 3)
                {
                    filteredProperties = filteredProperties.Where(p => p.Bathrooms >= 3);
                }
                else
                {
                    filteredProperties = filteredProperties.Where(p => p.Bathrooms == bathrooms);
                }
            }

            if (garages > 0)
            {
                if (garages == 2)
                {
                    filteredProperties = filteredProperties.Where(p => p.Garages >= 2);
                }
                else
                {
                    filteredProperties = filteredProperties.Where(p => p.Garages == garages);
                }
            }

            if (priceRange != "All")
            {
                filteredProperties = filteredProperties.Where(p => {
                    if (decimal.TryParse(p.Price, out decimal price))
                    {
                        return priceRange switch
                        {
                            "under10k" => price < 10000,
                            "10k-15k" => price >= 10000 && price <= 15000,
                            "15k-20k" => price >= 15001 && price <= 20000,
                            "over20k" => price > 20000,
                            _ => true
                        };
                    }
                    return true;
                });
            }

            ViewBag.CurrentSearch = searchString;
            ViewBag.CurrentBedrooms = bedrooms;
            ViewBag.CurrentBathrooms = bathrooms;
            ViewBag.CurrentGarages = garages;
            ViewBag.CurrentPriceRange = priceRange;
            ViewBag.IsGuest = true;

            return View(filteredProperties.ToList());
        }

        [HttpGet]
        public async Task<IActionResult> Details(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            var doc = await _db.Collection("properties").Document(id).GetSnapshotAsync();
            if (!doc.Exists) return NotFound();

            var property = doc.ConvertTo<PropertyModel>();

            ViewBag.IsGuest = true;
            return View(property);
        }

        [HttpGet]
        public async Task<IActionResult> Contact(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            var doc = await _db.Collection("users").Document(id).GetSnapshotAsync();
            if (!doc.Exists) return NotFound("Landowner not found.");

            var user = doc.ConvertTo<UserModel>();

            ViewBag.IsGuest = true;
            return View(user);
        }

        [HttpGet]
        public async Task<IActionResult> Apply(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            var doc = await _db.Collection("properties").Document(id).GetSnapshotAsync();
            if (!doc.Exists) return NotFound("Property not found.");

            var property = doc.ConvertTo<PropertyModel>();
            ViewBag.Property = property;
            ViewBag.IsGuest = true;

            return View(new TenantApplicationModel { PropertyId = id });
        }

        [HttpPost]
        public async Task<IActionResult> Apply(TenantApplicationModel model, IFormFile idFile, IFormFile payslipFile)
        {
            if (idFile == null || payslipFile == null)
            {
                ModelState.AddModelError("", "You must upload both ID and payslip documents.");
                var propDoc = await _db.Collection("properties").Document(model.PropertyId).GetSnapshotAsync();
                ViewBag.Property = propDoc.ConvertTo<PropertyModel>();
                ViewBag.IsGuest = true;
                return View(model);
            }

            var idUrl = await UploadFileToWebRoot(idFile, "application_ids");
            var payslipUrl = await UploadFileToWebRoot(payslipFile, "application_payslips");

            var newApplication = new Dictionary<string, object>
            {
                { "applicantId", null },
                { "applicantName", model.ApplicantName },
                { "applicantEmail", model.ApplicantEmail },
                { "applicantPhone", model.ApplicantPhone },
                { "propertyId", model.PropertyId },
                { "status", "Pending" },
                { "idUrl", idUrl },
                { "payslipUrl", payslipUrl },
                { "timestamp", FieldValue.ServerTimestamp }
            };

            await _db.Collection("applications").AddAsync(newApplication);

            return RedirectToAction("Confirmation");
        }

        [HttpGet]
        public IActionResult Confirmation()
        {
            ViewBag.IsGuest = true;
            return View();
        }

        private async Task<string> UploadFileToWebRoot(IFormFile file, string subfolder)
        {
            if (file == null || file.Length == 0) return null;

            string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", subfolder);
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            string uniqueFileName = Guid.NewGuid().ToString() + "_" + file.FileName;
            string filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            return $"/uploads/{subfolder}/{uniqueFileName}";
        }
    }
}