using Marketplace.Models;

namespace Marketplace.ViewModels
{
    // View model for admin to inspect a reported chat thread.
    // Shows the report details plus the full message history between reporter and reported user.
    public class AdminChatLogViewModel
    {
        // The report that triggered the review
        public ChatReport Report { get; set; } = null!;

        // All messages between the two users for this ad (oldest first)
        public List<ChatMessageViewModel> Messages { get; set; } = new List<ChatMessageViewModel>();

        // Denormalized names for easy rendering
        public string ReporterName { get; set; } = "";

        public string ReportedName { get; set; } = "";

        public string AdTitle { get; set; } = "";

        public string AdImagePath { get; set; } = "";

        public int AdvertisementId { get; set; }
    }
}
