namespace Marketplace.Models
{
    public class UserBlock
    {
        public int Id { get; set; }

        public string BlockerId { get; set; }
        public ApplicationUser Blocker { get; set; }

        public string BlockedId { get; set; }
        public ApplicationUser Blocked { get; set; }
    }
}
