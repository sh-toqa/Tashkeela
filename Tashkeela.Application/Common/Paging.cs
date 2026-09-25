using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace Tashkeela.Application.Common;

/// <summary>API-4: <c>?page=&amp;pageSize=</c> on every list endpoint. Validated like any other request.</summary>
public sealed class PageQuery
{
    public const int MaxPageSize = 100;

    [Range(1, int.MaxValue)]
    public int Page { get; init; } = 1;

    [Range(1, MaxPageSize)]
    public int PageSize { get; init; } = 20;
}

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => (TotalCount + PageSize - 1) / PageSize;
}

internal static class PagingExtensions
{
    /// <summary>A COUNT and a page query. <paramref name="query"/> must already be ordered deterministically.</summary>
    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(this IQueryable<T> query, PageQuery page, CancellationToken ct)
    {
        var total = await query.CountAsync(ct);
        var items = await query.Skip((page.Page - 1) * page.PageSize).Take(page.PageSize).ToListAsync(ct);
        return new PagedResult<T>(items, page.Page, page.PageSize, total);
    }
}
