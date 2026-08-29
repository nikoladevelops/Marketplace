using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace Marketplace.ViewModels
{
    // View model for editing an existing ad.
    public class EditAdvertisementViewModel
    {
        public int Id { get; set; }

        // Optional new main image
        public IFormFile? Image { get; set; }

        // Base64 fallback for a newly picked main image that has not been saved yet.
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

        // Owner id - never bound from client, server keeps original ad.UserId.
        [BindNever]
        public string? UserId { get; set; }

        [Required(ErrorMessage = "You need to select a category.")]
        [Range(1, int.MaxValue, ErrorMessage = "You need to select a category.")]
        public int CategoryId { get; set; }

        public IEnumerable<SelectListItem>? CategoryDropDown { get; set; }

        // Optional new extra images
        public IFormFile? AdditionalImage1 { get; set; }

        public IFormFile? AdditionalImage2 { get; set; }

        public IFormFile? AdditionalImage3 { get; set; }

        // Base64 fallbacks for newly picked extra images.
        public string? AdditionalImageBase64_1 { get; set; }

        public string? AdditionalImageBase64_2 { get; set; }

        public string? AdditionalImageBase64_3 { get; set; }

        public string? AdditionalImageFileName1 { get; set; }

        public string? AdditionalImageFileName2 { get; set; }

        public string? AdditionalImageFileName3 { get; set; }

        // Current images, shown so user knows what is already there
        public string? ExistingImagePath { get; set; }

        public IList<string>? ExistingAdditionalImagePaths { get; set; }
    }
}
