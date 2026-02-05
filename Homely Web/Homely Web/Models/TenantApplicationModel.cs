using Google.Cloud.Firestore;
using System; 

namespace Homely_Web.Models
{
    [FirestoreData]
    public class TenantApplicationModel
    {
        [FirestoreDocumentId]
        public string Id { get; set; }

        [FirestoreProperty("applicantId")]
        public string ApplicantId { get; set; }

        [FirestoreProperty("applicantName")]
        public string ApplicantName { get; set; }

        [FirestoreProperty("applicantEmail")]
        public string ApplicantEmail { get; set; }

        [FirestoreProperty("applicantPhone")]
        public string ApplicantPhone { get; set; }

        [FirestoreProperty("propertyId")]
        public string PropertyId { get; set; }

        [FirestoreProperty("status")]
        public string Status { get; set; }

        [FirestoreProperty("idUrl")]
        public string IdUrl { get; set; }

        [FirestoreProperty("payslipUrl")]
        public string PayslipUrl { get; set; }

        [FirestoreProperty("timestamp")]
        public Timestamp Timestamp { get; set; }

        internal string _PropertyName { get; set; } = "Loading...";
        public string PropertyName => _PropertyName;
    }
}