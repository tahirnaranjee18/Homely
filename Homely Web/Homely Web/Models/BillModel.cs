using Google.Cloud.Firestore;
using System;

namespace Homely_Web.Models
{
    [FirestoreData]
    public class BillModel
    {
        [FirestoreDocumentId]
        public string Id { get; set; }

        [FirestoreProperty("amount")]
        public string Amount { get; set; }

        [FirestoreProperty("dueDate")]
        public Timestamp DueDate { get; set; }

        [FirestoreProperty("landownerId")]
        public string LandownerId { get; set; }

        [FirestoreProperty("status")]
        public string Status { get; set; }

        [FirestoreProperty("tenantId")]
        public string TenantId { get; set; }

        [FirestoreProperty("description")]
        public string Description { get; set; }

        internal string _TenantName { get; set; }
        public string TenantName => _TenantName;
    }
}