using Microsoft.AspNetCore.Mvc;
using Homely_Web.Models;
using Google.Cloud.Firestore;
using System.Text.Json;
using System.Linq;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;
using System.IO;
using ClosedXML.Excel;
using iText.Kernel.Font;
using iText.IO.Font.Constants;
using Google.Cloud.Storage.V1;
using System.Net;
using System.Web;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;

namespace Homely_Web.Controllers
{
    public class PropertyManagerController : Controller
    {
        private readonly FirestoreDb _db;

        private readonly string _firebaseStorageBucket = "homely-29769.firebasestorage.app";

        private readonly string _propertyCollection = "properties";
        private readonly string _leaseCollection = "leases";
        private readonly string _userCollection = "users";
        private readonly string _reportCollection = "reports";
        private readonly string _applicationCollection = "applications";

        private readonly string _billCollection = "bills";
        private readonly string _paymentCollection = "payments";

        public PropertyManagerController(FirestoreDb db)
        {
            _db = db;
        }

        #region Auth Helpers
        private bool IsNotManager()
        {
            return HttpContext.Session.GetString("Role") != "PropertyManager";
        }
        private string GetOwnerId()
        {
            return HttpContext.Session.GetString("UserId");
        }
        #endregion

        #region Dashboard
        [HttpGet]
        public async Task<IActionResult> Dashboard()
        {
            if (IsNotManager()) return RedirectToAction("Login", "Admin");
            string ownerId = GetOwnerId();
            var reportsQueryTask = _db.Collection(_reportCollection).WhereEqualTo("landownerId", ownerId).WhereEqualTo("status", "PENDING").GetSnapshotAsync();
            var leasesQueryTask = _db.Collection(_leaseCollection).WhereEqualTo("landownerId", ownerId).WhereEqualTo("status", "ACTIVE").WhereLessThan("endDate", Timestamp.FromDateTime(DateTime.UtcNow.AddDays(60))).GetSnapshotAsync();
            var allPropsQueryTask = _db.Collection(_propertyCollection).WhereEqualTo("ownerId", ownerId).GetSnapshotAsync();
            var recentPropsQueryTask = _db.Collection(_propertyCollection)
                                           .WhereEqualTo("ownerId", ownerId)
                                           .OrderByDescending("createdAt")
                                           .Limit(3)
                                           .GetSnapshotAsync();
            await Task.WhenAll(reportsQueryTask, leasesQueryTask, allPropsQueryTask, recentPropsQueryTask);
            var allPropsSnapshot = await allPropsQueryTask;
            var rentedProps = allPropsSnapshot.Documents.Count(d => d.ConvertTo<PropertyModel>().IsRented);
            var totalProps = allPropsSnapshot.Count;
            var recentPropsSnapshot = await recentPropsQueryTask;
            var recentProperties = recentPropsSnapshot.Documents.Select(d => d.ConvertTo<PropertyModel>()).ToList();
            ViewBag.TotalProperties = totalProps;
            ViewBag.MaintenanceRequests = (await reportsQueryTask).Count;
            ViewBag.ExpiringLeases = (await leasesQueryTask).Count;
            ViewBag.OccupancyRate = totalProps > 0 ? (int)((double)rentedProps / totalProps * 100) : 0;
            ViewBag.ManagerName = HttpContext.Session.GetString("UserName") ?? "Property Manager";
            ViewBag.Properties = recentProperties;
            return View();
        }
        #endregion

        #region Properties
        [HttpGet]
        public async Task<IActionResult> Properties(string status = "All", string searchString = "")
        {
            if (IsNotManager()) return RedirectToAction("Login", "Admin");
            string ownerId = GetOwnerId();
            Query query = _db.Collection(_propertyCollection)
                             .WhereEqualTo("ownerId", ownerId)
                             .OrderByDescending("createdAt");
            if (status == "Available")
                query = query.WhereEqualTo("rented", false);
            else if (status == "Occupied")
                query = query.WhereEqualTo("rented", true);
            var snapshot = await query.GetSnapshotAsync();
            var properties = snapshot.Documents.Select(d => d.ConvertTo<PropertyModel>()).ToList();
            if (!string.IsNullOrEmpty(searchString))
            {
                properties = properties.Where(p =>
                    (p.Title != null && p.Title.Contains(searchString, StringComparison.OrdinalIgnoreCase)) ||
                    (p.Location != null && p.Location.Contains(searchString, StringComparison.OrdinalIgnoreCase))
                ).ToList();
            }
            ViewBag.CurrentSearch = searchString;
            return View(properties);
        }

