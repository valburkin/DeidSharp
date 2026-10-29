using DeidSharp.Evaluation.Cli.Corpora.Surrogates;

namespace DeidSharp.Evaluation.Cli.Tests.Corpora.Surrogates;

/// <summary>Which rule types a placeholder, on made-up contexts in the shape of a discharge summary.</summary>
public class PlaceholderRulesTests
{
    [Theory]
    [InlineData("Name:  ", "  Unit No:   ___", "name field", nameof(SurrogateKind.PatientName))]
    [InlineData("Name:  ___   Unit No:   ", "\n", "record number field", nameof(SurrogateKind.RecordNumber))]
    [InlineData("Admission Date:  ", "  Discharge Date:", "date field", nameof(SurrogateKind.FullDate))]
    [InlineData("Date of Birth:  ", "  Sex:   F", "birth date field", nameof(SurrogateKind.BirthDate))]
    [InlineData("Attending: ", ".\n", "staff field", nameof(SurrogateKind.StaffName))]
    [InlineData("seen by Dr. ", " in clinic", "doctor title", nameof(SurrogateKind.StaffSurname))]
    [InlineData("discussed with ", ", NP on the floor", "credential after", nameof(SurrogateKind.StaffName))]
    [InlineData("Ms. ", " was admitted", "patient title", nameof(SurrogateKind.PatientSurname))]
    [InlineData("called her daughter ", " who agreed", "relative", nameof(SurrogateKind.RelativeName))]
    [InlineData("This is a ", " year old woman", "age", nameof(SurrogateKind.Age))]
    [InlineData("Pertinent Results:\n", " 07:15AM BLOOD WBC-7.2", "time after", nameof(SurrogateKind.FullDate))]
    [InlineData("Imaging:\n", " CT head: no bleed", "imaging after", nameof(SurrogateKind.FullDate))]
    [InlineData("Followup Instructions:\n", "\n", "follow-up section", nameof(SurrogateKind.FollowUp))]
    [InlineData("Social History:\n", "\nFamily History:", "social history section", nameof(SurrogateKind.SocialHistory))]
    [InlineData("History of Present Illness:\n", " with a history of CHF", "history opening", nameof(SurrogateKind.AgeAndSex))]
    [InlineData("pager ", "\n", "phone field", nameof(SurrogateKind.Phone))]
    [InlineData("please call the clinic at ", ".", "call at", nameof(SurrogateKind.Phone))]
    [InlineData("transferred from ", " for further care", "transfer", nameof(SurrogateKind.Hospital))]
    [InlineData("followed at ", " Hospital", "facility word after", nameof(SurrogateKind.City))]
    [InlineData("She lives in ", " with her son", "home town", nameof(SurrogateKind.City))]
    [InlineData("started on ", " for the infection", "date preposition", nameof(SurrogateKind.Date))]
    [InlineData("on aspirin from ", " to ___", "date range start", nameof(SurrogateKind.Date))]
    [InlineData("seen by the team and ", " agreed", "anything else", nameof(SurrogateKind.Untyped))]
    [InlineData("her husband ", " do not resuscitate", "relative", nameof(SurrogateKind.RelativeName))]
    [InlineData("This is a ", " M with", "age before sex", nameof(SurrogateKind.Age))]
    [InlineData("seen with ", " m and", "anything else", nameof(SurrogateKind.Untyped))]
    [InlineData("results discussed with ", " us at the bedside", "anything else", nameof(SurrogateKind.Untyped))]
    [InlineData("seen with ", " er and", "anything else", nameof(SurrogateKind.Untyped))]
    [InlineData("taken to ", " ED for", "facility word after", nameof(SurrogateKind.City))]
    [InlineData("discussed with ", ", M.D. on call", "credential after", nameof(SurrogateKind.StaffName))]
    [InlineData("discussed with ", " M.D.", "credential after", nameof(SurrogateKind.StaffName))]
    [InlineData("discussed with ", ", MD,", "credential after", nameof(SurrogateKind.StaffName))]
    [InlineData("She was seen by ", " in clinic", "seen by", nameof(SurrogateKind.StaffName))]
    [InlineData("imaging reviewed by ", ".", "seen by", nameof(SurrogateKind.StaffName))]
    [InlineData("to be completed by ", ".", "date preposition", nameof(SurrogateKind.Date))]
    [InlineData("history of MS ", " and", "anything else", nameof(SurrogateKind.Untyped))]
    [InlineData("did not miss ", " and", "anything else", nameof(SurrogateKind.Untyped))]
    [InlineData("Miss ", " was", "patient title", nameof(SurrogateKind.PatientSurname))]
    [InlineData("INSTRUCTIONS FOR MR. ", ":", "patient title", nameof(SurrogateKind.PatientSurname))]
    [InlineData("discussed with ", ", M. D.", "credential after", nameof(SurrogateKind.StaffName))]
    [InlineData("seen by ", " ED physicians", "facility word after", nameof(SurrogateKind.City))]
    [InlineData("evaluated by ", " procedure", "eponym after", nameof(SurrogateKind.Untyped))]
    [InlineData("lasix on ___ followed by ", ".", "date preposition", nameof(SurrogateKind.Date))]
    [InlineData("Discharge Disposition:\nExtended Care\nFacility:\n", "\n", "facility field", nameof(SurrogateKind.Hospital))]
    [InlineData("on aspirin from ___ to ", " for pain", "date range end", nameof(SurrogateKind.Date))]
    [InlineData("held ___ - ", ".", "date range end", nameof(SurrogateKind.Date))]
    public void Types_a_placeholder_by_the_first_rule_its_context_matches(string before, string after, string rule, string kind)
    {
        var match = PlaceholderRules.Match(before, after);

        Assert.Equal(rule, match.Name);
        Assert.Equal(kind, match.Kind.ToString());
    }

    [Fact]
    public void Ends_with_a_rule_that_matches_anything_and_names_each_rule_once()
    {
        Assert.True(PlaceholderRules.All[^1].Matches("", ""));
        Assert.Equal(PlaceholderRules.All.Count, PlaceholderRules.All.Select(r => r.Name).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Names_only_kinds_that_can_be_filled()
    {
        Assert.Equal(Enum.GetValues<SurrogateKind>().Order(), NoteSurrogates.Kinds.Order());
        Assert.All(PlaceholderRules.All, r => Assert.Contains(r.Kind, NoteSurrogates.Kinds));
    }
}
