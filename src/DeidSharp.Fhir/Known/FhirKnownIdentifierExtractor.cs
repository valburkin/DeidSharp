using Hl7.Fhir.Model;
using DeidSharp.Dates;
using DeidSharp.Detection;

namespace DeidSharp.Fhir.Known;

/// <summary>
/// Collects the identifiers written in a resource's structured fields (every name,
/// phone, email, address, identifier and birth or death date, plus organization and
/// location names) so free-text de-identification can find them in notes, and the
/// output check can prove they are gone.
/// </summary>
public static class FhirKnownIdentifierExtractor
{
    private const string MothersMaidenName = "http://hl7.org/fhir/StructureDefinition/patient-mothersMaidenName";

    public static KnownIdentifiers Extract(Base root)
    {
        ArgumentNullException.ThrowIfNull(root);
        var known = KnownIdentifiers.Empty;
        var stack = new Stack<Base>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            known = Collect(node, known);
            foreach (var child in FhirChildren.Of(node))
            {
                stack.Push(child.Value);
            }
        }

        return known;
    }

    private static KnownIdentifiers Collect(Base node, KnownIdentifiers known) => node switch
    {
        HumanName name => Name(name, known),
        ContactPoint contact => Contact(contact, known),
        Address address => Address(address, known),
        Identifier identifier => known.AddIdentifier(identifier.Value, IdentifierCategory(identifier)),
        Patient patient => LifeDates(known, patient.BirthDate, (patient.Deceased as FhirDateTime)?.Value),
        RelatedPerson related => LifeDates(known, related.BirthDate, null),
        Person person => LifeDates(known, person.BirthDate, null),
        Practitioner practitioner => LifeDates(known, practitioner.BirthDate, null),
        Organization organization => organization.Alias.Aggregate(known.AddOrganization(organization.Name), (k, a) => k.AddOrganization(a)),
        Location location => location.Alias.Aggregate(known.AddLocation(location.Name), (k, a) => k.AddLocation(a)),
        Extension { Url: MothersMaidenName, Value: FhirString maiden } => known.AddName(maiden.Value),
        _ => known,
    };

    private static KnownIdentifiers Name(HumanName name, KnownIdentifiers known)
    {
        var full = string.Join(' ', name.Given.Append(name.Family).Where(p => !string.IsNullOrWhiteSpace(p)));
        return known.AddName(name.Text).AddName(full);
    }

    private static KnownIdentifiers Contact(ContactPoint contact, KnownIdentifiers known) => contact.System switch
    {
        ContactPoint.ContactPointSystem.Email => known.AddEmail(contact.Value),
        ContactPoint.ContactPointSystem.Url => known.Add(PhiCategory.Url, contact.Value),
        ContactPoint.ContactPointSystem.Fax => known.Add(PhiCategory.Fax, contact.Value),
        ContactPoint.ContactPointSystem.Other => known.AddIdentifier(contact.Value),
        _ => known.AddPhone(contact.Value),
    };

    private static KnownIdentifiers Address(Address address, KnownIdentifiers known)
    {
        var result = address.Line.Aggregate(known, (k, line) => k.AddStreetAddress(line));
        return result
            .AddStreetAddress(address.Text)
            .AddLocation(address.City)
            .AddLocation(address.District)
            .Add(PhiCategory.ZipCode, address.PostalCode);
    }

    private static KnownIdentifiers LifeDates(KnownIdentifiers known, string? birthDate, string? deathDate)
    {
        foreach (var value in new[] { birthDate, deathDate })
        {
            if (FhirDates.TryParse(value, out var date, out _) && date.Precision == DatePrecision.Day)
            {
                known = known.AddDate(date.Midpoint());
            }
        }

        return known;
    }

    private static PhiCategory IdentifierCategory(Identifier identifier)
    {
        var system = identifier.System ?? string.Empty;
        if (system.Contains("us-ssn", StringComparison.OrdinalIgnoreCase))
        {
            return PhiCategory.Ssn;
        }

        if (system.Contains("us-npi", StringComparison.OrdinalIgnoreCase))
        {
            return PhiCategory.LicenseNumber;
        }

        var code = identifier.Type?.Coding.FirstOrDefault()?.Code;
        return code switch
        {
            "MR" or "MRT" => PhiCategory.MedicalRecordNumber,
            "SS" or "SB" => PhiCategory.Ssn,
            "DL" or "NPI" or "MD" or "PRN" or "LN" => PhiCategory.LicenseNumber,
            "MB" or "SN" or "MA" or "MC" => PhiCategory.HealthPlanNumber,
            "AN" or "ACSN" or "BA" => PhiCategory.AccountNumber,
            _ => PhiCategory.OtherIdentifier,
        };
    }
}
