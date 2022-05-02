using System.ComponentModel.DataAnnotations;

namespace Marketplace.ViewModels
{
    public class RegisterViewModel
    {
        [Required]
        [StringLength(15,ErrorMessage ="Username can NOT be more than 15 characters long.")]
        public string Username { get; set; }

        [Required]
        [StringLength(30,ErrorMessage = "Email can NOT be more than 30 characters long.")]
        [DataType(DataType.EmailAddress)]
        public string Email { get; set; }

        [Required]
        [StringLength(30, ErrorMessage = "Password can NOT be more than 30 characters long.")]
        [DataType(DataType.Password)]
        public string Password { get; set; }

        [Required]
        [Display(Name ="Confirm Password")]
        [StringLength(30)]
        [DataType(DataType.Password)]
        [Compare("Password",ErrorMessage ="Password and confirmation password don't match.")]
        public string ConfirmPassword { get; set; }

        [Range(typeof(bool), "true", "true", ErrorMessage = "You have to agree to our terms and conditions!")]
        [Display(Name ="I agree to the terms and conditions.")]
        public bool AgreeToTerms { get; set; }
    }
}
