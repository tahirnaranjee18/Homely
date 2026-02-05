using Google.Cloud.Firestore;
using System;

namespace Homely_Web.Models
{
    [FirestoreData]
    public class LeaseModel
    {
        [FirestoreDocumentId]
        public string Id { get; set; }

        [FirestoreProperty("propertyId")]
        public string PropertyId { get; set; }

        [FirestoreProperty("landownerId")]
        public string LandownerId { get; set; }

        [FirestoreProperty("tenantId")]
        public string TenantId { get; set; }

        [FirestoreProperty("status")]
        public string Status { get; set; } = "Active";

        [FirestoreProperty("startDate")]
        public Timestamp StartDate { get; set; }

        [FirestoreProperty("endDate")]
        public Timestamp EndDate { get; set; }

        [FirestoreProperty("depositAmount")]
        public string DepositAmount { get; set; }

        [FirestoreProperty("leaseDocumentUrl")]
        public string LeaseDocumentUrl { get; set; }
        internal string _TenantName { get; set; } = "Loading...";
        internal string _PropertyAddress { get; set; } = "Loading...";

        public string TenantName => _TenantName;
        public string PropertyAddress => _PropertyAddress;

        public string FormattedStartDate => StartDate.ToDateTime().ToString("dd MMM yyyy");
        public string FormattedEndDate => EndDate.ToDateTime().ToString("dd MMM yyyy");
    }
}