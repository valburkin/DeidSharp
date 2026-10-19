using System.Text;
using System.Text.RegularExpressions;
using DeidSharp.Detection;
using DeidSharp.Evaluation;

namespace DeidSharp.Tests.Evaluation;

/// <summary>
/// A small hand-annotated corpus of fictional clinical notes. PHI is marked inline as
/// "{{Category|text}}"; <see cref="Parse"/> strips the marks and records gold spans.
/// Each note also lists what the structured record would know about its patient.
/// </summary>
internal static partial class SyntheticCorpus
{
    public static IReadOnlyList<(AnnotatedText Document, KnownIdentifiers Known)> Notes { get; } =
    [
        Note("{{Name|Rosa Alvarez}} (MRN: {{MedicalRecordNumber|4471-882-19}}) was admitted on {{Date|March 2, 2024}} with CKD stage 3.",
            k => k.AddName("Rosa Alvarez").AddIdentifier("4471-882-19", PhiCategory.MedicalRecordNumber)),
        Note("Daughter {{Name|Carmen}} can be reached at {{Phone|(617) 555-0142}} or {{Email|carmen.a@mailbox.example}}.",
            k => k.AddName("Carmen Alvarez")),
        Note("Pt is a {{Age|93}}-year-old woman living alone at {{StreetAddress|48 Willow Creek Road}}, {{Location|Springfield}}, MA {{ZipCode|01103}}.",
            k => k.AddStreetAddress("48 Willow Creek Rd").AddLocation("Springfield")),
        Note("DOB {{Date|03/04/1931}}. SSN {{Ssn|123-45-6789}}. Medicare ID {{HealthPlanNumber|1EG4-TE5-MK73}}.",
            k => k.AddDate(new DateOnly(1931, 3, 4))),
        Note("Seen by Dr. {{Name|Amelia Hart}} at {{Organization|St. Brendan Hospital}}; follow up with Dr {{Name|Hart}} on {{Date|2024-04-02}}.",
            k => k.AddName("Amelia Hart").AddOrganization("St. Brendan Hospital")),
        Note("BP 128/76, HR 88, creatinine 2.1 mg/dL, eGFR 31. Continue lisinopril 10 mg daily; pain 3/10.",
            k => k),
        Note("ICD pacemaker serial number {{DeviceIdentifier|SN-44812}} interrogated; lot # {{DeviceIdentifier|7654321D}}.",
            k => k),
        Note("Portal: {{Url|https://portal.example.org/u/8812}}; last login from {{IpAddress|192.168.10.25}}.",
            k => k),
        Note("Husband {{Name|Robert Lee}} drove her; plate {{VehicleIdentifier|7ABC123}}. Card on file {{AccountNumber|4111 1111 1111 1111}}.",
            k => k.AddName("Robert Lee")),
        Note("Fax records to {{Fax|617-555-0101}}. Account {{AccountNumber|99887766}}. Licence {{LicenseNumber|S99923841}} verified.",
            k => k.AddIdentifier("S99923841", PhiCategory.LicenseNumber)),
        Note("Mrs. {{Name|Álvarez}} reports {{Name|Rosa}}'s fatigue started in {{Date|Aug 2023}}, before her {{Date|5th of September}} visit.",
            k => k.AddName("Rosa María Álvarez")),
        Note("Neighbor {{Name|Lucia Ferreira}} called from {{Phone|+1 413 555 0177}} about the {{Date|12/25/23}} fall.",
            k => k.AddName("Lucia Ferreira")),
    ];

    private static (AnnotatedText, KnownIdentifiers) Note(string marked, Func<KnownIdentifiers, KnownIdentifiers> known) =>
        (Parse(marked), known(KnownIdentifiers.Empty));

    public static AnnotatedText Parse(string marked)
    {
        var text = new StringBuilder();
        var gold = new List<GoldSpan>();
        var position = 0;
        foreach (Match m in Mark().Matches(marked))
        {
            text.Append(marked, position, m.Index - position);
            var start = text.Length;
            text.Append(m.Groups["text"].Value);
            gold.Add(new GoldSpan(start, text.Length, Enum.Parse<PhiCategory>(m.Groups["category"].Value)));
            position = m.Index + m.Length;
        }

        text.Append(marked, position, marked.Length - position);
        return new AnnotatedText(text.ToString(), gold);
    }

    [GeneratedRegex(@"\{\{(?<category>\w+)\|(?<text>.*?)\}\}")]
    private static partial Regex Mark();
}
