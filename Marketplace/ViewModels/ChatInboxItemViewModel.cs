namespace Marketplace.ViewModels
{
    public class ChatInboxItemViewModel
    {
        public string PartnerName { get; set; } = "";
        public int AdvertisementId { get; set; }
        public string AdvertisementTitle { get; set; } = "";
        public string AdvertisementImagePath { get; set; } = "";
        public string Snippet { get; set; } = "";
        public DateTime LastSentAt { get; set; }
        public int UnreadCount { get; set; }
    }

    public class ChatInboxViewModel
    {
        public List<ChatInboxItemViewModel> Items { get; set; } = new();
        public int CurrentPage { get; set; } = 1;
        public int PageSize { get; set; } = 12;
        public int TotalCount { get; set; }
        public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
        public bool HasPrevious => CurrentPage > 1;
        public bool HasNext => CurrentPage < TotalPages;
    }
}
