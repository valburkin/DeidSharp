using System.Collections.Frozen;
using System.Globalization;

namespace DeidSharp.Evaluation.Cli.Corpora.Surrogates;

// Design pattern: Table-driven method.
// The pattern: behavior that varies case by case is written as rows of data that one
// piece of code reads, instead of as one branch per case.
// Here: each kind of placeholder is one row of Renderers, which says what text and labels
// go in its place; a new kind is a new row, never a branch in Render.

/// <summary>
/// The made-up identity behind one note: a patient with a name, age, record number and
/// admission date, and the clinicians, relatives and hospitals the note mentions. Each
/// placeholder is filled from it, so the patient keeps one name and one age throughout, a
/// clinician named twice is the same person, and the dates fall around one admission.
/// </summary>
internal sealed class NoteSurrogates
{
    private static readonly string[] FullDateFormats = ["M/d/yyyy", "MM/dd/yyyy", "yyyy-MM-dd"];
    private static readonly string[] Saints = ["Mary", "Joseph", "Luke", "Francis", "Anne", "Vincent", "Elizabeth", "John", "Michael", "Catherine", "Anthony", "Jude", "Patrick", "Margaret", "Peter", "Paul"];

    private static readonly string[] Relations = ["wife", "husband", "daughter", "son", "sister", "brother", "partner", "niece"];

    private static readonly string[] FacilityNames =
    [
        "Hospital", "Medical Center", "General Hospital", "Memorial Hospital", "Regional Medical Center", "Community Hospital",
        "Rehabilitation Hospital", "Nursing and Rehabilitation Center", "Health Center", "Senior Living", "Hospice",
    ];

    // One row per kind of placeholder: what goes in its place.
    private static readonly FrozenDictionary<SurrogateKind, Func<NoteSurrogates, IReadOnlyList<SurrogatePiece>>> Renderers =
        new Dictionary<SurrogateKind, Func<NoteSurrogates, IReadOnlyList<SurrogatePiece>>>
        {
            [SurrogateKind.PatientName] = s => [Phi(s._lastNameFirst ? $"{s._last}, {s._first}" : $"{s._first} {s._last}", SurrogateLabels.PatientName)],
            [SurrogateKind.PatientSurname] = s => [Phi(s._last, SurrogateLabels.PatientName)],
            [SurrogateKind.StaffName] = s => [Phi(FullName(s.Staff()), SurrogateLabels.StaffName)],
            [SurrogateKind.StaffSurname] = s => [Phi(s.Staff().Last, SurrogateLabels.StaffName)],
            [SurrogateKind.RelativeName] = s => [Phi(s.Relative(), SurrogateLabels.RelativeName)],
            [SurrogateKind.Date] = s => [Phi(s.ProseDate(), SurrogateLabels.Date)],
            [SurrogateKind.FullDate] = s => [Phi(s.Format(s.DateAround()), SurrogateLabels.Date)],
            [SurrogateKind.BirthDate] = s => [Phi(s.Format(s._admitted.AddYears(-s._age).AddDays(-s._random.Next(365))), SurrogateLabels.Date)],
            [SurrogateKind.Age] = s => [Phi(s._age.ToString(CultureInfo.InvariantCulture), s.AgeLabel)],
            [SurrogateKind.RecordNumber] = s => [Phi(s._recordNumber, SurrogateLabels.RecordNumber)],
            [SurrogateKind.Phone] = s => [Phi(s.Phone(), SurrogateLabels.Phone)],
            [SurrogateKind.Hospital] = s => [Phi(s.Hospital(), SurrogateLabels.Hospital)],
            [SurrogateKind.City] = s => [Phi(s._lists.Place(s._random), SurrogateLabels.City)],
            [SurrogateKind.FollowUp] = s => s.FollowUp(),
            [SurrogateKind.SocialHistory] = s => s.SocialHistory(),
            [SurrogateKind.AgeAndSex] = s => [Phi(s._age.ToString(CultureInfo.InvariantCulture), s.AgeLabel), new SurrogatePiece($" y/o {s._sex}", null)],
            [SurrogateKind.Untyped] = s => [Phi(s.UntypedName(), SurrogateLabels.UntypedName)],
        }.ToFrozenDictionary();

