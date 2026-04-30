namespace Odin.Api.Services.Paddle.Models.Common;

/// <summary>Envelope returned by Paddle for single-entity GETs and writes: <c>{ "data": ..., "meta": ... }</c>.</summary>
public sealed class PaddleEnvelope<T> where T : class
{
    public T? Data { get; set; }
    public PaddleMeta? Meta { get; set; }
}

/// <summary>Envelope returned by Paddle list endpoints: <c>{ "data": [...], "meta": { "pagination": ... } }</c>.</summary>
public sealed class PaddleListEnvelope<T> where T : class
{
    public List<T> Data { get; set; } = [];
    public PaddleListMeta? Meta { get; set; }
}

public sealed class PaddleMeta
{
    public string? RequestId { get; set; }
}

public sealed class PaddleListMeta
{
    public string? RequestId { get; set; }
    public PaddlePagination? Pagination { get; set; }
}

public sealed class PaddlePagination
{
    public int PerPage { get; set; }

    /// <summary>Absolute URL containing the next-page query parameters (cursor + filters), or empty when <c>HasMore</c> is false.</summary>
    public string? Next { get; set; }

    public bool HasMore { get; set; }

    public int EstimatedTotal { get; set; }
}

/// <summary>Top-level envelope used by error responses: <c>{ "error": ..., "meta": ... }</c>.</summary>
public sealed class PaddleErrorEnvelope
{
    public PaddleErrorBody? Error { get; set; }
    public PaddleMeta? Meta { get; set; }
}

public sealed class PaddleErrorBody
{
    public string? Type { get; set; }
    public string? Code { get; set; }
    public string? Detail { get; set; }
    public string? DocumentationUrl { get; set; }
    public List<PaddleErrorFieldEntry>? Errors { get; set; }
}

public sealed class PaddleErrorFieldEntry
{
    public string? Field { get; set; }
    public string? Message { get; set; }
}

/// <summary>Money primitive used everywhere in Paddle: smallest currency unit as a string (e.g. <c>"4999"</c> = $49.99).</summary>
public sealed class PaddleMoney
{
    public string Amount { get; set; } = "0";
    public string CurrencyCode { get; set; } = "";
}

public sealed class PaddleImportMeta
{
    public string? ExternalId { get; set; }
    public string? ImportedFrom { get; set; }
}
