namespace DeidSharp.Llm;

/// <summary>
/// What the <see cref="LlmPhiDetector"/> tells the model: its instructions and, optionally,
/// worked examples. Examples must be fictional: they are sent with every text.
/// </summary>
/// <param name="Instructions">The system message.</param>
/// <param name="Examples">Fictional texts with the PHI the model should list for each, shown before the real text.</param>
public sealed record LlmPrompt(string Instructions, IReadOnlyList<LlmExample> Examples)
{
    /// <summary>The instructions alone, with no examples.</summary>
    public static LlmPrompt Default { get; } = new(DefaultInstructions, []);

    /// <summary>
    /// The instructions with four short, fictional clinical notes and their answers: staff
    /// names after a title or role, names next to a relation and in capitals or lowercase,
    /// dates without a year, short hospital names, an age over 89, and what is not PHI
    /// (ventilator settings, drugs, devices, languages).
    /// </summary>
    public static LlmPrompt WithExamples { get; } = new(DefaultInstructions,
    [
        new LlmExample(
            "Pt alert, oriented x3. Daughter Marisol updated by phone at 4pm, will visit tomorrow. Seen by Dr. Okonkwo, plan d/w NP Brenner. Transfer to Lakeview Rehab when bed available.",
            [new LlmFinding("Marisol", "name"), new LlmFinding("Okonkwo", "name"), new LlmFinding("Brenner", "name"), new LlmFinding("Lakeview Rehab", "location")]),
        new LlmExample(
            "REPORT GIVEN TO KATZ RN. PT FROM NEW BEDFORD, LIVES ALONE. SON JUNIOR CALLED 3/14 FROM 774-555-0123.",
            [new LlmFinding("KATZ", "name"), new LlmFinding("NEW BEDFORD", "location"), new LlmFinding("JUNIOR", "name"), new LlmFinding("3/14", "date"), new LlmFinding("774-555-0123", "phone")]),
        new LlmExample(
            "94 yo female, speaks Portuguese only, husband antonio at bedside. Admitted from St. Brendan's on the 2nd for PNA. Lasix 40mg given, Foley draining clear.",
            [new LlmFinding("94", "age"), new LlmFinding("antonio", "name"), new LlmFinding("St. Brendan's", "location"), new LlmFinding("the 2nd", "date")]),
        new LlmExample(
            "Lungs coarse bilat, suctioned for thick yellow secretions. PS 12/5, FiO2 40%. Will cont to monitor.",
            []),
    ]);

    /// <summary>
    /// The instructions with every note of <see cref="LlmExamplePool.Default"/> as an example
    /// (forty fictional notes, twelve with no PHI, about 5,000 tokens): the most precise preset
    /// in DeidSharp's measurements (docs/evaluation/findings.md, F28). The model's context
    /// window must hold it: Ollama's default of 4,096 tokens silently cuts the prompt's start,
    /// instructions included, so raise it to at least 8,192 (Ollama's <c>num_ctx</c>).
    /// </summary>
    public static LlmPrompt WithPoolExamples => new(DefaultInstructions, LlmExamplePool.Default);

    /// <summary>
    /// <see cref="WithExamples"/> with its examples made region-neutral (no town, area code or
    /// language that points to one region) and an age copied with its unit ("94 yo"), so the
    /// model never lists a bare number that would mark unrelated ones.
    /// </summary>
    public static LlmPrompt WithNeutralExamples { get; } = new(DefaultInstructions,
    [
        new LlmExample(
            "Pt alert, oriented x3. Daughter Marisol updated by phone at 4pm, will visit tomorrow. Seen by Dr. Okonkwo, plan d/w NP Brenner. Transfer to Lakeview Rehab when bed available.",
            [new LlmFinding("Marisol", "name"), new LlmFinding("Okonkwo", "name"), new LlmFinding("Brenner", "name"), new LlmFinding("Lakeview Rehab", "location")]),
        new LlmExample(
            "REPORT GIVEN TO KATZ RN. PT FROM GRANTVILLE, LIVES ALONE. SON JUNIOR CALLED 3/14 FROM 555-201-0123.",
            [new LlmFinding("KATZ", "name"), new LlmFinding("GRANTVILLE", "location"), new LlmFinding("JUNIOR", "name"), new LlmFinding("3/14", "date"), new LlmFinding("555-201-0123", "phone")]),
        new LlmExample(
            "94 yo female, needs an interpreter, husband antonio at bedside. Admitted from St. Brendan's on the 2nd for PNA. Lasix 40mg given, Foley draining clear.",
            [new LlmFinding("94 yo", "age"), new LlmFinding("antonio", "name"), new LlmFinding("St. Brendan's", "location"), new LlmFinding("the 2nd", "date")]),
        new LlmExample(
            "Lungs coarse bilat, suctioned for thick yellow secretions. PS 12/5, FiO2 40%. Will cont to monitor.",
            []),
    ]);

