using Hl7.Fhir.Model;

namespace DeidSharp.Fhir.Rules;

/// <summary>What a rule decided about one element.</summary>
public enum ElementActionKind
{
    /// <summary>This rule does not handle the element; ask the next rule.</summary>
    Pass,

    /// <summary>Keep the element as it is and do not look inside it.</summary>
    Keep,

    /// <summary>Keep the element and apply the rules to its children.</summary>
    Descend,

    /// <summary>Remove the element.</summary>
    Remove,

    /// <summary>Put <see cref="ElementAction.Replacement"/> in its place and do not look inside it.</summary>
    Replace,
}

// Design pattern: Static Factory Method.
// The pattern: static members that build an object, named for what it means, in place
// of a constructor call.
// Here: a rule returns ElementAction.Remove or ElementAction.ReplaceWith(x), which reads
// as the decision. ReplaceWith also ensures a replacement is never null.

/// <summary>A rule's decision about one element, with the replacement when there is one.</summary>
public readonly record struct ElementAction(ElementActionKind Kind, Base? Replacement = null)
{
    public static ElementAction Pass => new(ElementActionKind.Pass);

    public static ElementAction Keep => new(ElementActionKind.Keep);

    public static ElementAction Descend => new(ElementActionKind.Descend);

    public static ElementAction Remove => new(ElementActionKind.Remove);

    public static ElementAction ReplaceWith(Base replacement) =>
        new(ElementActionKind.Replace, replacement ?? throw new ArgumentNullException(nameof(replacement)));
}
