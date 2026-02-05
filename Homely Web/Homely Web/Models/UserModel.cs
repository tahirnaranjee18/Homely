using Google.Cloud.Firestore;

namespace Homely_Web.Models
{
    [FirestoreData]
    public class UserModel
    {
        [FirestoreDocumentId]
        public string Uid { get; set; }

        [FirestoreProperty("fullName")]
        public string FullName { get; set; }

        [FirestoreProperty("email")]
        public string Email { get; set; }

        [FirestoreProperty("role")]
        public string Role { get; set; }

        [FirestoreProperty("status")]
        public string Status { get; set; }

    }
}