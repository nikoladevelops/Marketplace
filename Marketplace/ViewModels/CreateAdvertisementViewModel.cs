using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace Marketplace.ViewModels
{
    // View model for creating a new ad.
    public class CreateAdvertisementViewModel
    {
        public int Id { get; set; }

        // Main image file. Required, but we also accept a Base64 fallback
        // so the preview survives a validation error without re-upload.
        public IFormFile? Image { get; set; }

        // Base64 data URL for the main image, kept client side and posted back on errors.
        // Example: "data:image/jpeg;base64,..."
        public string? MainImageBase64 { get; set; }

        public string? MainImageFileName { get; set; }

        [Required]
        [StringLength(35, MinimumLength = 3)]
        public string Title { get; set; } = "";

        [Required]
        [StringLength(250, MinimumLength = 20)]
        public string Description { get; set; } = "";

        [Required]
        [Range(1, 1000000)]
        [DataType(DataType.Currency)]
        [Display(Name = "Price (EUR)")]
        public decimal Price { get; set; }

        [Required(ErrorMessage = "You need to specify where you are located.")]
        [StringLength(100, MinimumLength = 2)]
        public string Location { get; set; } = "";

        public double? Latitude { get; set; }

        public double? Longitude { get; set; }

        [Required(ErrorMessage = "You need to select a category.")]
        [Range(1, int.MaxValue, ErrorMessage = "You need to select a category.")]
        public int CategoryId { get; set; }

        public IEnumerable<SelectListItem>? CategoryDropDown { get; set; }

        // Optional extra images
        public IFormFile? AdditionalImage1 { get; set; }

        public IFormFile? AdditionalImage2 { get; set; }

        public IFormFile? AdditionalImage3 { get; set; }

        // Base64 fallbacks for the three extra slots, same idea as MainImageBase64.
        public string? AdditionalImageBase64_1 { get; set; }

        public string? AdditionalImageBase64_2 { get; set; }

        public string? AdditionalImageBase64_3 { get; set; }

        public string? AdditionalImageFileName1 { get; set; }

        public string? AdditionalImageFileName2 { get; set; }

        public string? AdditionalImageFileName3 { get; set; }
    }
}
