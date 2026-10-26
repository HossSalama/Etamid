using System.Text.Json;
using Etamid.Application.Common.Models;

namespace Etamid.Application.Tests;

public class ApiResponseTests
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    [Fact]
    public void Success_response_omits_errors_and_meta()
    {
        var json = JsonSerializer.Serialize(ApiResponse.Ok(new { id = 1 }, "ok"), Options);

        json.Should().Contain("\"success\":true");
        json.Should().NotContain("errors");
        json.Should().NotContain("meta");
    }

    [Fact]
    public void Paginated_response_includes_meta()
    {
        var json = JsonSerializer.Serialize(ApiResponse.Ok(Array.Empty<int>(), "ok", new PageMeta(1, 20, 0, 0)), Options);

        json.Should().Contain("\"meta\"");
        json.Should().Contain("\"pageSize\":20");
    }

    [Fact]
    public void Failure_response_includes_errors_and_null_data()
    {
        var response = ApiResponse.Fail("bad", [new ApiError
        {
            Code = "OTP_INVALID",
            Message = "bad",
            Field = "code",
            Extras = new() { ["retryAfterSeconds"] = 900 }
        }]);

        var json = JsonSerializer.Serialize(response, Options);

        json.Should().Contain("\"success\":false");
        json.Should().Contain("\"data\":null");
        json.Should().Contain("\"code\":\"OTP_INVALID\"");
        json.Should().Contain("\"retryAfterSeconds\":900");
        json.Should().NotContain("meta");
    }
}
