namespace Marketplace.Utility
{
    // Small helper for roles, limits and image file handling.
    // Keeps common constants and file utilities in one place.
    public class Helper
    {
        public const string AdminRole = "Admin";
        public const string SellerRole = "Seller";
        public const string PremiumRole = "Premium";

        public const int SellerMaxAds = 20;
        public const int PremiumMaxAds = 40;

        // Returns the max ads allowed for the given role.
        // Premium users get a higher limit.
        public static int MaxAdsForRoles(bool isPremium)
        {
            return isPremium ? PremiumMaxAds : SellerMaxAds;
        }

        // Reads an uploaded file into a byte array.
        // Useful when you need to store or process the file in memory.
        public static async Task<byte[]> GetByteArrayFromImage(IFormFile file)
        {
            using (var target = new MemoryStream())
            {
                await file.CopyToAsync(target);

                return target.ToArray();
            }
        }

        // Saves an uploaded image to wwwroot/uploads/{subFolder}.
        // Creates the folder if needed and gives the file a unique name.
        // Returns the public path like /uploads/advertisements/xxx.jpg or null if no file.
        public static async Task<string?> SaveImageAsync(IFormFile? imageFile, string subFolder, IWebHostEnvironment webHostEnvironment)
        {
            if (imageFile == null || imageFile.Length == 0)
            {
                return null;
            }

            string uploadsFolder = Path.Combine(webHostEnvironment.WebRootPath, "uploads", subFolder);

            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            string uniqueFileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(imageFile.FileName);
            string filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await imageFile.CopyToAsync(fileStream);
            }

            return $"/uploads/{subFolder}/{uniqueFileName}";
        }

        // Deletes an image file from disk if it exists.
        // Safe to call with null or empty paths.
        public static void DeleteImage(string? imagePath, IWebHostEnvironment webHostEnvironment)
        {
            if (string.IsNullOrEmpty(imagePath))
            {
                return;
            }

            string fullPath = Path.Combine(webHostEnvironment.WebRootPath, imagePath.TrimStart('/'));

            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }
        }

        // TryDecodeBase64DataUrl
        // Turns a data URL like "data:image/jpeg;base64,..." into raw bytes.
        // Returns null if the string is not a valid data URL.
        public static byte[]? TryDecodeBase64DataUrl(string? dataUrl, out string? extension)
        {
            extension = null;

            if (string.IsNullOrWhiteSpace(dataUrl))
            {
                return null;
            }

            var trimmed = dataUrl.Trim();

            if (!trimmed.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var commaIndex = trimmed.IndexOf(',');

            if (commaIndex < 0)
            {
                return null;
            }

            var meta = trimmed.Substring(5, commaIndex - 5);
            var base64Part = trimmed.Substring(commaIndex + 1);

            // Guess extension from mime type, e.g. "jpeg" or "png".
            var mime = meta.Split(';')[0];

            if (mime.Contains('/'))
            {
                var ext = mime.Split('/')[1].ToLowerInvariant();

                if (ext == "jpeg")
                {
                    ext = "jpg";
                }

                extension = "." + ext;
            }
            else
            {
                extension = ".jpg";
            }

            try
            {
                return Convert.FromBase64String(base64Part);
            }
            catch
            {
                return null;
            }
        }

        // SaveBase64ImageAsync
        // Saves a Base64 data URL to disk the same way SaveImageAsync does.
        // Used when the form was re-posted after a validation error and we only have the preview data.
        public static async Task<string?> SaveBase64ImageAsync(string? dataUrl, string? originalFileName, string subFolder, IWebHostEnvironment webHostEnvironment)
        {
            var bytes = TryDecodeBase64DataUrl(dataUrl, out var ext);

            if (bytes == null || bytes.Length == 0)
            {
                return null;
            }

            // Dont allow huge payloads to fill the disk (limit ~5MB per image).
            if (bytes.Length > 5 * 1024 * 1024)
            {
                return null;
            }

            string uploadsFolder = Path.Combine(webHostEnvironment.WebRootPath, "uploads", subFolder);

            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            string safeName = string.IsNullOrWhiteSpace(originalFileName) ? "image" + ext : Path.GetFileName(originalFileName);

            // If the name has no extension, add the one we guessed.
            if (string.IsNullOrEmpty(Path.GetExtension(safeName)) && !string.IsNullOrEmpty(ext))
            {
                safeName += ext;
            }

            string uniqueFileName = Guid.NewGuid().ToString() + "_" + safeName;
            string filePath = Path.Combine(uploadsFolder, uniqueFileName);

            await File.WriteAllBytesAsync(filePath, bytes);

            return $"/uploads/{subFolder}/{uniqueFileName}";
        }
    }
}
