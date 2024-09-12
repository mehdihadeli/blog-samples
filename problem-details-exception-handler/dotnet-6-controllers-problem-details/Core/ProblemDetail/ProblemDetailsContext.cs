using Microsoft.AspNetCore.Mvc;

namespace DotNet6ProblemDetails.Core.ProblemDetail;

public sealed class ProblemDetailsContext
{
    private ProblemDetails? _problemDetails;

    /// <summary>
    /// The <see cref="HttpContext"/> associated with the current request being processed by the filter.
    /// </summary>
    public HttpContext HttpContext { get; init; }

    /// <summary>
    /// A collection of additional arbitrary metadata associated with the current request endpoint.
    /// </summary>
    public EndpointMetadataCollection? AdditionalMetadata { get; init; }

    /// <summary>
    /// An instance of <see cref="ProblemDetails"/> that will be
    /// used during the response payload generation.
    /// </summary>
    public ProblemDetails ProblemDetails
    {
        get => _problemDetails ??= new ProblemDetails();
        init => _problemDetails = value;
    }
}