namespace Odin.Api.Endpoints.Subscribe.Models;

/// <summary>Public pre-launch waitlist signup. Email is the only field we collect.</summary>
public sealed record SubscribeRequest(string Email);

/// <summary>Always reports success to the anonymous caller (see endpoint for why failures are masked).</summary>
public sealed record SubscribeResponse(bool Success);
