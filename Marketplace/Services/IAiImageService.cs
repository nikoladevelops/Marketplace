using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Marketplace.Services
{
    // IAiImageService - contract for AI listing generation from images.
    // Expected input is 1 main image (required, slot 1) plus 0 to 3 optional extra images (slots 2 to 4).
    // The main image decides Title and CategoryId. Extras only enrich the Description.
    public interface IAiImageService
    {
        // GenerateListingFromImagesAsync - builds a draft listing from the image slots.
        // The caller must pass images in slot order: main first, then extras.
        Task<GeneratedListingDto?> GenerateListingFromImagesAsync(IEnumerable<IFormFile> imageFiles, CancellationToken cancellationToken);
    }
}
