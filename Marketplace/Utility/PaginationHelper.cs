namespace Marketplace.Utility
{
    // PaginationHelper
    // Small, testable helper that decides which page numbers to show.
    // We keep it outside the view so we can reason about edge cases in one place.

    public static class PaginationHelper
    {
        // GetDisplayPages
        // Builds the list of page indexes to render.
        // current is zero based, total is the number of pages.
        // window is how many pages we show around the current one.
        // We always keep the first and last page visible, and use null as a gap for ellipsis.
        // Examples:
        //   total=10, current=9, window=5 -> [0, null, 5,6,7,8,9]
        //   total=20, current=9, window=5 -> [0, null, 7,8,9,10,11, null, 19]
        public static List<int?> GetDisplayPages(int current, int total, int window = 5)
        {
            var result = new List<int?>();

            if (total <= 1)
            {
                return result;
            }

            // If there are only a few pages, just show them all.
            if (total <= 7)
            {
                for (int i = 0; i < total; i++)
                {
                    result.Add(i);
                }

                return result;
            }

            // Always start with the first page.
            result.Add(0);

            // Work out the window around the current page.
            int half = window / 2;

            int windowStart = current - half;
            int windowEnd = current + half;

            // Keep the window inside the inner range (1 to total-2).
            if (windowStart < 1)
            {
                windowEnd += 1 - windowStart;
                windowStart = 1;
            }

            if (windowEnd > total - 2)
            {
                windowStart -= windowEnd - (total - 2);
                windowEnd = total - 2;
            }

            // Clamp again in case total is tiny.
            if (windowStart < 1)
            {
                windowStart = 1;
            }

            if (windowEnd > total - 2)
            {
                windowEnd = total - 2;
            }

            // Left gap.
            if (windowStart > 1)
            {
                result.Add(null);
            }
            else if (windowStart == 1)
            {
                // No gap needed, but we already added 0, so we will add 1 naturally.
            }

            for (int i = windowStart; i <= windowEnd; i++)
            {
                result.Add(i);
            }

            // Right gap.
            if (windowEnd < total - 2)
            {
                result.Add(null);
            }

            // Always end with the last page.
            result.Add(total - 1);

            // Remove duplicates and tighten up when current is at the very start or end.
            // Also avoid double ellipsis when window touches the edges.
            var cleaned = new List<int?>();
            bool hasPrevNull = false;

            foreach (var p in result)
            {
                if (p == null)
                {
                    if (!hasPrevNull)
                    {
                        cleaned.Add(null);
                        hasPrevNull = true;
                    }

                    continue;
                }

                // Skip duplicate page numbers.
                if (cleaned.Count > 0 && cleaned[cleaned.Count - 1] == p)
                {
                    continue;
                }

                cleaned.Add(p);
                hasPrevNull = false;
            }

            return cleaned;
        }

        // ClampPage
        // Makes sure a page number stays inside 0 to total-1.
        // Useful on the server before querying.
        public static int ClampPage(int page, int total)
        {
            if (total <= 0)
            {
                return 0;
            }

            if (page < 0)
            {
                return 0;
            }

            if (page >= total)
            {
                return total - 1;
            }

            return page;
        }
    }
}
