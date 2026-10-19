using System.Collections.Immutable;
using System.Globalization;

namespace DeidSharp.Detection;

// Design pattern: Immutable Object.
// The pattern: an object whose state never changes after it is built; "changing" it
// returns a new object and leaves the original as it was.
// Here: each Add returns a new set. One set can then be shared across threads, and the
// known-identifier detector can cache its compiled searches against a set safely. The
// set is held in immutable trees, so building one value by value (every identifier of a
// large bundle) costs O(n log n), not O(n²).

/// <summary>
/// Identifiers already known from the structured part of a record: the patient's name,
/// birth date, MRN, phone, address, relatives' names. Free text written about the
/// patient tends to repeat exactly these, so looking for them directly finds PHI that a
/// generic model can miss (an unusual surname, an MRN without a label).
///
/// Immutable: each <c>Add</c> returns a new set.
/// </summary>
public sealed class KnownIdentifiers
{
    private readonly ImmutableList<KnownIdentifier> _items;
    private readonly ImmutableHashSet<KnownIdentifier> _distinct;

    private KnownIdentifiers(ImmutableList<KnownIdentifier> items, ImmutableHashSet<KnownIdentifier> distinct)
    {
        _items = items;
        _distinct = distinct;
    }

    public static KnownIdentifiers Empty { get; } = new([], []);

    /// <summary>The identifiers in the order they were added.</summary>
    public IReadOnlyList<KnownIdentifier> Items => _items;

    public int Count => _items.Count;

    /// <summary>Adds a value. Blank values are ignored; duplicates are kept once.</summary>
    public KnownIdentifiers Add(PhiCategory category, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return this;
        }

        var item = new KnownIdentifier(category, value.Trim());
        return _distinct.Contains(item) ? this : new KnownIdentifiers(_items.Add(item), _distinct.Add(item));
    }

    public KnownIdentifiers AddName(string? name) => Add(PhiCategory.Name, name);

    /// <summary>Adds a date (birth, death, admission…); it is matched in every common written format.</summary>
    public KnownIdentifiers AddDate(DateOnly date) =>
        Add(PhiCategory.Date, date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

    public KnownIdentifiers AddPhone(string? phone) => Add(PhiCategory.Phone, phone);

    public KnownIdentifiers AddEmail(string? email) => Add(PhiCategory.Email, email);

    /// <summary>Adds one street-address line, e.g. "12 Oak Street Apt 4".</summary>
    public KnownIdentifiers AddStreetAddress(string? line) => Add(PhiCategory.StreetAddress, line);

    /// <summary>Adds a city, county or other place name.</summary>
    public KnownIdentifiers AddLocation(string? place) => Add(PhiCategory.Location, place);

    public KnownIdentifiers AddOrganization(string? organization) => Add(PhiCategory.Organization, organization);

    /// <summary>Adds an identifier such as an MRN, SSN or plan number.</summary>
    public KnownIdentifiers AddIdentifier(string? value, PhiCategory category = PhiCategory.OtherIdentifier) =>
        Add(category, value);

    /// <summary>Returns a set holding the identifiers of both sets.</summary>
    public KnownIdentifiers Merge(KnownIdentifiers other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return other._items.Aggregate(this, (set, item) => set.Add(item.Category, item.Value));
    }
}

/// <summary>One known identifier: its category and its value as it appears in the record.</summary>
public sealed record KnownIdentifier(PhiCategory Category, string Value);