        [HttpGet]
        public async Task<IActionResult> PropertyDetails(string id)
        {
            if (IsNotManager()) return RedirectToAction("Login", "Admin");
            var doc = await _db.Collection(_propertyCollection).Document(id).GetSnapshotAsync();
            if (!doc.Exists) return NotFound("Property not found.");
            var property = doc.ConvertTo<PropertyModel>();
            if (property.OwnerId != GetOwnerId()) return Unauthorized();
            return View(property);
        }

        [HttpPost]
        public async Task<IActionResult> AddProperty([FromForm] string title, [FromForm] string location, [FromForm] string price, [FromForm] string leaseDuration, [FromForm] int bedrooms, [FromForm] int bathrooms, [FromForm] int garages, IFormFile? image)
        {
            if (IsNotManager()) return Json(new { success = false, message = "Not authorized." });
            string ownerId = GetOwnerId();
            string publicImageUrl = "/images/default-property.jpg";
            if (image != null && image.Length > 0)
            {
                publicImageUrl = await UploadImageToFirebaseStorage(image);
            }
            var newProperty = new PropertyModel
            {
                OwnerId = ownerId,
                Title = title,
                Location = location,
                Price = price,
                LeaseDuration = leaseDuration,
                Bedrooms = bedrooms,
                Bathrooms = bathrooms,
                Garages = garages,
                IsRented = false,
                ImageUrl = publicImageUrl,
                ImageUrls = new List<string> { publicImageUrl },
                CreatedAt = Timestamp.FromDateTime(DateTime.UtcNow)
            };
            await _db.Collection(_propertyCollection).AddAsync(newProperty);
            return Json(new { success = true, message = "Property added successfully!" });
        }

        [HttpPost]
        public async Task<IActionResult> EditProperty([FromForm] string id, [FromForm] string title, [FromForm] string location, [FromForm] string price, [FromForm] string leaseDuration, [FromForm] int bedrooms, [FromForm] int bathrooms, [FromForm] int garages, IFormFile? image)
        {
            if (IsNotManager()) return Json(new { success = false, message = "Not authorized." });
            var docRef = _db.Collection(_propertyCollection).Document(id);
            var snapshot = await docRef.GetSnapshotAsync();
            if (!snapshot.Exists) return Json(new { success = false, message = "Property not found." });
            var property = snapshot.ConvertTo<PropertyModel>();
            if (property.OwnerId != GetOwnerId()) return Json(new { success = false, message = "Unauthorized." });
            var updates = new Dictionary<string, object>
            {
                { "title", title },
                { "location", location },
                { "price", price },
                { "leaseDuration", leaseDuration },
                { "bedrooms", bedrooms },
                { "bathrooms", bathrooms },
                { "garages", garages }
            };
            if (property.CreatedAt.ToDateTime() < new DateTime(2000, 1, 1))
            {
                updates.Add("createdAt", Timestamp.FromDateTime(DateTime.UtcNow));
            }
            if (image != null && image.Length > 0)
            {
                string publicImageUrl = await UploadImageToFirebaseStorage(image);
                updates.Add("imageUrl", publicImageUrl);
                updates.Add("imageUrls", new List<string> { publicImageUrl });
            }
            await docRef.UpdateAsync(updates);
            return Json(new { success = true, message = "Property updated successfully!" });
        }

