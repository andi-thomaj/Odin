namespace Odin.Api.Data.Enums;

/// <summary>
/// Which OpenAI image endpoint a job uses. <see cref="Generation"/> → <c>/v1/images/generations</c>
/// (text-only prompt); <see cref="Edit"/> → <c>/v1/images/edits</c> (prompt + one or more uploaded
/// reference images). Stored as a string (<c>HasConversion&lt;string&gt;</c>).
/// </summary>
public enum ImageGenerationMode
{
    Generation,
    Edit,
}
