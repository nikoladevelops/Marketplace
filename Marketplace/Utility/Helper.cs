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
    }
}
