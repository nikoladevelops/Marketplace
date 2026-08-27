namespace Marketplace.Utility
{
    public class Helper
    {
        public const string AdminRole = "Admin";
        public const string SellerRole = "Seller";
        public const string PremiumRole = "Premium";

        public const int SellerMaxAds = 20;
        public const int PremiumMaxAds = 40;

        public static int MaxAdsForRoles(bool isPremium) => isPremium ? PremiumMaxAds : SellerMaxAds;
        public static async Task<byte[]> GetByteArrayFromImage(IFormFile file)
        {
            using (var target = new MemoryStream())
            {
                await file.CopyToAsync(target);
                return target.ToArray();
            }
        }
        
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
