using System.ComponentModel.DataAnnotations;

namespace Odin.Api.Extensions
{
    public static class ValidationExtensions
    {
        private static IDictionary<string, string[]> ToValidationProblemErrors(this IEnumerable<ValidationResult> validationResults)
        {
            return validationResults
                .SelectMany(vr => vr.MemberNames.Select(mn => new { MemberName = mn, vr.ErrorMessage }))
                .GroupBy(x => x.MemberName)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(x => x.ErrorMessage ?? string.Empty).ToArray());
        }

        public static IResult? ValidateAndGetProblem<T>(this T request) where T : IValidatableObject
        {
            var validationResults = request.Validate(new ValidationContext(request)).ToList();

            return validationResults.Count != 0
                ? Results.ValidationProblem(validationResults.ToValidationProblemErrors())
                : null;
        }
    }
}
