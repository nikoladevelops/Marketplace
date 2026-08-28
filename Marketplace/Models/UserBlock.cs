namespace Marketplace.Models
{
    // Simple data holder for blocking.
    // One row means Blocker does not want to see or be seen by Blocked.
    public class UserBlock
    {
        public int Id { get; set; }

        // Who did the blocking
        public string BlockerId { get; set; } = "";

        public ApplicationUser Blocker { get; set; } = null!;

        // Who got blocked
        public string BlockedId { get; set; } = "";

        public ApplicationUser Blocked { get; set; } = null!;
    }
}
