namespace DeidSharp.Evaluation.Cli.Corpora.Surrogates;

// Design pattern: Table-driven method.
// The pattern: behavior that varies case by case is written as rows of data that one
// piece of code reads, instead of as one branch per case.
// Here: each way of telling what a placeholder stood for is one PlaceholderRule row,
// tried in order; the first that matches decides. A new context is a new row, never a
// branch in the filler.

/// <summary>
/// The rules that guess what each <c>___</c> in a MIMIC-IV note stood for, from the text
/// around it. They were written from the notes' published structure (the header fields,
/// the follow-up section, the lab lines) and from how clinical prose names people, places
/// and dates, then checked with counts of how often each rule fires and with the maintainer's
/// audit of a sample from the tuning half. Specific rules come first; the last matches anything.
/// </summary>
internal static class PlaceholderRules
{
    // Rules ignore case, except where a short token in capitals is also a common word in
    // lowercase: "DO" and "do", "US" and "us", "ER" and "er", "M" and "m". Titles take their
    // usual case or capitals, except "MS", which is multiple sclerosis, and "miss", a verb.

    /// <summary>How many characters before and after a placeholder the rules see.</summary>
    public const int ContextLength = 80;

    private const string Relatives =
        "wife|husband|daughter|son|mother|father|mom|dad|sister|brother|niece|nephew|grandson|granddaughter|grandmother|grandfather|" +
        "aunt|uncle|cousin|partner|boyfriend|girlfriend|friend|fiancee?|spouse|HCP|proxy|guardian|neighbor|caregiver|daughter-in-law|son-in-law";

    public static IReadOnlyList<PlaceholderRule> All { get; } =
    [
        // The section is removed whole, so one placeholder stands for every appointment.
        new("follow-up section", @"Follow-?\s*up\s+Instructions:\s*", null, SurrogateKind.FollowUp),
        new("social history section", @"Social\s+History:\s*", null, SurrogateKind.SocialHistory),
        new("name field", @"\bName:\s*", null, SurrogateKind.PatientName),
        new("record number field", @"\b(?:Unit\s*No|MRN|Medical\s+Record\s*(?:No|Number|#)?)\.?\s*[:#]?\s*", null, SurrogateKind.RecordNumber),
        new("birth date field", @"\b(?:Date\s+of\s+Birth|DOB)\s*:?\s*", null, SurrogateKind.BirthDate),
        new("date field", @"\bDate\s*:\s*", null, SurrogateKind.FullDate),
        new("staff field", @"\b(?:Attending|Surgeon|PCP|Primary\s+Care(?:\s+Physician|\s+Provider)?|Provider|Physician)\s*:?[ \t]*(?:is[ \t]+)?", null, SurrogateKind.StaffName),
        new("doctor title", @"\b(?:Dr|Drs|Doctor)\.?[ \t]*", null, SurrogateKind.StaffSurname),
        // "M\.[ ]?D" and not "M\.D\.": the closing \b needs a letter after it, which "M.D. " lacks.
        new("credential after", null, @",?[ \t]*(?-i:MD|M\.[ ]?D|DO|NP|PA|RN|PhD)\b", SurrogateKind.StaffName),
        new("patient title", @"\b(?-i:Mr|Mrs|Ms|Miss|Mister|MR|MRS|MISS|MISTER)\.?[ \t]*", null, SurrogateKind.PatientSurname),
        new("relative", @"\b(?:" + Relatives + @")[ \t]*(?:\(|,|named|is)?[ \t]*", null, SurrogateKind.RelativeName),
        new("age", null, @"[ \t]*-?[ \t]*(?:years?\b|yrs?\b|y/o|y\.o\.?|yo\b|y[ \t]o\b)", SurrogateKind.Age),
        new("age before sex", null, @"[ \t]*-?[ \t]*(?:woman|man|male|female|gentleman|lady|(?-i:F|M))\b", SurrogateKind.Age),
        new("history opening", @"History\s+of\s+Present\s+Illness:\s*", null, SurrogateKind.AgeAndSex),
        new("time after", null, @"[ \t]*@?[ \t]*\d{1,2}:\d{2}", SurrogateKind.FullDate),
        new("imaging after", null, @"[ \t]+(?:CT|CTA|CXR|MRI|MRA|MRCP|Echo|TTE|TEE|EKG|ECG|EGD|ERCP|KUB|Duplex|LENIs|XR|Cath|EEG|Colonoscopy|Bronchoscopy|(?-i:US|PET))\b", SurrogateKind.FullDate),
        new("phone field", @"\b(?:phone|tel|telephone|pager|fax|cell|number)[ \t]*(?:#|no\.?)?[ \t]*[:#]?[ \t]*", null, SurrogateKind.Phone),
        new("call at", @"\bcall\b[^\n]{0,60}\bat[ \t]*", null, SurrogateKind.Phone),
        new("facility word after", null,
            @"[ \t]+(?:Hospital|Medical|Clinic|Center|Centre|Rehab|Rehabilitation|Health|Healthcare|Nursing|Hospice|Manor|Senior|Care|House|Home|(?-i:ED|ER)|Emergency|ICU|Campus|Infirmary|Memorial|General|Regional|Community|Urgent)\b",
            SurrogateKind.City),
        new("eponym after", null, @"[ \t]+(?:disease|syndrome|sign|score|criteria|procedure|catheter|maneuver|test|classification|scale|repair|fracture)\b", SurrogateKind.Untyped),
        new("facility field", @"\b(?:Facility|Hospital|Rehab|Location|Nursing\s+Home|Institution)\s*:\s*", null, SurrogateKind.Hospital),
        new("transfer", @"\b(?:transferred|transfer|admitted|presented|presents|presenting|sent|brought|seen|evaluated|discharged|went|returned|taken|treated|hospitalized|hospitalization|admission|stay|stayed|resides|residing)\b[^\n.]{0,30}\b(?:to|from|at|in)[ \t]+(?:the[ \t]+)?", null, SurrogateKind.Hospital),
        new("home town", @"\b(?:lives|living|lived|moved|born|grew\s+up|native|resident|traveled|travelled|travel|trip|vacation|visiting)\b(?:[^\n.]{0,20}\b(?:in|to|from|of))?[ \t]+", null, SurrogateKind.City),
        new("at", @"(?:\bat|@)[ \t]+(?:the[ \t]+)?", null, SurrogateKind.Hospital),
        // After every rule that reads the word after the placeholder ("by ___ Hospital"), and
        // before the date preposition rule, which would read "seen by ___" as a date. Not
        // "followed by", which as often means "and then" ("followed by ___ days of").
        new("seen by", @"\b(?:seen|evaluated|examined|managed|consulted|signed|reviewed|dictated|performed|discussed|referred)[ \t]+by[ \t]+", null, SurrogateKind.StaffName),
        new("date range start", @"\bfrom[ \t]+", @"[ \t]*(?:to|-|–|until|through)[ \t]*___", SurrogateKind.Date),
        new("date range end", @"___[ \t]*(?:to|-|–|until|through)[ \t]*", null, SurrogateKind.Date),
        new("date preposition", @"\b(?:on|since|until|till|through|thru|dated?|by|starting|started|DOS|as\s+of|prior\s+to)[ \t]+", null, SurrogateKind.Date),
        new("anything else", null, null, SurrogateKind.Untyped),
    ];

    /// <summary>What the first matching rule says the placeholder stood for.</summary>
    public static PlaceholderRule Match(string textBefore, string textAfter) => All.First(r => r.Matches(textBefore, textAfter));
}
