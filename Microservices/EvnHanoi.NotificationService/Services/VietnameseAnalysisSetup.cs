using Elastic.Clients.Elasticsearch.Analysis;
using Elastic.Clients.Elasticsearch.IndexManagement;

namespace EvnHanoi.NotificationService.Services;

/// <summary>
/// Analyzer tiếng Việt: vi_tokenizer (plugin elasticsearch-analysis-vietnamese) + asciifolding (built-in).
/// </summary>
public static class VietnameseAnalysisSetup
{
    public const string AnalyzerName = "vietnamese_analyzer";
    public const string SearchAnalyzerName = "vietnamese_search";

    private static readonly string[] VnSynonyms =
    [
        "MBA, máy biến áp, power transformer",
        "MC, máy cắt, circuit breaker",
        "DCL, dao cách ly, disconnector",
        "DTD, dao tiếp địa, earthing switch",
        "TBA, trạm biến áp, substation",
        "RMU, tủ RMU, ring main unit",
        "LBS, cầu dao phụ tải, load break switch",
        "FCO, cầu chì tự rơi, fuse cutout",
        "CBM, bảo dưỡng theo điều kiện, condition based maintenance",
        "GIS, gas insulated switchgear",
        "HGIS, hybrid gas insulated switchgear",
        "BU, biến điện áp, voltage transformer, VT, PT",
        "BI, biến dòng điện, current transformer, CT",
        "CSV, chống sét van, surge arrester",
        "XLPE, cáp ngầm, underground cable",
        "trạm, trạm biến áp",
        "máy, máy biến áp"
    ];

    private static readonly string[] VnStopwords =
    [
        "và", "của", "các", "là", "được", "cho", "trong",
        "có", "với", "từ", "đến", "này", "theo", "tại",
        "một", "những", "về", "đã", "khi", "trên", "bởi"
    ];

    public static void Configure(IndexSettingsAnalysisDescriptor analysis)
        => Configure(analysis, useViTokenizer: true);

    /// <summary>
    /// Fallback khi cluster chưa cài plugin elasticsearch-analysis-vietnamese (không có vi_tokenizer).
    /// Giữ tên analyzer vietnamese_* + asciifolding để search không dấu vẫn hoạt động.
    /// </summary>
    public static void ConfigureStandardTokenizer(IndexSettingsAnalysisDescriptor analysis)
        => Configure(analysis, useViTokenizer: false);

    private static void Configure(IndexSettingsAnalysisDescriptor analysis, bool useViTokenizer)
    {
        var tokenizer = useViTokenizer ? "vi_tokenizer" : "standard";

        analysis
            .CharFilters(cf => cf
                .Mapping("vn_char_mapping", m => m
                    .Mappings(["đ => d", "Đ => D"])
                )
            )
            .TokenFilters(tf => tf
                .Synonym("vn_synonym", sy => sy.Synonyms(VnSynonyms))
                .Stop("vn_stop", st => st.Stopwords(VnStopwords))
            )
            .Analyzers(an => an
                .Custom(AnalyzerName, ca => ca
                    .CharFilter(["vn_char_mapping"])
                    .Tokenizer(tokenizer)
                    .Filter(["lowercase", "vn_synonym", "asciifolding", "vn_stop"])
                )
                .Custom(SearchAnalyzerName, ca => ca
                    .CharFilter(["vn_char_mapping"])
                    .Tokenizer(tokenizer)
                    .Filter(["lowercase", "vn_synonym", "asciifolding"])
                )
            );
    }
}
