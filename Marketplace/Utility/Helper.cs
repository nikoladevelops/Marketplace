namespace Marketplace.Utility
{
    public class Helper
    {
        public static string AdminRole = "Admin";
        public static string SellerRole = "Seller";

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