    private readonly SurrogateLists _lists;
    private readonly SeededRandom _random;
    private readonly char _sex;
    private readonly string _first;
    private readonly string _last;
    private readonly bool _lastNameFirst;
    private readonly int _age;
    private readonly DateOnly _admitted;
    private readonly string _fullDateFormat;
    private readonly bool _phoneInParentheses;
    private readonly string _recordNumber;
    private readonly List<(string First, string Last)> _staff = [];
    private readonly List<string> _relatives = [];
    private readonly List<string> _hospitals = [];

    /// <param name="sex">The patient's sex as the note gives it (<c>'F'</c> or <c>'M'</c>), or null to draw one.</param>
    public NoteSurrogates(SurrogateLists lists, SeededRandom random, char? sex)
    {
        ArgumentNullException.ThrowIfNull(lists);
        ArgumentNullException.ThrowIfNull(random);
        (_lists, _random) = (lists, random);
        _sex = sex ?? RandomSex();
        _first = lists.FirstName(random, _sex);
        _last = lists.Surname(random);
        _lastNameFirst = random.Next(2) == 0;

        // A few adult inpatients in a hundred are 90 or older; Safe Harbor removes only those ages.
        _age = random.NextDouble() < 0.04 ? random.Next(90, 100) : random.Next(18, 90);

        // MIMIC-IV's admissions span 2008 to 2019.
        _admitted = new DateOnly(2008, 1, 1).AddDays(random.Next(12 * 365));
        _fullDateFormat = random.Pick(FullDateFormats);
        _phoneInParentheses = random.Next(2) == 0;
        _recordNumber = random.Next(10_000_000, 100_000_000).ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>The text that goes where a placeholder of <paramref name="kind"/> was, with the PHI in it labeled.</summary>
    public IReadOnlyList<SurrogatePiece> Render(SurrogateKind kind) =>
        Renderers.TryGetValue(kind, out var render) ? render(this) : throw new ArgumentOutOfRangeException(nameof(kind));

    /// <summary>The kinds of placeholder this class fills.</summary>
    public static IEnumerable<SurrogateKind> Kinds => Renderers.Keys;

    private string AgeLabel => _age >= 90 ? SurrogateLabels.Age : SurrogateLabels.AgeUnder90;

    private static SurrogatePiece Phi(string text, string label) => new(text, label);

    private static string FullName((string First, string Last) person) => $"{person.First} {person.Last}";

    private char RandomSex() => _random.Next(2) == 0 ? 'F' : 'M';

    // The section was removed whole; in MIMIC-III, which kept its structure, each appointment
    // read "Provider: <name>, MD Phone:<number> Date/Time:<date> <time>".
    private List<SurrogatePiece> FollowUp()
    {
        var pieces = new List<SurrogatePiece>();
        var appointments = _random.Next(1, 4);
        for (var i = 0; i < appointments; i++)
        {
            pieces.AddRange(
            [
                new SurrogatePiece(i == 0 ? "Provider: " : "\nProvider: ", null),
                Phi(FullName(Staff()), SurrogateLabels.StaffName),
                new SurrogatePiece(", MD Phone: ", null),
                Phi(Phone(), SurrogateLabels.Phone),
                new SurrogatePiece(" Date/Time: ", null),
                Phi(Format(_admitted.AddDays(_random.Next(7, 60))), SurrogateLabels.Date),
                new SurrogatePiece(string.Create(CultureInfo.InvariantCulture, $" {_random.Next(8, 17)}:{_random.Next(4) * 15:D2}"), null),
            ]);
        }

        return pieces;
    }

    // The section was removed whole; what it says about where and with whom the patient
    // lives is the PHI in it. One of three forms, so the section isn't the same in every note.
    private SurrogatePiece[] SocialHistory()
    {
        var relation = _random.Pick(Relations);
        return _random.Next(3) switch
        {
            0 => [new SurrogatePiece("Lives in ", null), Phi(_lists.Place(_random), SurrogateLabels.City), new SurrogatePiece($" with {relation} ", null), Phi(Relative(), SurrogateLabels.RelativeName), new SurrogatePiece(".", null)],
            1 => [new SurrogatePiece("Lives alone in ", null), Phi(_lists.Place(_random), SurrogateLabels.City), new SurrogatePiece($". Emergency contact: {relation} ", null), Phi(Relative(), SurrogateLabels.RelativeName), new SurrogatePiece(", ", null), Phi(Phone(), SurrogateLabels.Phone), new SurrogatePiece(".", null)],
            _ => [new SurrogatePiece("Resides at ", null), Phi(Hospital(), SurrogateLabels.Hospital), new SurrogatePiece($". {char.ToUpperInvariant(relation[0])}{relation[1..]} ", null), Phi(Relative(), SurrogateLabels.RelativeName), new SurrogatePiece(" visits weekly.", null)],
        };
    }

    // A clinician already named comes back more often than a new one appears.
    private (string First, string Last) Staff() => Reuse(_staff, 8, () => (_lists.FirstName(_random, RandomSex()), _lists.Surname(_random)));

    // What no rule typed was a first name or a surname, now and then a clinician's (the
    // maintainer's reading of a sample of the tuning half); half are drawn from each, from the
    // note's clinicians or from people the note mentions once.
    private string UntypedName()
    {
        var person = _random.Next(2) == 0 ? Staff() : (First: _lists.FirstName(_random, RandomSex()), Last: _lists.Surname(_random));
        return _random.Next(2) == 0 ? person.First : person.Last;
    }

    private string Relative() => Reuse(_relatives, 3, () => _lists.FirstName(_random, RandomSex()));

    private string Hospital() => Reuse(_hospitals, 3, () => _random.NextDouble() < 0.2
        ? $"St. {_random.Pick(Saints)}'s {_random.Pick(FacilityNames)}"
        : $"{_lists.Place(_random)} {_random.Pick(FacilityNames)}");

    private T Reuse<T>(List<T> pool, int limit, Func<T> create)
    {
        if (pool.Count > 0 && (pool.Count >= limit || _random.NextDouble() < 0.6))
        {
            return _random.Pick(pool);
        }

        var made = create();
        pool.Add(made);
        return made;
    }

    private DateOnly DateAround() => _admitted.AddDays(_random.Next(-30, 15));

    // Prose writes dates in full, as month and day, or with the month's name.
    private string ProseDate()
    {
        var date = DateAround();
        var roll = _random.NextDouble();
        return roll < 0.6 ? Format(date) : date.ToString(roll < 0.85 ? "M/d" : "MMMM d", CultureInfo.InvariantCulture);
    }

    private string Format(DateOnly date) => date.ToString(_fullDateFormat, CultureInfo.InvariantCulture);

    private string Phone()
    {
        // North American numbers: neither the area code nor the exchange starts with 0 or 1.
        var area = _random.Next(200, 1000);
        var exchange = _random.Next(200, 1000);
        var line = _random.Next(10_000);
        return _phoneInParentheses
            ? string.Create(CultureInfo.InvariantCulture, $"({area}) {exchange}-{line:D4}")
            : string.Create(CultureInfo.InvariantCulture, $"{area}-{exchange}-{line:D4}");
    }
}

/// <summary>A piece of the text that replaces a placeholder: PHI with its gold label, or plain text (<paramref name="Label"/> null).</summary>
internal readonly record struct SurrogatePiece(string Text, string? Label);
