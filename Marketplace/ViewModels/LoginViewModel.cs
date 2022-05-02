using System.ComponentModel.DataAnnotations;

namespace Marketplace.ViewModels
{
    public class LoginViewModel
    {
        [Required]
        [StringLength(15,ErrorMessage ="Username can NOT be more than 15 characters long.")]
        public string Username { get; set; }

        [Required]
        [StringLength(30, ErrorMessage = "Password can NOT be more than 100 characters long.")]
        [DataType(DataType.Password)]
        public string Password { get; set; }

        [Display(Name ="Remember Me")]
        public bool RememberMe { get; set; }
    }
}
