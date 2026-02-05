using Google.Cloud.Firestore;
using System;

namespace Homely_Web.Models
{
    [FirestoreData]
    public class ReportCommentModel
    {
        [FirestoreDocumentId]
        public string Id { get; set; }

        [FirestoreProperty("authorId")]
        public string AuthorId { get; set; }

        [FirestoreProperty("authorName")]
        public string AuthorName { get; set; }

        [FirestoreProperty("text")]
        public string Text { get; set; }

        [FirestoreProperty("imageUrl")]
        public string ImageUrl { get; set; }

        [FirestoreProperty("timestamp")]
        public Timestamp Timestamp { get; set; }
    }
}