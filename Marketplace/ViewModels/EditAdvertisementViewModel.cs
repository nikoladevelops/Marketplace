using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace Marketplace.ViewModels
{
    public class EditAdvertisementViewModel
    {
        public int Id { get; set; }

        public IFormFile? Image { get; set; }

        [Required]
        [StringLength(35, MinimumLength = 3)]
        public string Title { get; set; }

        [Required]
        [StringLength(250, MinimumLength = 20)]
        public string Description { get; set; }

        [Required]
        [Range(1, 1000000)]
        [DataType(DataType.Currency)]
        public double Price { get; set; }

        [Required(ErrorMessage = "You need to specify where you are located.")]
        [StringLength(100, MinimumLength = 2)]
        public string Location { get; set; }

        public double? Latitude { get; set; }
        public double? Longitude { get; set; }

        public string? UserId { get; set; }

        [Required(ErrorMessage = "You need to select a category.")]
        [Range(1, 9, ErrorMessage = "You need to select a category.")]
        public int CategoryId { get; set; }

        public IEnumerable<SelectListItem>? CategoryDropDown { get; set; }

        public IFormFile? AdditionalImage1 { get; set; }
        public IFormFile? AdditionalImage2 { get; set; }
        public IFormFile? AdditionalImage3 { get; set; }

        public string? ExistingImagePath { get; set; }
        public IList<string>? ExistingAdditionalImagePaths { get; set; }
    }
}