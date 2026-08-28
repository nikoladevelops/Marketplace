using System.ComponentModel.DataAnnotations;
using Marketplace.Models;

namespace Marketplace.Models
{
    // This is a single chat message between two users about an ad.
    // Each message belongs to one ad thread between a sender and receiver.
    public class ChatMessage
    {
        public int Id { get; set; }

        // The actual text of the message
        [Required]
        [StringLength(1000)]
        public string Body { get; set; } = "";

        // When it was sent
        public DateTime SentAt { get; set; }

        // Has the receiver read it yet
        public bool IsReadByReceiver { get; set; }

        // Who sent it
        public string SenderId { get; set; } = "";

        public ApplicationUser Sender { get; set; } = null!;

        // Who should receive it
        public string ReceiverId { get; set; } = "";

        public ApplicationUser Receiver { get; set; } = null!;

        // Which ad this chat is about
        public int AdvertisementId { get; set; }

        public AdvertisementModel Advertisement { get; set; } = null!;
    }
}
