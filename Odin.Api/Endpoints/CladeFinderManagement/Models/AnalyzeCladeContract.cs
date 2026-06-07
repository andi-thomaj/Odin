using System.ComponentModel.DataAnnotations;

namespace Odin.Api.Endpoints.CladeFinderManagement.Models
{
    public class AnalyzeCladeContract
    {
        public class Request : IValidatableObject
        {
            /// <summary>Raw genetic data file: 23andMe/AncestryDNA text or VCF (.txt/.csv/.vcf, optionally .zip/.gz).</summary>
            public required IFormFile File { get; set; }

            /// <summary>Genome build of the upload: "hg19" or "hg38". Optional; the tools API defaults it.</summary>
            public string? Build { get; set; }

            public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
            {
                var allowedExtensions = new[] { ".txt", ".csv", ".vcf", ".zip", ".gz" };
                const long maxFileSize = 50 * 1024 * 1024; // 50 MB

                if (File is null || File.Length == 0)
                {
                    yield return new ValidationResult("File is required.", [nameof(File)]);
                    yield break;
                }

                var extension = Path.GetExtension(File.FileName).ToLowerInvariant();
                if (!allowedExtensions.Contains(extension))
                {
                    yield return new ValidationResult(
                        $"Invalid file type. Allowed types: {string.Join(", ", allowedExtensions)}",
                        [nameof(File)]);
                }

                if (File.Length > maxFileSize)
                {
                    yield return new ValidationResult(
                        $"File size exceeds the maximum allowed size of {maxFileSize / (1024 * 1024)} MB.",
                        [nameof(File)]);
                }

                if (Build is not null && Build is not ("hg19" or "hg38"))
                {
                    yield return new ValidationResult("Build must be 'hg19' or 'hg38'.", [nameof(Build)]);
                }
            }
        }

        public class Response
        {
            /// <summary>Best-matching Y-DNA haplogroup clade, or null when it could not be determined.</summary>
            public string? Clade { get; set; }

            /// <summary>Score of the best clade (higher is better).</summary>
            public double? Score { get; set; }

            /// <summary>Runner-up clade, useful to gauge confidence.</summary>
            public NextPrediction? NextPrediction { get; set; }

            /// <summary>Immediate sub-clades below the predicted clade.</summary>
            public IReadOnlyList<DownstreamClade> Downstream { get; set; } = [];

            /// <summary>Paternal haplogroup lineage, ordered backbone-most → terminal (last element == Clade).</summary>
            public IReadOnlyList<string> Lineage { get; set; } = [];

            /// <summary>Non-fatal note, e.g. conflicting SNP calls.</summary>
            public string? Warning { get; set; }

            /// <summary>Set when a clade could not be determined from otherwise-valid input.</summary>
            public string? Error { get; set; }

            /// <summary>Count of unique positive SNP calls parsed.</summary>
            public int PositivesUsed { get; set; }

            /// <summary>Count of unique negative SNP calls parsed.</summary>
            public int NegativesUsed { get; set; }

            /// <summary>Y-chromosome reads seen while parsing the upload.</summary>
            public int? YReads { get; set; }

            /// <summary>Detected upload format: "vcf" or "microarray".</summary>
            public string? SourceFormat { get; set; }

            /// <summary>Genome build actually used ("hg19" or "hg38"), auto-detected from the file.</summary>
            public string? EffectiveBuild { get; set; }
        }

        public class NextPrediction
        {
            public required string Clade { get; set; }
            public double Score { get; set; }
        }

        public class DownstreamClade
        {
            public required string Clade { get; set; }
            public int? Children { get; set; }
        }
    }
}
