namespace Homely_Web.Models
{
    public class TenantModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Email { get; set; } = "";
        public string Phone { get; set; } = "";
        public string EmergencyContact { get; set; } = "";
    }
}
