namespace Marketplace.Utility
{
    public class Helper
    {
        public const string AdminRole = "Admin";
        public const string SellerRole = "Seller";
        public const string PremiumRole = "Premium";
        public static async Task<byte[]> GetByteArrayFromImage(IFormFile file)
        {
            using (var target = new MemoryStream())
            {
                await file.CopyToAsync(target);
                return target.ToArray();
            }
        }
    }
}
