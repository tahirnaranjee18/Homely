using System.Text.Json.Serialization;

namespace Homely_Web.Models
{
    public class FirebaseAuthResponse
    {
        [JsonPropertyName("localId")]
        public string LocalId { get; set; } 

        [JsonPropertyName("email")]
        public string Email { get; set; }

        [JsonPropertyName("idToken")]
        public string IdToken { get; set; } 
    }
}