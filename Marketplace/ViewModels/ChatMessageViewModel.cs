namespace Marketplace.ViewModels
{
    // One message in a chat thread, shaped for the UI.
    public class ChatMessageViewModel
    {
        public int Id { get; set; }

        public string Body { get; set; } = "";

        public DateTime SentAt { get; set; }

        public bool IsMine { get; set; }

        public bool IsReadByReceiver { get; set; }

        public string SenderName { get; set; } = "";
    }
}
