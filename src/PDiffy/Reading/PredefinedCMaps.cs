namespace PDiffy.Reading;

/// <summary>
/// The CMaps every PDF reader has built in (ISO 32000-1, table 118), with the Adobe character collection each covers.
/// Identity-H and Identity-V cover none: they use a code as its CID in any font.
/// </summary>
internal static class PredefinedCMaps
{
    private static readonly Dictionary<string, string?> Collections = new()
    {
        ["Identity-H"] = null, ["Identity-V"] = null,
        ["GB-EUC-H"] = "GB1", ["GB-EUC-V"] = "GB1", ["GBpc-EUC-H"] = "GB1", ["GBpc-EUC-V"] = "GB1",
        ["GBK-EUC-H"] = "GB1", ["GBK-EUC-V"] = "GB1", ["GBKp-EUC-H"] = "GB1", ["GBKp-EUC-V"] = "GB1",
        ["GBK2K-H"] = "GB1", ["GBK2K-V"] = "GB1", ["UniGB-UCS2-H"] = "GB1", ["UniGB-UCS2-V"] = "GB1",
        ["UniGB-UTF16-H"] = "GB1", ["UniGB-UTF16-V"] = "GB1",
        ["B5pc-H"] = "CNS1", ["B5pc-V"] = "CNS1", ["HKscs-B5-H"] = "CNS1", ["HKscs-B5-V"] = "CNS1",
        ["ETen-B5-H"] = "CNS1", ["ETen-B5-V"] = "CNS1", ["ETenms-B5-H"] = "CNS1", ["ETenms-B5-V"] = "CNS1",
        ["CNS-EUC-H"] = "CNS1", ["CNS-EUC-V"] = "CNS1", ["UniCNS-UCS2-H"] = "CNS1", ["UniCNS-UCS2-V"] = "CNS1",
        ["UniCNS-UTF16-H"] = "CNS1", ["UniCNS-UTF16-V"] = "CNS1",
        ["83pv-RKSJ-H"] = "Japan1", ["90ms-RKSJ-H"] = "Japan1", ["90ms-RKSJ-V"] = "Japan1", ["90msp-RKSJ-H"] = "Japan1",
        ["90msp-RKSJ-V"] = "Japan1", ["90pv-RKSJ-H"] = "Japan1", ["Add-RKSJ-H"] = "Japan1", ["Add-RKSJ-V"] = "Japan1",
        ["EUC-H"] = "Japan1", ["EUC-V"] = "Japan1", ["Ext-RKSJ-H"] = "Japan1", ["Ext-RKSJ-V"] = "Japan1",
        ["H"] = "Japan1", ["V"] = "Japan1", ["UniJIS-UCS2-H"] = "Japan1", ["UniJIS-UCS2-V"] = "Japan1",
        ["UniJIS-UCS2-HW-H"] = "Japan1", ["UniJIS-UCS2-HW-V"] = "Japan1", ["UniJIS-UTF16-H"] = "Japan1",
        ["UniJIS-UTF16-V"] = "Japan1",
        ["KSC-EUC-H"] = "Korea1", ["KSC-EUC-V"] = "Korea1", ["KSCms-UHC-H"] = "Korea1", ["KSCms-UHC-V"] = "Korea1",
        ["KSCms-UHC-HW-H"] = "Korea1", ["KSCms-UHC-HW-V"] = "Korea1", ["KSCpc-EUC-H"] = "Korea1",
        ["UniKS-UCS2-H"] = "Korea1", ["UniKS-UCS2-V"] = "Korea1", ["UniKS-UTF16-H"] = "Korea1",
        ["UniKS-UTF16-V"] = "Korea1",
    };

    public static bool Contains(string name) => Collections.ContainsKey(name);

    public static string? CollectionOf(string name) => Collections.GetValueOrDefault(name);
}
