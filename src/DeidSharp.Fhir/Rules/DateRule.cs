using System.Globalization;
using Hl7.Fhir.Model;
using DeidSharp.Dates;

namespace DeidSharp.Fhir.Rules;

// Design pattern: Chain of Responsibility (one handler).
// The pattern: a request passes along a chain of handlers until one of them handles
// it; each handler either acts or hands it to the next.
// Here: the handler for dates, dateTimes and instants. It passes the rest on.

/// <summary>
/// Every date, dateTime and instant. Safe Harbor keeps the year only ("2024-03-05T10:00Z"
/// → "2024"); an instant must carry a full timestamp, so it is removed. Pseudonymization
/// shifts each by the patient's offset, keeping its precision and time of day. A value
/// that does not parse as a date is removed: it cannot be transformed safely.
/// </summary>
public sealed class DateRule : IFhirElementRule
{
    private const string DateTimeFormat = "yyyy-MM-ddTHH:mm:ss.FFFFFFFK";

    public ValueTask<ElementAction> ApplyAsync(FhirElementContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return ValueTask.FromResult(context.Value switch
        {
            Date date => Transform(context, date.Value, v => new Date(v)),
            FhirDateTime dateTime => Transform(context, dateTime.Value, v => new FhirDateTime(v)),
            Instant instant => TransformInstant(context, instant),
            _ => ElementAction.Pass,
        });
    }

    private static ElementAction Transform(FhirElementContext context, string? value, Func<string, Base> create)
    {
        if (!FhirDates.TryParse(value, out var date, out var timestamp))
        {
            return ElementAction.Remove;
        }

        var session = context.Session;
        var transformed = session.Mode == FhirDeidentificationMode.Pseudonymized && timestamp is { } exact
            ? Shift(context, exact).ToString(DateTimeFormat, CultureInfo.InvariantCulture)
            : session.Dates.Transform(date, session.SubjectOf(context.Resource)).ToString();
        return ElementAction.ReplaceWith(create(transformed));
    }

    private static ElementAction TransformInstant(FhirElementContext context, Instant instant) =>
        context.Session.Mode == FhirDeidentificationMode.Pseudonymized && instant.Value is { } value
            ? ElementAction.ReplaceWith(new Instant(Shift(context, value)))
            : ElementAction.Remove;

    // Moves the day by the patient's offset; the time of day and the UTC offset stay.
    private static DateTimeOffset Shift(FhirElementContext context, DateTimeOffset timestamp)
    {
        var session = context.Session;
        var day = PartialDate.FromDate(DateOnly.FromDateTime(timestamp.DateTime));
        var shifted = session.Dates.Transform(day, session.SubjectOf(context.Resource)).Midpoint();
        return new DateTimeOffset(shifted.ToDateTime(TimeOnly.FromDateTime(timestamp.DateTime)), timestamp.Offset);
    }
}