        [HttpPost]
        public async Task<IActionResult> DeleteProperty(string id)
        {
            if (IsNotManager()) return Json(new { success = false, message = "Not authorized." });
            if (string.IsNullOrEmpty(id)) return Json(new { success = false, message = "Invalid ID." });
            var docRef = _db.Collection(_propertyCollection).Document(id);
            var snapshot = await docRef.GetSnapshotAsync();
            if (!snapshot.Exists) return Json(new { success = false, message = "Property not found." });
            if (snapshot.ConvertTo<PropertyModel>().OwnerId != GetOwnerId())
                return Json(new { success = false, message = "You do not own this property." });
            var leaseQuery = _db.Collection(_leaseCollection).WhereEqualTo("propertyId", id).WhereEqualTo("status", "ACTIVE").Limit(1);
            var leaseSnapshot = await leaseQuery.GetSnapshotAsync();
            if (leaseSnapshot.Count > 0)
            {
                return Json(new { success = false, message = "Cannot delete property. It has one or more active leases. Please terminate the lease(s) first." });
            }
            await docRef.DeleteAsync();
            return Json(new { success = true, message = "Property deleted successfully!" });
        }
        #endregion

        #region Leases
        [HttpGet]
        public async Task<IActionResult> Leases(string searchString = "")
        {
            if (IsNotManager()) return RedirectToAction("Login", "Admin");
            string ownerId = GetOwnerId();
            var snapshot = await _db.Collection(_leaseCollection).WhereEqualTo("landownerId", ownerId).GetSnapshotAsync();
            var leases = new List<LeaseModel>();
            var userIds = new List<string>();
            var propertyIds = new List<string>();
            foreach (var doc in snapshot.Documents)
            {
                var lease = doc.ConvertTo<LeaseModel>();
                leases.Add(lease);
                if (!userIds.Contains(lease.TenantId)) userIds.Add(lease.TenantId);
                if (!propertyIds.Contains(lease.PropertyId)) propertyIds.Add(lease.PropertyId);
            }
            var users = await GetUserDictionary(userIds);
            var properties = await GetPropertyDictionary(propertyIds);
            foreach (var lease in leases)
            {
                lease._TenantName = users.GetValueOrDefault(lease.TenantId, "Unknown Tenant");
                lease._PropertyAddress = properties.GetValueOrDefault(lease.PropertyId, "Unknown Property");
            }
            ViewBag.ActiveLeaseCount = leases.Count(l => l.Status == "ACTIVE");
            ViewBag.InactiveLeaseCount = leases.Count(l => l.Status != "ACTIVE");
            var finalLeases = leases.AsEnumerable();
            if (!string.IsNullOrEmpty(searchString))
            {
                finalLeases = finalLeases.Where(l => l.TenantName.Contains(searchString, StringComparison.OrdinalIgnoreCase) ||
                                                      l.PropertyAddress.Contains(searchString, StringComparison.OrdinalIgnoreCase));
            }
            var tenantSnapshot = await _db.Collection(_userCollection).WhereEqualTo("role", "tenant").GetSnapshotAsync();
            ViewBag.Tenants = tenantSnapshot.Documents.Select(d => d.ConvertTo<UserModel>()).ToList();
            var propSnapshot = await _db.Collection(_propertyCollection).WhereEqualTo("ownerId", ownerId).GetSnapshotAsync();
            ViewBag.Properties = propSnapshot.Documents.Select(d => d.ConvertTo<PropertyModel>()).ToList();
            ViewBag.CurrentSearch = searchString;
            return View(finalLeases.ToList());
        }

