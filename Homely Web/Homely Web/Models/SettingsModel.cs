namespace Homely_Web.Models
{
    public class SettingsModel
    {
        public string FullName { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public string Theme { get; set; }
        public string Notifications { get; set; }
        public DateTime? PasswordChangedOn { get; set; }
    }
}
