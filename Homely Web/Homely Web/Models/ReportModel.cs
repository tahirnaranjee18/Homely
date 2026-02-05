using Google.Cloud.Firestore;
using System;

namespace Homely_Web.Models
{
    [FirestoreData]
    public class ReportModel
    {
        [FirestoreDocumentId]
        public string Id { get; set; }
        [FirestoreProperty("title")]
        public string Title { get; set; }
        [FirestoreProperty("propertyAddress")]
        public string PropertyAddress { get; set; }
        [FirestoreProperty("assignedCaretakerId")]
        public string AssignedCaretakerId { get; set; }
        [FirestoreProperty("landownerId")]
        public string LandownerId { get; set; }
        [FirestoreProperty("status")]
        public string Status { get; set; }
        [FirestoreProperty("timestamp")]
        public Timestamp Timestamp { get; set; }

        internal string _AssignedCaretakerName { get; set; } = "N/A";

        public string AssignedCaretakerName => _AssignedCaretakerName;

        public string FormattedTimestamp => Timestamp.ToDateTime().ToString("dd MMM yyyy");
    }
}