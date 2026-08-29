using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Marketplace.Services
{
    // IAiImageService - contract for AI listing generation from images.
    public interface IAiImageService
    {
        // GenerateListingFromImagesAsync - builds a draft listing from uploaded images.
        Task<GeneratedListingDto?> GenerateListingFromImagesAsync(IEnumerable<IFormFile> imageFiles, CancellationToken cancellationToken);
    }
}
