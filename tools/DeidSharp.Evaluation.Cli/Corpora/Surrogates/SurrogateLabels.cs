namespace DeidSharp.Evaluation.Cli.Corpora.Surrogates;

/// <summary>The gold labels the surrogate generator gives the spans it inserts.</summary>
internal static class SurrogateLabels
{
    public const string PatientName = "PatientName";
    public const string StaffName = "StaffName";
    public const string RelativeName = "RelativeName";

    /// <summary>A first name or a surname put where no rule recognized the context.</summary>
    public const string UntypedName = "UntypedName";

    public const string Date = "Date";

    /// <summary>An age of 90 or more, which Safe Harbor removes.</summary>
    public const string Age = "Age";

    /// <summary>An age under 90, which Safe Harbor keeps.</summary>
    public const string AgeUnder90 = "AgeUnder90";

    public const string RecordNumber = "RecordNumber";
    public const string Phone = "Phone";
    public const string Hospital = "Hospital";
    public const string City = "City";
}