        [HttpPost]
        public async Task<IActionResult> AddLease(IFormCollection form, IFormFile? pdfFile)
        {
            if (IsNotManager()) return Json(new { success = false, message = "Not authorized." });
            string ownerId = GetOwnerId();
            string pdfPath = "/leases/sample.pdf";
            string propertyId = form["propertyId"];
            string tenantId = form["tenantId"];
            if (pdfFile != null && pdfFile.Length > 0)
            {
                string leaseFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/leases");
                if (!Directory.Exists(leaseFolder)) Directory.CreateDirectory(leaseFolder);
                string fileName = Guid.NewGuid().ToString() + Path.GetExtension(pdfFile.FileName);
                string fullPath = Path.Combine(leaseFolder, fileName);
                using (var stream = new FileStream(fullPath, FileMode.Create)) { await pdfFile.CopyToAsync(stream); }
                pdfPath = "/leases/" + fileName;
            }
            var newLeaseRef = _db.Collection(_leaseCollection).Document();
            var newLease = new LeaseModel
            {
                Id = newLeaseRef.Id,
                PropertyId = propertyId,
                TenantId = tenantId,
                LandownerId = ownerId,
                StartDate = Timestamp.FromDateTime(DateTime.Parse(form["startDate"]).ToUniversalTime()),
                EndDate = Timestamp.FromDateTime(DateTime.Parse(form["endDate"]).ToUniversalTime()),
                DepositAmount = form["rentAmount"],
                LeaseDocumentUrl = pdfPath,
                Status = "ACTIVE"
            };
            var batch = _db.StartBatch();
            batch.Set(newLeaseRef, newLease);
            var propertyRef = _db.Collection(_propertyCollection).Document(propertyId);
            var propertyUpdates = new Dictionary<string, object>
            {
                { "rented", true },
                { "tenantId", tenantId }
            };
            batch.Update(propertyRef, propertyUpdates);
            await batch.CommitAsync();
            return Json(new { success = true, message = "Lease added and property updated successfully!" });
        }

        [HttpPost]
        public async Task<IActionResult> TerminateLease(string id)
        {
            if (IsNotManager()) return Json(new { success = false, message = "Not authorized." });
            var leaseRef = _db.Collection(_leaseCollection).Document(id);
            var leaseSnap = await leaseRef.GetSnapshotAsync();
            if (!leaseSnap.Exists) return Json(new { success = false, message = "Lease not found." });
            var lease = leaseSnap.ConvertTo<LeaseModel>();
            if (lease.LandownerId != GetOwnerId()) return Json(new { success = false, message = "Unauthorized." });
            var batch = _db.StartBatch();
            batch.Update(leaseRef, "status", "TERMINATED_BY_LANDOWNER");
            var propertyRef = _db.Collection(_propertyCollection).Document(lease.PropertyId);
            var propertyUpdates = new Dictionary<string, object>
            {
                { "rented", false },
                { "tenantId", FieldValue.Delete }
            };
            batch.Update(propertyRef, propertyUpdates);
            await batch.CommitAsync();
            return Json(new { success = true, message = "Lease has been terminated and property is now available." });
        }

        [HttpPost]
        public async Task<IActionResult> RenewLease(string id)
        {
            if (IsNotManager()) return Json(new { success = false, message = "Not authorized." });
            var docRef = _db.Collection(_leaseCollection).Document(id);
            var snapshot = await docRef.GetSnapshotAsync();
            if (!snapshot.Exists) return Json(new { success = false, message = "Lease not found." });
            var lease = snapshot.ConvertTo<LeaseModel>();
            if (lease.LandownerId != GetOwnerId()) return Json(new { success = false, message = "Unauthorized." });
            var newEndDate = lease.EndDate.ToDateTime().AddYears(1);
            var updates = new Dictionary<string, object> { { "endDate", Timestamp.FromDateTime(newEndDate.ToUniversalTime()) }, { "status", "ACTIVE" } };
            await docRef.UpdateAsync(updates);
            return Json(new { success = true, message = $"Lease renewed." });
        }
        #endregion

