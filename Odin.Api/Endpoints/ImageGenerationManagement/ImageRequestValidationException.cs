namespace Odin.Api.Endpoints.ImageGenerationManagement;

/// <summary>
/// Raised by the service when a request violates a configurable guardrail (e.g. exceeds
/// <c>ImageGenerationLimitsOptions.MaxImagesPerRequest</c>, references a missing reference image). The
/// endpoint surfaces it as a 400. Distinct from the static contract validation so the limit checks can use
/// the injected options.
/// </summary>
public sealed class ImageRequestValidationException(string message) : Exception(message);
