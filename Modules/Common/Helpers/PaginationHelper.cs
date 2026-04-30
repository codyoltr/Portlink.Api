using Portlink.Api.Modules.Common.Dtos;

namespace Portlink.Api.Helpers;

public static class PaginationHelper
{
    public static PaginatedResponse<T> Paginate<T>(
        IEnumerable<T> source,
        int page,
        int pageSize)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize < 1 ? 10 : pageSize > 100 ? 100 : pageSize;

        var total = source.Count();
        var items = source.Skip((page - 1) * pageSize).Take(pageSize);

        return new PaginatedResponse<T>
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }
}