        #region Applications
        [HttpGet]
        public async Task<IActionResult> Applications(string searchString = "")
        {
            if (IsNotManager()) return RedirectToAction("Login", "Admin");
            string ownerId = GetOwnerId();
            var propSnapshot = await _db.Collection(_propertyCollection).WhereEqualTo("ownerId", ownerId).GetSnapshotAsync();
            var propertyIds = propSnapshot.Documents.Select(d => d.Id).ToList();
            if (!propertyIds.Any())
            {
                ViewBag.Pending = new List<TenantApplicationModel>();
                ViewBag.Approved = new List<TenantApplicationModel>();
                ViewBag.Rejected = new List<TenantApplicationModel>();
                return View(new List<TenantApplicationModel>());
            }
            var appSnapshot = await _db.Collection(_applicationCollection).WhereIn("propertyId", propertyIds).GetSnapshotAsync();
            var applications = appSnapshot.Documents.Select(d => d.ConvertTo<TenantApplicationModel>()).ToList();
            var properties = await GetPropertyDictionary(propertyIds);
            foreach (var app in applications)
            {
                app._PropertyName = properties.GetValueOrDefault(app.PropertyId, "Unknown Property");
            }
            if (!string.IsNullOrEmpty(searchString))
            {
                applications = applications.Where(a => a.ApplicantName.Contains(searchString, StringComparison.OrdinalIgnoreCase)).ToList();
            }
            ViewBag.Pending = applications.Where(a => a.Status == "Pending").ToList();
            ViewBag.Approved = applications.Where(a => a.Status == "Approved").ToList();
            ViewBag.Rejected = applications.Where(a => a.Status == "Rejected").ToList();
            ViewBag.CurrentSearch = searchString;
            return View(applications);
        }
        [HttpPost]
        public async Task<IActionResult> ApproveApplication(string id)
        {
            if (IsNotManager()) return Json(new { success = false, message = "Not authorized." });
            var appRef = _db.Collection(_applicationCollection).Document(id);
            await appRef.UpdateAsync("status", "Approved");
            return Json(new { success = true, message = "Application approved." });
        }
        [HttpPost]
        public async Task<IActionResult> RejectApplication(string id)
        {
            if (IsNotManager()) return Json(new { success = false, message = "Not authorized." });
            var appRef = _db.Collection(_applicationCollection).Document(id);
            await appRef.UpdateAsync("status", "Rejected");
            return Json(new { success = true, message = "Application rejected." });
        }
        #endregion

        #region Maintenance
        [HttpGet]
        public async Task<IActionResult> Maintenance(string searchString = "")
        {
            if (IsNotManager()) return RedirectToAction("Login", "Admin");
            string ownerId = GetOwnerId();
            var snapshot = await _db.Collection(_reportCollection).WhereEqualTo("landownerId", ownerId).OrderByDescending("timestamp").GetSnapshotAsync();
            var reports = snapshot.Documents.Select(d => d.ConvertTo<ReportModel>()).ToList();
            var caretakerIds = reports.Select(r => r.AssignedCaretakerId).Where(id => !string.IsNullOrEmpty(id)).Distinct().ToList();
            var caretakers = await GetUserDictionary(caretakerIds);
            foreach (var report in reports)
            {
                report._AssignedCaretakerName = caretakers.GetValueOrDefault(report.AssignedCaretakerId, "N/A");
            }
            var filteredReports = reports.AsEnumerable();
            if (!string.IsNullOrEmpty(searchString))
            {
                filteredReports = filteredReports.Where(r => r.Title.Contains(searchString, StringComparison.OrdinalIgnoreCase) ||
                                                              r.PropertyAddress.Contains(searchString, StringComparison.OrdinalIgnoreCase));
            }
            ViewBag.OpenTasks = filteredReports.Where(t => t.Status == "PENDING").ToList();
            ViewBag.InProgress = filteredReports.Where(t => t.Status == "IN_PROGRESS").ToList();
            ViewBag.Completed = filteredReports.Where(t => t.Status == "RESOLVED").ToList();
            var cSnapshot = await _db.Collection(_userCollection).WhereEqualTo("role", "caretaker").GetSnapshotAsync();
            ViewBag.Caretakers = cSnapshot.Documents.Select(d => d.ConvertTo<UserModel>()).ToList();
            var pSnapshot = await _db.Collection(_propertyCollection).WhereEqualTo("ownerId", ownerId).GetSnapshotAsync();
            ViewBag.Properties = pSnapshot.Documents.Select(d => d.ConvertTo<PropertyModel>()).ToList();
            ViewBag.CurrentSearch = searchString;
            return View(filteredReports.ToList());
        }
        [HttpPost]
        public async Task<IActionResult> AssignCaretaker(string id, string caretakerId)
        {
            if (IsNotManager()) return Json(new { success = false, message = "Not authorized." });
            string ownerId = GetOwnerId();
            var reportRef = _db.Collection(_reportCollection).Document(id);
            var reportSnap = await reportRef.GetSnapshotAsync();
            if (!reportSnap.Exists) return Json(new { success = false, message = "Report not found." });
            if (reportSnap.ConvertTo<ReportModel>().LandownerId != ownerId)
                return Json(new { success = false, message = "Unauthorized." });
            var userDoc = await _db.Collection(_userCollection).Document(caretakerId).GetSnapshotAsync();
            var caretakerName = userDoc.Exists ? userDoc.ConvertTo<UserModel>().FullName : "Unknown";
            var batch = _db.StartBatch();
            var updates = new Dictionary<string, object> { { "assignedCaretakerId", caretakerId }, { "status", "IN_PROGRESS" } };
            batch.Update(reportRef, updates);
            var commentRef = reportRef.Collection("comments").Document();
            var comment = new Dictionary<string, object> { { "authorId", ownerId }, { "authorName", HttpContext.Session.GetString("UserName") ?? "System" }, { "text", $"Task assigned to {caretakerName}." }, { "imageUrl", null }, { "timestamp", FieldValue.ServerTimestamp } };
            batch.Set(commentRef, comment);
            await batch.CommitAsync();
            return Json(new { success = true, message = "Caretaker assigned successfully!" });
        }
        [HttpPost]
        public async Task<IActionResult> UpdateStatus(string id, string status)
        {
            if (IsNotManager()) return Json(new { success = false, message = "Not authorized." });
            var reportRef = _db.Collection(_reportCollection).Document(id);
            await reportRef.UpdateAsync("status", status);
            return Json(new { success = true, message = $"Task moved to {status}." });
        }
        #endregion

