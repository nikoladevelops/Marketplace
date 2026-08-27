namespace Marketplace.ViewModels
{
    public class MyAdvertisementsViewModel
    {
        public IEnumerable<SimplifiedAdvertisementViewModel> Advertisements { get; set; } = Enumerable.Empty<SimplifiedAdvertisementViewModel>();
        public int PageNumber { get; set; }
        public int MaxCountPages { get; set; }
        public int TotalCount { get; set; }
        public int PageSize { get; set; }
    }
}
