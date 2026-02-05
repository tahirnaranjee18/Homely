using Google.Cloud.Firestore;
using System.Collections.Generic;
using System.Linq;
using System; 

namespace Homely_Web.Models
{
    [FirestoreData]
    public class PropertyModel
    {
        [FirestoreDocumentId]
        public string Id { get; set; }

        [FirestoreProperty("ownerId")]
        public string OwnerId { get; set; }

        [FirestoreProperty("title")]
        public string Title { get; set; }

        [FirestoreProperty("location")]
        public string Location { get; set; }

        [FirestoreProperty("price")]
        public string Price { get; set; }

        [FirestoreProperty("leaseDuration")]
        public string LeaseDuration { get; set; }

        [FirestoreProperty("bedrooms")]
        public int Bedrooms { get; set; }

        [FirestoreProperty("bathrooms")]
        public int Bathrooms { get; set; }

        [FirestoreProperty("garages")]
        public int Garages { get; set; }

        [FirestoreProperty("rented")]
        public bool IsRented { get; set; }

        [FirestoreProperty("imageUrl")]
        public string ImageUrl { get; set; }

        [FirestoreProperty("imageUrls")]
        public List<string> ImageUrls { get; set; }

        [FirestoreProperty("createdAt")]
        public Timestamp CreatedAt { get; set; }

        public string DisplayImage => (ImageUrls != null && ImageUrls.Any())
                                            ? ImageUrls[0]
                                            : (ImageUrl ?? "/images/default-property.jpg");

        public override string ToString() => $"{Title} ({Location})";
    }
}