        #region Arrears

        [HttpGet]
        public async Task<IActionResult> Arrears(string searchString = "")
        {
            if (IsNotManager()) return RedirectToAction("Login", "Admin");
            string ownerId = GetOwnerId();

            var billsQuery = _db.Collection(_billCollection)
                                .WhereEqualTo("landownerId", ownerId)
                                .WhereEqualTo("status", "UNPAID");

            var paymentsQuery = _db.Collection(_paymentCollection)
                                   .WhereEqualTo("landownerId", ownerId)
                                   .WhereIn("status", new List<string> { "PENDING", "Pending", "pending" });

            var recentPaymentsQuery = _db.Collection(_paymentCollection)
                                         .WhereEqualTo("landownerId", ownerId)
                                         .WhereEqualTo("status", "Approved")
                                         .OrderByDescending("timestamp")
                                         .Limit(20)
                                         .GetSnapshotAsync();

            await Task.WhenAll(billsQuery.GetSnapshotAsync(), paymentsQuery.GetSnapshotAsync(), recentPaymentsQuery);

            var overdueBillsSnapshot = await billsQuery.GetSnapshotAsync();
            var pendingPaymentsSnapshot = await paymentsQuery.GetSnapshotAsync();
            var recentPaymentsSnapshot = await recentPaymentsQuery;

            var overdueBills = overdueBillsSnapshot.Documents.Select(d => d.ConvertTo<BillModel>()).ToList();
            var pendingPayments = pendingPaymentsSnapshot.Documents.Select(d => d.ConvertTo<PaymentModel>()).ToList();
            var recentPayments = recentPaymentsSnapshot.Documents.Select(d => d.ConvertTo<PaymentModel>()).ToList();

            var tenantIds = new List<string>();
            tenantIds.AddRange(overdueBills.Select(b => b.TenantId));
            tenantIds.AddRange(pendingPayments.Select(p => p.UserId));
            tenantIds.AddRange(recentPayments.Select(p => p.UserId));
            var tenants = await GetUserDictionary(tenantIds.Distinct().ToList());

            foreach (var bill in overdueBills)
            {
                bill._TenantName = tenants.GetValueOrDefault(bill.TenantId, "Unknown Tenant");
            }
            foreach (var payment in pendingPayments)
            {
                payment._TenantName = tenants.GetValueOrDefault(payment.UserId, "Unknown Tenant");
            }
            foreach (var payment in recentPayments)
            {
                payment._TenantName = tenants.GetValueOrDefault(payment.UserId, "Unknown Tenant");
            }

            var filteredOverdue = overdueBills.AsEnumerable();
            var filteredPending = pendingPayments.AsEnumerable();

            if (!string.IsNullOrEmpty(searchString))
            {
                filteredOverdue = filteredOverdue.Where(b => b._TenantName.Contains(searchString, StringComparison.OrdinalIgnoreCase));
                filteredPending = filteredPending.Where(p => p._TenantName.Contains(searchString, StringComparison.OrdinalIgnoreCase));
            }

            var finalOverdueList = filteredOverdue.OrderBy(b => b.DueDate).ToList();
            var finalPendingList = filteredPending.OrderBy(p => p.Timestamp).ToList();

            ViewBag.OverdueBills = finalOverdueList;
            ViewBag.PendingPayments = finalPendingList;
            ViewBag.RecentPayments = recentPayments;

            ViewBag.OverdueCount = finalOverdueList.Count;
            ViewBag.PendingCount = finalPendingList.Count;

            ViewBag.CurrentSearch = searchString;

            var activeLeases = await _db.Collection(_leaseCollection)
                                        .WhereEqualTo("landownerId", ownerId)
                                        .WhereEqualTo("status", "ACTIVE")
                                        .GetSnapshotAsync();
            var activeTenantIds = activeLeases.Documents.Select(d => d.ConvertTo<LeaseModel>().TenantId).Distinct().ToList();
            ViewBag.ActiveTenants = await GetUserDictionary(activeTenantIds);

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> CreateBill(string tenantId, string amount, string description, DateTime dueDate)
        {
            if (IsNotManager()) return Json(new { success = false, message = "Not authorized." });
            string ownerId = GetOwnerId();

            var newBillRef = _db.Collection(_billCollection).Document();
            var bill = new BillModel
            {
                Id = newBillRef.Id,
                TenantId = tenantId,
                LandownerId = ownerId,
                Amount = amount,
                Description = description,
                DueDate = Timestamp.FromDateTime(dueDate.ToUniversalTime()),
                Status = "UNPAID"
            };

            await newBillRef.SetAsync(bill);

            return Json(new { success = true, message = $"Bill for R{amount} sent to tenant." });
        }

        [HttpPost]
        public async Task<IActionResult> ApprovePayment(string paymentId, string billId)
        {
            if (IsNotManager()) return Json(new { success = false, message = "Not authorized." });
            if (string.IsNullOrEmpty(paymentId) || string.IsNullOrEmpty(billId))
                return Json(new { success = false, message = "Invalid IDs." });

            var paymentRef = _db.Collection(_paymentCollection).Document(paymentId);
            var billRef = _db.Collection(_billCollection).Document(billId);

            var batch = _db.StartBatch();
            batch.Update(paymentRef, "status", "Approved");
            batch.Update(billRef, "status", "PAID");
            await batch.CommitAsync();

            return Json(new { success = true, message = "Payment approved and marked as PAID." });
        }

        [HttpPost]
        public async Task<IActionResult> RejectPayment(string paymentId, string billId)
        {
            if (IsNotManager()) return Json(new { success = false, message = "Not authorized." });
            if (string.IsNullOrEmpty(paymentId) || string.IsNullOrEmpty(billId))
                return Json(new { success = false, message = "Invalid IDs." });

            var paymentRef = _db.Collection(_paymentCollection).Document(paymentId);
            var billRef = _db.Collection(_billCollection).Document(billId);

            var batch = _db.StartBatch();
            batch.Update(paymentRef, "status", "Rejected");
            batch.Update(billRef, "status", "UNPAID");
            await batch.CommitAsync();

            return Json(new { success = true, message = "Payment rejected and marked as UNPAID." });
        }
        #endregion

        #region Settings
        [HttpGet]
        public async Task<IActionResult> Settings()
        {
            if (IsNotManager()) return RedirectToAction("Login", "Admin");
            var doc = await _db.Collection(_userCollection).Document(GetOwnerId()).GetSnapshotAsync();
            if (!doc.Exists) return NotFound("User not found.");
            return View(doc.ConvertTo<UserModel>());
        }
        [HttpPost]
        public async Task<IActionResult> SaveSettings(UserModel model, IFormFile? profilePicture)
        {
            if (IsNotManager()) return Json(new { success = false, message = "Not authorized." });
            string profilePath = HttpContext.Session.GetString("ProfilePic") ?? "/images/default-profile.png";
            if (profilePicture != null && profilePicture.Length > 0)
            {
                string imageFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images");
                if (!Directory.Exists(imageFolder)) Directory.CreateDirectory(imageFolder);
                string fileName = Guid.NewGuid().ToString() + Path.GetExtension(profilePicture.FileName);
                string fullPath = Path.Combine(imageFolder, fileName);
                using (var stream = new FileStream(fullPath, FileMode.Create)) { await profilePicture.CopyToAsync(stream); }
                profilePath = "/images/" + fileName;
            }
            var userRef = _db.Collection(_userCollection).Document(GetOwnerId());
            var updates = new Dictionary<string, object> { { "fullName", model.FullName }, { "email", model.Email } };
            await userRef.UpdateAsync(updates);
            HttpContext.Session.SetString("UserName", model.FullName);
            HttpContext.Session.SetString("ProfilePic", profilePath);
            return Json(new { success = true, message = "Settings updated successfully!" });
        }
        #endregion

        #region Helpers
        private async Task<Dictionary<string, string>> GetUserDictionary(List<string> userIds)
        {
            if (userIds == null || !userIds.Any()) return new Dictionary<string, string>();
            var userDict = new Dictionary<string, string>();
            var batches = userIds.Distinct().Chunk(30);
            foreach (var batch in batches)
            {
                var snapshot = await _db.Collection(_userCollection).WhereIn(FieldPath.DocumentId, batch).GetSnapshotAsync();
                foreach (var doc in snapshot.Documents)
                {
                    userDict[doc.Id] = doc.ConvertTo<UserModel>().FullName;
                }
            }
            return userDict;
        }
        private async Task<Dictionary<string, string>> GetPropertyDictionary(List<string> propertyIds)
        {
            if (propertyIds == null || !propertyIds.Any()) return new Dictionary<string, string>();
            var propDict = new Dictionary<string, string>();
            var batches = propertyIds.Distinct().Chunk(30);
            foreach (var batch in batches)
            {
                var snapshot = await _db.Collection(_propertyCollection).WhereIn(FieldPath.DocumentId, batch).GetSnapshotAsync();
                foreach (var doc in snapshot.Documents)
                {
                    propDict[doc.Id] = doc.ConvertTo<PropertyModel>().Location;
                }
            }
            return propDict;
        }

        private async Task<string> UploadImageToFirebaseStorage(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return null;

            var storage = StorageClient.Create();
            string fileName = $"properties/{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";

            using (var stream = new MemoryStream())
            {
                await file.CopyToAsync(stream);
                stream.Position = 0;

                var storageObject = await storage.UploadObjectAsync(
                    _firebaseStorageBucket,
                    fileName,
                    file.ContentType,
                    stream,
                    new UploadObjectOptions { PredefinedAcl = PredefinedObjectAcl.PublicRead }
                );

                var encodedFileName = HttpUtility.UrlEncode(storageObject.Name);
                return $"https://firebasestorage.googleapis.com/v0/b/{storageObject.Bucket}/o/{encodedFileName}?alt=media";
            }
        }
        #endregion
    }
}