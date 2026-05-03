using Odin.Api.Data.Enums;

namespace Odin.Api.Endpoints.CalculatorManagement.Models;

public static class GetCalculatorContract
{
    public class Response
    {
        public int Id { get; set; }
        public required string Label { get; set; }
        public required string Coordinates { get; set; }
        public CalculatorType Type { get; set; }
        public bool IsAdmin { get; set; }
        public int UserId { get; set; }
        public string? UserEmail { get; set; }
        public string? UserUsername { get; set; }
    }
}

public static class CreateCalculatorContract
{
    public class Request
    {
        public required string Label { get; set; }
        public required string Coordinates { get; set; }
        public CalculatorType Type { get; set; }
    }
}

public static class UpdateCalculatorContract
{
    public class Request
    {
        public required string Label { get; set; }
        public required string Coordinates { get; set; }
        public CalculatorType Type { get; set; }
    }
}
