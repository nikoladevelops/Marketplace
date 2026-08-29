namespace Marketplace.Models
{
    // ChatReport - a user reports a chat thread with another user.
    // One report per reporter per thread, so people cannot spam the same chat.
    public class ChatReport
    {
        public int Id { get; set; }

        // Who made the report
        public string ReporterId { get; set; } = "";

        public ApplicationUser Reporter { get; set; } = null!;

        // Who is being reported
        public string ReportedUserId { get; set; } = "";

        public ApplicationUser ReportedUser { get; set; } = null!;

        // Which ad this chat is about
        public int AdvertisementId { get; set; }

        public AdvertisementModel Advertisement { get; set; } = null!;

        // Stable key for the thread, e.g. "42:userA:userB" sorted, so we can enforce one report per thread
        public string ThreadKey { get; set; } = "";

        // Why the user is reporting
        public ReportReason Reason { get; set; }

        // Free text details, 20-500 chars
        public string Description { get; set; } = "";

        // Current state, resolved means an admin looked at it
        public ReportStatus Status { get; set; } = ReportStatus.Pending;

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        // Which admin resolved it, if any
        public string? ReviewedByAdminId { get; set; }

        public ApplicationUser? ReviewedByAdmin { get; set; }

        public DateTime? ReviewedAtUtc { get; set; }

        // What the admin decided to do
        public ReportAction? ActionTaken { get; set; }
    }

    // Reason for the report, shown as a dropdown in the modal
    public enum ReportReason
    {
        Spam = 0,
        Harassment = 1,
        Scam = 2,
        InappropriateContent = 3,
        Other = 4
    }

    // Simple lifecycle for a report
    public enum ReportStatus
    {
        Pending = 0,
        Resolved = 1
    }

    // What the admin did after reviewing
    public enum ReportAction
    {
        Dismissed = 0,
        Banned = 1
    }
}
