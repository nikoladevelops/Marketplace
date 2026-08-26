using Marketplace.Models;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Services
{
    public class AdvertisementFilterService
    {
        public IQueryable<AdvertisementModel> Apply(
            IQueryable<AdvertisementModel> query,
            string? searchTerm,
            string? location,
            int? categoryId,
            string? minPriceStr,
            string? maxPriceStr)
        {
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = $"%{searchTerm.Trim()}%";
                query = query.Where(x =>
                    EF.Functions.ILike(x.Title, term) ||
                    EF.Functions.ILike(x.Description, term));
            }

            if (!string.IsNullOrWhiteSpace(location))
            {
                var loc = $"%{location.Trim()}%";
                query = query.Where(x => EF.Functions.ILike(x.Location, loc));
            }

            if (categoryId.HasValue && categoryId.Value > 0)
            {
                query = query.Where(x => x.CategoryId == categoryId.Value);
            }

            decimal? min = null;
            decimal? max = null;

            if (!string.IsNullOrWhiteSpace(minPriceStr) && decimal.TryParse(minPriceStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var pMin))
                min = pMin;
            if (!string.IsNullOrWhiteSpace(maxPriceStr) && decimal.TryParse(maxPriceStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var pMax))
                max = pMax;

            if (min.HasValue && max.HasValue && min > max)
            {
                var tmp = min;
                min = max;
                max = tmp;
            }

            if (min.HasValue)
                query = query.Where(x => x.Price >= min.Value);
            if (max.HasValue)
                query = query.Where(x => x.Price <= max.Value);

            return query;
        }

        public IOrderedQueryable<AdvertisementModel> ApplySorting(IQueryable<AdvertisementModel> query, string filter)
        {
            filter = (filter ?? "new").ToLowerInvariant();
            return filter switch
            {
                "new" => query.OrderByDescending(x => x.DateCreatedOn),
                "old" => query.OrderBy(x => x.DateCreatedOn),
                "cheapest" => query.OrderBy(x => x.Price),
                "most expensive" => query.OrderByDescending(x => x.Price),
                _ => query.OrderByDescending(x => x.DateCreatedOn)
            };
        }
    }
}
