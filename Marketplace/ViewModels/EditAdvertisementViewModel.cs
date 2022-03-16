using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace Marketplace.ViewModels
{
    public class EditAdvertisementViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Primary image is required.")]
        public IFormFile Image { get; set; }

        [Required]
        [StringLength(35,MinimumLength = 3)]
        public string Title { get; set; }

        [Required]
        [StringLength(250, MinimumLength = 20)]
        public string Description { get; set; }

        [Required]
        [Range(1,1000000)]
        [DataType(DataType.Currency)]
        public double Price { get; set; }

        [Required(ErrorMessage = "You need to specify where you are located.")]
        [StringLength(15,MinimumLength =4)]
        public string Location { get; set; }

        public string? UserId { get; set; }

        // hardcoded value 9 for amount of categories available
        [Required(ErrorMessage = "You need to select a category.")]
        [Range(1,9, ErrorMessage = "You need to select a category.")]
        public int CategoryId { get; set; }

        public IEnumerable<SelectListItem>? CategoryDropDown { get; set; }

        public IEnumerable<IFormFile>? AdditionalImages { get; set; }

        public byte[]? ImageInBytes { get; set; }

        public IList<byte[]>? AdditionalImagesInBytes { get; set; }

    }
}
