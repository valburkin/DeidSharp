namespace DeidSharp.Evaluation.Cli.Corpora.Surrogates;

/// <summary>What a placeholder stood for, as its context suggests, and so what goes in its place.</summary>
internal enum SurrogateKind
{
    /// <summary>The patient's full name ("Name: ___").</summary>
    PatientName,

    /// <summary>The patient's surname after a title ("Ms. ___").</summary>
    PatientSurname,

    /// <summary>A clinician's full name ("Attending: ___").</summary>
    StaffName,

    /// <summary>A clinician's surname ("Dr. ___").</summary>
    StaffSurname,

    /// <summary>A relative's or friend's first name ("his wife ___").</summary>
    RelativeName,

    /// <summary>A date in prose ("on ___"), written in full or as month and day.</summary>
    Date,

    /// <summary>A date in a field or a timestamp ("Admission Date: ___", "___ 07:15AM"), always written in full.</summary>
    FullDate,

    /// <summary>The patient's date of birth.</summary>
    BirthDate,

    /// <summary>The patient's age ("___ year old").</summary>
    Age,

    /// <summary>The patient's record number ("Unit No: ___").</summary>
    RecordNumber,

    /// <summary>A phone number ("call ... at ___").</summary>
    Phone,

    /// <summary>A hospital or care facility ("transferred to ___").</summary>
    Hospital,

    /// <summary>A town, or a place word in a facility's name ("___ Hospital").</summary>
    City,

    /// <summary>The follow-up section, removed whole: appointments with a clinician, a phone number and a date each.</summary>
    FollowUp,

    /// <summary>The social history section, removed whole: where and with whom the patient lives.</summary>
    SocialHistory,

    /// <summary>The age and sex that open the history of present illness ("___ with a history of"): "72 y/o F".</summary>
    AgeAndSex,

    /// <summary>No rule recognized the context; a first name or a surname goes in, which is what these placeholders stood for.</summary>
    Untyped,
}
