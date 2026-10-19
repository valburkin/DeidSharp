namespace DeidSharp.Pseudonymization;

// Design pattern: Strategy.
// The pattern: a family of interchangeable algorithms behind one interface; the
// caller runs whichever it is given without knowing which one it got.
// Here: how a value becomes a pseudonym. Every consumer (ids, identifiers, date
// shifts, surrogates) asks this interface, so the keyed hash can be swapped (a key held
// in an HSM, another algorithm) without touching them.

/// <summary>
/// Turns a value into a stable, irreversible digest. The same domain and value always
/// give the same digest (so a patient keeps one pseudonym everywhere); different
/// domains give unrelated digests (so an MRN's pseudonym says nothing about the same
/// digits used as a phone number). <see cref="PseudonymFormats"/> shapes digests into ids.
/// </summary>
public interface IPseudonymizer
{
    /// <param name="domain">What kind of value this is, e.g. "Patient.id" or "Name".</param>
    /// <returns>At least 32 bytes.</returns>
    byte[] Digest(string domain, string value);
}
