namespace DeidSharp.Text;

/// <summary>
/// The invented values surrogates are drawn from. Common, unremarkable names and
/// places, so a surrogate reads like real text; no list ties back to the source data.
/// </summary>
internal static class SurrogateData
{
    public static readonly string[] FirstNames =
    [
        "James", "Mary", "Robert", "Patricia", "John", "Jennifer", "Michael", "Linda", "David", "Elizabeth",
        "William", "Barbara", "Richard", "Susan", "Joseph", "Jessica", "Thomas", "Sarah", "Charles", "Karen",
        "Daniel", "Lisa", "Matthew", "Nancy", "Anthony", "Sandra", "Mark", "Ashley", "Steven", "Emily",
        "Paul", "Donna", "Andrew", "Michelle", "Joshua", "Carol", "Kevin", "Amanda", "Brian", "Melissa",
    ];

    public static readonly string[] LastNames =
    [
        "Bennett", "Carter", "Dawson", "Ellis", "Fletcher", "Garrison", "Hale", "Ingram", "Jennings", "Keller",
        "Lawson", "Mercer", "Norris", "Osborne", "Prescott", "Quinlan", "Ramsey", "Sutton", "Thornton", "Underwood",
        "Vaughn", "Whitaker", "Yates", "Abbott", "Barlow", "Conley", "Dorsey", "Emerson", "Finch", "Gallagher",
        "Holloway", "Irwin", "Jarvis", "Kendall", "Lowell", "Maddox", "Nolan", "Pierce", "Rowland", "Shepard",
    ];

    public static readonly string[] Cities =
    [
        "Riverton", "Lakeview", "Fairhaven", "Brookfield", "Cedar Falls", "Maple Grove", "Oakridge", "Pine Bluff",
        "Westfield", "Easton", "Northbrook", "Silver Lake", "Glenwood", "Ashford", "Millbrook", "Stonebridge",
        "Clearwater", "Kingsport", "Bridgewater", "Hillcrest",
    ];

    public static readonly string[] Streets =
    [
        "Maple", "Oak", "Cedar", "Elm", "Pine", "Willow", "Birch", "Chestnut", "Walnut", "Spruce",
        "Lakeview", "Hillside", "Meadow", "Sunset", "Ridge", "Park", "Highland", "Orchard", "River", "Forest",
    ];

    public static readonly string[] StreetSuffixes = ["Street", "Avenue", "Road", "Lane", "Drive", "Court", "Place", "Way"];

    public static readonly string[] Organizations =
    [
        "Riverside Medical Center", "Lakeview General Hospital", "Northfield Clinic", "Cedar Valley Health",
        "Saint Brendan Hospital", "Westbrook Family Practice", "Harbor Point Medical Group", "Summit Regional Hospital",
        "Oak Hollow Health Center", "Greenfield Community Clinic",
    ];
}
