namespace Marketplace.ViewModels
{
    public class ChatThreadViewModel
    {
        public string PartnerName { get; set; } = "";
        public string MyUserName { get; set; } = "";
        public bool IsPartnerAdmin { get; set; }

        public int AdvertisementId { get; set; }
        public string AdvertisementTitle { get; set; } = "";
        public string AdvertisementPrice { get; set; } = "";
        public string AdvertisementImagePath { get; set; } = "";

        public System.Collections.Generic.List<ChatMessageViewModel> Messages { get; set; }
            = new System.Collections.Generic.List<ChatMessageViewModel>();

        public bool IsBlockedByMe { get; set; }
        public bool HasBlockedMe { get; set; }
        public bool CanSend { get; set; }

        // Pagination for long threads
        public int CurrentPage { get; set; } = 1;
        public int PageSize { get; set; } = 50;
        public int TotalCount { get; set; }
        public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
        public bool HasPrevious => CurrentPage > 1;
        public bool HasNext => CurrentPage < TotalPages;
        public bool HasOlder => HasPrevious;
    }
}
