using System.Globalization;
using System.Text.Json;

namespace DeidSharp.Verification;

/// <summary>
/// Flattens a JSON document into the values a <see cref="LeakScanner"/> scans: every
/// string value (and, when asked, every property name), each with its path, written like
/// "$.conditions[0].name". Numbers, booleans and nulls hold no text and are skipped.
/// </summary>
internal static class JsonStringCollector
{
    public static List<ScannedValue> Collect(JsonElement root, LeakScanOptions options)
    {
        var values = new List<ScannedValue>();
        Collect(root, "$", options, values);
        return values;
    }

    private static void Collect(JsonElement element, string path, LeakScanOptions options, List<ScannedValue> values)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                CollectObject(element, path, options, values);
                break;
            case JsonValueKind.Array:
                var index = 0;
                foreach (var item in element.EnumerateArray())
                {
                    Collect(item, string.Create(CultureInfo.InvariantCulture, $"{path}[{index++}]"), options, values);
                }

                break;
            case JsonValueKind.String:
                values.Add(new ScannedValue(path, element.GetString() ?? string.Empty));
                break;
            case JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False or JsonValueKind.Null or JsonValueKind.Undefined:
                // No text to scan.
                break;
        }
    }

    private static void CollectObject(JsonElement element, string path, LeakScanOptions options, List<ScannedValue> values)
    {
        var position = 0;
        foreach (var property in element.EnumerateObject())
        {
            var childPath = $"{path}.{PathName(property.Name, position++, options)}";
            if (options.ScanPropertyNames)
            {
                values.Add(new ScannedValue(childPath + "(name)", property.Name));
            }

            Collect(property.Value, childPath, options, values);
        }
    }

    // A name outside the allowed list is replaced by its position: paths end up in logs,
    // and a key written by a model can itself be PHI.
    private static string PathName(string name, int position, LeakScanOptions options) =>
        options.PathPropertyNames is null || options.PathPropertyNames.Contains(name)
            ? name
            : string.Create(CultureInfo.InvariantCulture, $"<property {position}>");
}
