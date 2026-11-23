using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Etamid.Api.IntegrationTests;

/// <summary>
/// GET /api/v1/support/faqs?search= : the FAQ search box. Search matches the
/// question or the answer, in Arabic or English, whatever the UI language is.
/// </summary>
public class FaqSearchTests(EtamidFactory factory) : IClassFixture<EtamidFactory>
{
    private readonly HttpClient _anon = factory.CreateClient();

    // ------------------------------------------------------------------ helpers
    private static async Task<JsonElement> Json(HttpResponseMessage r)
        => JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement;

    private async Task<JsonElement> Faqs(string? search = null, string? language = null)
    {
        var url = search is null ? "/api/v1/support/faqs" : $"/api/v1/support/faqs?search={Uri.EscapeDataString(search)}";
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (language is not null) request.Headers.Add("Accept-Language", language);
        var r = await _anon.SendAsync(request);
        r.StatusCode.Should().Be(HttpStatusCode.OK, await r.Content.ReadAsStringAsync());
        return (await Json(r)).GetProperty("data");
    }

    private static List<string> Questions(JsonElement data)
        => data.EnumerateArray().Select(f => f.GetProperty("question").GetString()!).ToList();

    // ------------------------------------------------------------------ tests
    [Fact]
    public async Task No_search_returns_all_six()
    {
        var body = await Faqs(language: "ar");

        body.GetArrayLength().Should().Be(6);
    }

    [Fact]
    public async Task Search_arabic_matches_question()
    {
        // "رسوم" is only in question 5 ("هل توجد رسوم لاستخدام المنصة؟").
        var body = await Faqs("رسوم", "ar");

        Questions(body).Should().Equal(["هل توجد رسوم لاستخدام المنصة؟"]);
    }

    [Fact]
    public async Task Search_arabic_matches_answer_too()
    {
        // "حساب" is in question 2 ("إنشاء حساب جديد؟") and in the answers of
        // questions 2 ("بيانات حسابك"، "صفحة حسابي")، 3 and 4 ("حساب الضمان").
        // Questions 5 and 6 have no "حساب" anywhere: Q6's answer is about
        // "صفحة صفقاتي" and "صفحة طلبات الدعم", not "صفحة حسابي".
        var body = await Faqs("حساب", "ar");

        Questions(body).Should().Equal([
            "كيف يمكنني إنشاء حساب جديد؟",
            "كيف يمكنني الدفع في المنصة؟",
            "هل بياناتي الشخصية والمالية آمنة؟"]);
    }

    [Fact]
    public async Task Search_english_matches_while_ui_is_english()
    {
        var body = await Faqs("fees", "en");

        Questions(body).Should().Equal(["Are there fees for using the platform?"]);
    }

    [Fact]
    public async Task Search_is_case_insensitive()
    {
        var body = await Faqs("FEES", "en");

        Questions(body).Should().Equal(["Are there fees for using the platform?"]);
    }

    [Fact]
    public async Task Search_no_match_returns_empty_list()
    {
        var body = await Faqs("zzz-no-such-question", "ar");

        body.GetArrayLength().Should().Be(0);
    }
}
