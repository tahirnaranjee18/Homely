using Google.Cloud.Firestore;
using System;

namespace Homely_Web.Models
{
    [FirestoreData]
    public class PaymentModel
    {
        [FirestoreDocumentId]
        public string Id { get; set; }

        [FirestoreProperty("billId")]
        public string BillId { get; set; }

        [FirestoreProperty("description")]
        public string Description { get; set; }

        [FirestoreProperty("imageUrl")]
        public string ImageUrl { get; set; }

        [FirestoreProperty("landownerId")]
        public string LandownerId { get; set; }

        [FirestoreProperty("paymentType")]
        public string PaymentType { get; set; }

        [FirestoreProperty("status")]
        public string Status { get; set; }

        [FirestoreProperty("timestamp")]
        public Timestamp Timestamp { get; set; }

        [FirestoreProperty("userId")]
        public string UserId { get; set; }

        internal string _TenantName { get; set; }
        public string TenantName => _TenantName;
    }
}