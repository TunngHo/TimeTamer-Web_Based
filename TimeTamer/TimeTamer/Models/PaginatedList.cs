using Microsoft.EntityFrameworkCore;

namespace TimeTamer.Models
{
    public interface IPaginatedList
    {
        int PageIndex { get; }
        int TotalPages { get; }
        int TotalItems { get; }
        int PageSize { get; }
        bool HasPreviousPage { get; }
        bool HasNextPage { get; }
    }

    public class PaginatedList<T> : List<T>, IPaginatedList
    {
        public int PageIndex { get; }
        public int TotalPages { get; }
        public int TotalItems { get; }
        public int PageSize { get; }
        public bool HasPreviousPage => PageIndex > 1;
        public bool HasNextPage => PageIndex < TotalPages;

        private PaginatedList(IEnumerable<T> items, int count, int pageIndex, int pageSize)
        {
            PageIndex = Math.Max(1, pageIndex);
            PageSize = Math.Max(1, pageSize);
            TotalItems = count;
            TotalPages = Math.Max(1, (int)Math.Ceiling(count / (double)PageSize));
            AddRange(items);
        }

        public static async Task<PaginatedList<T>> CreateAsync(IQueryable<T> source, int pageIndex, int pageSize, int minPageSize = 2)
        {
            pageIndex = Math.Max(1, pageIndex);
            pageSize = Math.Clamp(pageSize, Math.Max(1, minPageSize), 50);
            var count = await source.CountAsync();
            var totalPages = Math.Max(1, (int)Math.Ceiling(count / (double)pageSize));
            pageIndex = Math.Min(pageIndex, totalPages);
            var items = await source.Skip((pageIndex - 1) * pageSize).Take(pageSize).ToListAsync();
            return new PaginatedList<T>(items, count, pageIndex, pageSize);
        }

        public static PaginatedList<T> Create(IEnumerable<T> source, int pageIndex, int pageSize)
        {
            pageIndex = Math.Max(1, pageIndex);
            pageSize = Math.Clamp(pageSize, 2, 50);
            var list = source.ToList();
            var count = list.Count;
            var totalPages = Math.Max(1, (int)Math.Ceiling(count / (double)pageSize));
            pageIndex = Math.Min(pageIndex, totalPages);
            var items = list.Skip((pageIndex - 1) * pageSize).Take(pageSize);
            return new PaginatedList<T>(items, count, pageIndex, pageSize);
        }
    }
}



