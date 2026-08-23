using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Marketplace.Services
{
    public interface IAiService
    {
        Task<GeneratedListingDto?> GenerateListingFromImagesAsync(IEnumerable<IFormFile> imageFiles, CancellationToken cancellationToken);
    }
}