using System.ComponentModel.DataAnnotations;
using Marketplace.Models;

namespace Marketplace.Models
{
    public class ChatMessage
    {
        public int Id { get; set; }

        [Required]
        [StringLength(1000)]
        public string Body { get; set; }

        public DateTime SentAt { get; set; }
        public bool IsReadByReceiver { get; set; }

        public string SenderId { get; set; }
        public ApplicationUser Sender { get; set; }

        public string ReceiverId { get; set; }
        public ApplicationUser Receiver { get; set; }

        public int AdvertisementId { get; set; }
        public AdvertisementModel Advertisement { get; set; }
    }
}
