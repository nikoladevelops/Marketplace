namespace Marketplace.Models
{
    // Tiny model just for the error page.
    // Shows a request id if we have one so we can trace the error.
    public class ErrorViewModel
    {
        public string? RequestId { get; set; }

        // Helper to know if we should show the request id
        public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
    }
}