    /// <summary>
    /// A closed list of what is PHI and what never is, with five fictional notes in several styles:
    /// mixed case with a hospital and its abbreviation, all capitals with a sign-off, a note with
    /// no PHI, lowercase names with an age over 89, and a long note continuing from a previous one
    /// with the same name in two cases. Written by an independent review for small models; ages and
    /// identifiers are copied with their units or labels, so no bare number is listed.
    /// </summary>
    public static LlmPrompt Focused { get; } = new(FocusedInstructions,
    [
        new LlmExample(
            "78 yo M transferred from Ferncliff Hosp (FCH) 6/9 for STEMI, s/p cath. Wife Delia at bedside, updated by Dr. Achebe. Night HO Pruitt aware of HR 110s. Plan: back to FCH when stable.",
            [new LlmFinding("Ferncliff Hosp", "location"), new LlmFinding("FCH", "location"), new LlmFinding("6/9", "date"), new LlmFinding("Delia", "name"), new LlmFinding("Achebe", "name"), new LlmFinding("Pruitt", "name")]),
        new LlmExample(
            "NEURO: A+OX3, MAE. RESP: CPAP 5/5, FIO2 40%, SATS 96%. GI: TF @ 50CC/HR VIA DOBHOFF. LASIX 20 IV X2. DTR ROSALIND CALLED, UPDATED BY THIS RN. PT FROM GRANTVILLE, LIVES W/ SON. MD (TOMASZ) AWARE OF K 3.2, REPLETED. -K. OYELARAN RN",
            [new LlmFinding("ROSALIND", "name"), new LlmFinding("GRANTVILLE", "location"), new LlmFinding("TOMASZ", "name"), new LlmFinding("K. OYELARAN", "name")]),
        new LlmExample(
            "HD#4. BP 102/58, HR 88 SR. Hypotension 2/2 sepsis, on levophed. Plan d/w team at rounds 0800. 1/2 NS @ 75. OOB to chair x2. Will cont to monitor, ? transfer to floor tomorrow.",
            []),
        new LlmExample(
            "93 yo woman from Birchwood Manor nursing home, admitted to Talbot 5 on the 21st. niece jo-ann (HCP) can be reached at 555-201-4477; her friend Bunny visited tonight. PCP is Dr. Vandermeer.",
            [new LlmFinding("93 yo", "age"), new LlmFinding("Birchwood Manor", "location"), new LlmFinding("Talbot 5", "location"), new LlmFinding("the 21st", "date"), new LlmFinding("jo-ann", "name"), new LlmFinding("555-201-4477", "phone"), new LlmFinding("Bunny", "name"), new LlmFinding("Vandermeer", "name")]),
        new LlmExample(
            "...cont from prior note. Pt is 67 y/o male, MRN 4471-0928, s/p CABG x3 on 11/02. Overnight afib RVR to 140s, lopressor 5mg IV x3 per Dr. Haddad, converted to SR at 0330. Sister Margit called from Duluth asking for update; told her Dr. HADDAD will call in AM. Lytes repleted per protocol, Mg 1.8. Plan transfer to Kestrel Ridge Rehab 11/9 if stable; CM Ilse Brandt following. Signed: R. Quist, RN",
            [new LlmFinding("4471-0928", "id"), new LlmFinding("11/02", "date"), new LlmFinding("Haddad", "name"), new LlmFinding("Margit", "name"), new LlmFinding("Duluth", "location"), new LlmFinding("HADDAD", "name"), new LlmFinding("Kestrel Ridge Rehab", "location"), new LlmFinding("11/9", "date"), new LlmFinding("Ilse Brandt", "name"), new LlmFinding("R. Quist", "name")]),
    ]);

