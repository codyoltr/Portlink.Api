namespace Portlink.Api.Modules.Common.Dtos;
/// <summary>Sayfalanmış liste response wrapper</summary>
public class PaginatedResponse<T>
{
    public IEnumerable<T> Items { get; set; } = Enumerable.Empty<T>();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public bool HasNext => Page < TotalPages;
    public bool HasPrevious => Page > 1;
}

/// <summary>Standart API başarı/hata response</summary>
public class ApiResponse<T>
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public T? Data { get; set; }

    public static ApiResponse<T> Ok(T data, string? message = null) =>
        new() { Success = true, Data = data, Message = message };

    public static ApiResponse<T> Fail(string message) =>
        new() { Success = false, Message = message };
}

public class ApiResponse : ApiResponse<object>
{
    public static ApiResponse Ok(string? message = null) =>
        new() { Success = true, Message = message };

    public new static ApiResponse Fail(string message) =>
        new() { Success = false, Message = message };
}

/// <summary>Liman listesi response</summary>
public class PortResponse
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Region { get; set; }
    public string? Coordinates { get; set; }
}

/// <summary>Hizmet kategorisi response</summary>
public class ServiceCategoryResponse
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public List<string> SubServices { get; set; } = new();
}

public class WalletResponse
{
    public decimal TotalEarnings { get; set; }
    public decimal PendingEarnings { get; set; }
    public decimal CompletedEarnings { get; set; }
    public List<WalletTransactionResponse> Transactions { get; set; } = new();
}

public class WalletTransactionResponse
{
    public Guid Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
}
