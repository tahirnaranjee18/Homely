using System.ComponentModel.DataAnnotations;

namespace Homely_Web.Models
{
    public class AdminLogin
    {
        [Required] public string Username { get; set; }
        [Required, MinLength(8)] public string Password { get; set; }
        public bool RememberMe { get; set; }
        public string Role { get; set; } = "Admin"; 
    }

    public class AdminRegister
    {
        [Required] public string FullName { get; set; }
        [Required, EmailAddress] public string Email { get; set; }
        [Required, MinLength(8)] public string Password { get; set; }
        [Compare("Password")] public string ConfirmPassword { get; set; }
    }

    public class AdminForgotPassword
    {
        [Required, EmailAddress] public string Email { get; set; }
    }
}