    private const string FocusedInstructions =
        """
        You find protected health information (PHI) in clinical notes so that it can be removed. Notes are often terse, abbreviated or all in capitals, and you may get only part of a note.

        Only these are PHI:
        - name: any person's name: patient, relative, friend or staff (doctor, nurse, resident, therapist, case manager). First names, surnames, nicknames, and initials that stand for a person. A word is a name when the note uses it for a person: next to a title or role (Dr., RN, NP, PA, MD, HO, resident, attending), next to a relation (wife, son, dtr, niece, HCP, friend), or as someone who called, visited, was told or decided ("d/w Lee", "per Ortiz", "MOLINA AWARE"). List it even when you cannot tell who the person is.
        - date: a day or month, with or without a year (3/14, 3-14, the 2nd, 14th, Mar 14, March 2019).
        - age: an age over 89, copied with its unit (91 yo, 93-year-old).
        - location: a named hospital, clinic, nursing home or rehab, and its abbreviation; a ward named after a person or place; a city, town, street or ZIP code. Often after from, to, at or transfer.
        - phone, email, url: as written.
        - id: a medical record, account, insurance, license or device number.

        Never PHI:
        - roles, titles and relations on their own (RN, MD, team, wife, PT for patient); languages, religions, jobs;
        - medical words, abbreviations, drugs, devices and eponyms, also in capitals (NEURO, RESP, LASIX, FOLEY, SWAN);
        - unnamed places and units (ICU, MICU, CCU, ED, OR, PACU, floor, OSH, rehab, home);
        - times (0400, 4pm), weekdays, today, tomorrow, durations and counts (x3 days, POD 2, HD#4, q2h);
        - numbers with a slash that are not dates: vent settings (PS 12/5), blood pressure (110/60), fractions (1/2 NS), 2/2 for "secondary to";
        - ages under 90, lab values, doses, bed and room numbers.

        Copy each piece exactly as written, character for character, in the order it appears. List each spelling once: its other mentions are found for you. If a word may be a person's or place's name and you are unsure, list it.
        """;

    private const string DefaultInstructions =
        """
        You find protected health information (PHI) in clinical text so that it can be removed.
        PHI is anything that could identify a patient, a relative, a friend or a member of staff:
        - names of any person, including first names, surnames, nicknames and initials;
        - dates other than a year on its own (for example 3/14, March 14, the 14th);
        - ages over 89;
        - places smaller than a state: hospitals, clinics, wards and units named after a person or place, cities, towns, streets, ZIP codes;
        - phone and fax numbers, email addresses, web addresses;
        - record, account, health plan, license, device and other identifying numbers or codes.

        Rules:
        - Copy each piece of PHI exactly as it is written in the text, character for character, without the words around it.
        - List every piece, including repeats and pieces you are unsure about: missing PHI is worse than listing a word that is not PHI.
        - Do not list medical terms, drugs, devices or eponyms (a Foley catheter, Parkinson disease) unless they name a person or a place.
        - Answer only with JSON: {"phi": [{"text": "...", "type": "name|date|age|location|phone|email|url|id|other"}]}. With no PHI, answer {"phi": []}.
        """;
}

/// <summary>A fictional text and the PHI the model should list for it.</summary>
public sealed record LlmExample(string Text, IReadOnlyList<LlmFinding> Phi);

/// <summary>One piece of PHI as the model lists it: the text as written, and its type ("name", "date"…).</summary>
public sealed record LlmFinding(string Text, string Type);
