using System.Net;
using System.Net.Http.Json;
using System.Text;
using Otp.Api.Contracts;
using Otp.Api.IntegrationTests.Infrastructure;

namespace Otp.Api.IntegrationTests.Endpoints;

public sealed class OtpEndpointsTests : IClassFixture<OtpApiFactory>, IDisposable
{
    internal const string GenerateUrl = "/api/otp/generate";
    internal const string ValidateUrl = "/api/otp/validate";

    private readonly OtpApiFactory _factory;
    private readonly HttpClient _client;

    public OtpEndpointsTests(OtpApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public void Dispose() => _client.Dispose();

    private static string NewUserId() => Guid.NewGuid().ToString("N")[..16];

    private static string WrongCodeFor(string code) => code == "123456" ? "654321" : "123456";

    private static StringContent Json(string json) => new(json, Encoding.UTF8, "application/json");

    private Task<HttpResponseMessage> PostGenerateAsync(string userId, DateTimeOffset? dateTime = null) =>
        _client.PostAsJsonAsync(GenerateUrl, new { userId, dateTime = dateTime ?? _factory.Clock.GetUtcNow() });

    private Task<HttpResponseMessage> PostValidateAsync(string userId, string code) =>
        _client.PostAsJsonAsync(ValidateUrl, new { userId, code });

    private async Task<GenerateOtpResponse> GenerateAsync(string userId, DateTimeOffset? dateTime = null)
    {
        var response = await PostGenerateAsync(userId, dateTime);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<GenerateOtpResponse>())!;
    }

    private async Task<ValidateOtpResponse> ValidateAsync(string userId, string code)
    {
        var response = await PostValidateAsync(userId, code);
        response.StatusCode.Should().Be(HttpStatusCode.OK, "business outcomes are reported in the body, not as HTTP errors");
        return (await response.Content.ReadFromJsonAsync<ValidateOtpResponse>())!;
    }

    // ------------------------------------------------------------------ generate

    [Fact]
    public async Task Generate_ValidRequest_ReturnsOkWithASixDigitCodeValidForThirtySeconds()
    {
        var userId = NewUserId();

        var body = await GenerateAsync(userId);

        body.UserId.Should().Be(userId);
        body.Code.Should().MatchRegex("^[0-9]{6}$");
        body.ValiditySeconds.Should().Be(30);
        body.RemainingSeconds.Should().Be(30);
        body.IssuedAt.Should().Be(_factory.Clock.GetUtcNow());
        (body.ExpiresAt - body.IssuedAt).Should().Be(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public async Task Generate_ValidRequest_RespondsWithJson()
    {
        var response = await PostGenerateAsync(NewUserId());

        response.Content.Headers.ContentType!.MediaType.Should().Be("application/json");
    }

    [Fact]
    public async Task Generate_SameUserAndDateTime_ReturnsTheSameCode()
    {
        var userId = NewUserId();
        var dateTime = new DateTimeOffset(2026, 10, 16, 12, 30, 0, TimeSpan.Zero);

        var first = await GenerateAsync(userId, dateTime);
        var second = await GenerateAsync(userId, dateTime);

        second.Code.Should().Be(first.Code);
    }

    [Fact]
    public async Task Generate_SameInstantWithADifferentOffset_ReturnsTheSameCode()
    {
        var userId = NewUserId();
        var utc = new DateTimeOffset(2026, 10, 16, 12, 30, 0, TimeSpan.Zero);
        var plusTwo = new DateTimeOffset(2026, 10, 16, 14, 30, 0, TimeSpan.FromHours(2));

        var first = await GenerateAsync(userId, utc);
        var second = await GenerateAsync(userId, plusTwo);

        second.Code.Should().Be(first.Code);
    }

    [Fact]
    public async Task Generate_DateTimeFarInTheFuture_StillExpiresThirtySecondsFromNow()
    {
        var body = await GenerateAsync(NewUserId(), _factory.Clock.GetUtcNow().AddYears(5));

        body.ExpiresAt.Should().Be(_factory.Clock.GetUtcNow().AddSeconds(30));
    }

    [Fact]
    public async Task Generate_UserIdWithSurroundingWhitespace_IsTrimmed()
    {
        var userId = NewUserId();

        var body = await GenerateAsync($"  {userId}  ");

        body.UserId.Should().Be(userId);
    }

    [Fact]
    public async Task Generate_UserIdOfExactlyThirtyCharacters_IsAccepted()
    {
        var response = await PostGenerateAsync(new string('a', 30));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Generate_BlankUserId_ReturnsBadRequest(string userId)
    {
        var response = await PostGenerateAsync(userId);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().ContainEquivalentOf("userId");
    }

    [Fact]
    public async Task Generate_UserIdLongerThanThirtyCharacters_ReturnsBadRequest()
    {
        var response = await PostGenerateAsync(new string('a', 31));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Generate_MissingDateTime_ReturnsBadRequest()
    {
        var response = await _client.PostAsync(GenerateUrl, Json("""{"userId":"alice"}"""));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().ContainEquivalentOf("dateTime");
    }

    [Fact]
    public async Task Generate_MalformedDateTime_ReturnsBadRequest()
    {
        var response = await _client.PostAsync(GenerateUrl, Json("""{"userId":"alice","dateTime":"not-a-date"}"""));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Generate_EmptyJsonObject_ReturnsBadRequestAsProblemDetails()
    {
        var response = await _client.PostAsync(GenerateUrl, Json("{}"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task Generate_BodyThatIsNotJson_ReturnsBadRequest()
    {
        var response = await _client.PostAsync(GenerateUrl, Json("this is not json"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Generate_NonJsonContentType_ReturnsUnsupportedMediaType()
    {
        var content = new StringContent("userId=alice", Encoding.UTF8, "text/plain");

        var response = await _client.PostAsync(GenerateUrl, content);

        response.StatusCode.Should().Be(HttpStatusCode.UnsupportedMediaType);
    }

    [Fact]
    public async Task Generate_GetRequest_ReturnsMethodNotAllowed()
    {
        var response = await _client.GetAsync(GenerateUrl);

        response.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
    }

    // ------------------------------------------------------------------ validate

    [Fact]
    public async Task Validate_CorrectCode_ReturnsValid()
    {
        var userId = NewUserId();
        var otp = await GenerateAsync(userId);

        var body = await ValidateAsync(userId, otp.Code);

        body.IsValid.Should().BeTrue();
        body.Status.Should().Be("Valid");
        body.Message.Should().Be("The one-time password is valid.");
        body.RemainingAttempts.Should().BeNull();
        body.RetryAfterSeconds.Should().BeNull();
    }

    [Fact]
    public async Task Validate_CorrectCodeTwice_SecondAttemptIsNotFound()
    {
        var userId = NewUserId();
        var otp = await GenerateAsync(userId);
        await ValidateAsync(userId, otp.Code);

        var second = await ValidateAsync(userId, otp.Code);

        second.IsValid.Should().BeFalse();
        second.Status.Should().Be("NotFound");
        second.Message.Should().Contain("No active one-time password");
    }

    [Fact]
    public async Task Validate_CodeWithSurroundingWhitespace_IsAccepted()
    {
        var userId = NewUserId();
        var otp = await GenerateAsync(userId);

        var body = await ValidateAsync(userId, $"  {otp.Code}  ");

        body.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_UnknownUser_ReturnsNotFoundWithHttpOk()
    {
        var body = await ValidateAsync(NewUserId(), "123456");

        body.Status.Should().Be("NotFound");
    }

    [Fact]
    public async Task Validate_WrongCode_ReturnsInvalidWithTheRemainingAttempts()
    {
        var userId = NewUserId();
        var otp = await GenerateAsync(userId);

        var body = await ValidateAsync(userId, WrongCodeFor(otp.Code));

        body.IsValid.Should().BeFalse();
        body.Status.Should().Be("Invalid");
        body.RemainingAttempts.Should().Be(2);
        body.Message.Should().Be("The one-time password is incorrect. 2 attempt(s) remaining.");
    }

    [Fact]
    public async Task Validate_WrongCodeFollowedByTheCorrectCode_StillSucceeds()
    {
        var userId = NewUserId();
        var otp = await GenerateAsync(userId);
        await ValidateAsync(userId, WrongCodeFor(otp.Code));

        var body = await ValidateAsync(userId, otp.Code);

        body.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_JustBeforeThirtySeconds_IsStillValid()
    {
        var userId = NewUserId();
        var otp = await GenerateAsync(userId);
        _factory.Clock.Advance(TimeSpan.FromMilliseconds(29_999));

        var body = await ValidateAsync(userId, otp.Code);

        body.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_AtExactlyThirtySeconds_ReturnsExpired()
    {
        var userId = NewUserId();
        var otp = await GenerateAsync(userId);
        _factory.Clock.Advance(TimeSpan.FromSeconds(30));

        var body = await ValidateAsync(userId, otp.Code);

        body.IsValid.Should().BeFalse();
        body.Status.Should().Be("Expired");
        body.Message.Should().Contain("expired");
    }

    [Fact]
    public async Task Validate_ThreeWrongCodes_LocksTheUserOut()
    {
        var userId = NewUserId();
        var wrong = WrongCodeFor((await GenerateAsync(userId)).Code);

        var first = await ValidateAsync(userId, wrong);
        var second = await ValidateAsync(userId, wrong);
        var third = await ValidateAsync(userId, wrong);

        first.RemainingAttempts.Should().Be(2);
        second.RemainingAttempts.Should().Be(1);
        third.Status.Should().Be("AttemptsExceeded");
        third.RetryAfterSeconds.Should().Be(60);
        third.Message.Should().Be("Too many incorrect attempts. Please try again in 60 second(s).");
    }

    [Fact]
    public async Task Validate_WhileLockedOut_IsRefusedWithTheRemainingWait()
    {
        var userId = NewUserId();
        var otp = await GenerateAsync(userId);
        var wrong = WrongCodeFor(otp.Code);
        for (var attempt = 0; attempt < 3; attempt++)
        {
            await ValidateAsync(userId, wrong);
        }

        _factory.Clock.Advance(TimeSpan.FromSeconds(20));
        var body = await ValidateAsync(userId, otp.Code);

        body.IsValid.Should().BeFalse();
        body.Status.Should().Be("AttemptsExceeded");
        body.RetryAfterSeconds.Should().Be(40);
    }

    [Fact]
    public async Task Validate_AfterTheLockoutEnds_AFreshPasswordWorksAgain()
    {
        var userId = NewUserId();
        var wrong = WrongCodeFor((await GenerateAsync(userId)).Code);
        for (var attempt = 0; attempt < 3; attempt++)
        {
            await ValidateAsync(userId, wrong);
        }

        _factory.Clock.Advance(TimeSpan.FromSeconds(60));
        var fresh = await GenerateAsync(userId);
        var body = await ValidateAsync(userId, fresh.Code);

        body.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("abc123")]
    [InlineData("123")]
    [InlineData("1234567890")]
    [InlineData("")]
    [InlineData("12 34")]
    public async Task Validate_CodeThatIsNotFourToNineDigits_ReturnsBadRequest(string code)
    {
        var response = await PostValidateAsync(NewUserId(), code);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Validate_MissingUserId_ReturnsBadRequest()
    {
        var response = await _client.PostAsync(ValidateUrl, Json("""{"code":"123456"}"""));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Validate_UserIdLongerThanThirtyCharacters_ReturnsBadRequest()
    {
        var response = await PostValidateAsync(new string('a', 31), "123456");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Validate_ManyConcurrentRequestsWithTheCorrectCode_ExactlyOneSucceeds()
    {
        var userId = NewUserId();
        var otp = await GenerateAsync(userId);

        var results = await Task.WhenAll(
            Enumerable.Range(0, 20).Select(_ => ValidateAsync(userId, otp.Code)));

        results.Count(result => result.IsValid).Should().Be(1);
    }

    // --------------------------------------------------------- generate while locked

    [Fact]
    public async Task Generate_WhileLockedOut_ReturnsTooManyRequestsWithARetryAfterHeader()
    {
        var userId = NewUserId();
        var wrong = WrongCodeFor((await GenerateAsync(userId)).Code);
        for (var attempt = 0; attempt < 3; attempt++)
        {
            await ValidateAsync(userId, wrong);
        }

        var response = await PostGenerateAsync(userId);

        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        response.Headers.GetValues("Retry-After").Should().ContainSingle().Which.Should().Be("60");
        response.Content.Headers.ContentType!.MediaType.Should().Contain("json");
        (await response.Content.ReadAsStringAsync()).Should().Contain("Too many incorrect attempts. Please try again in 60 second(s).");
    }

    [Fact]
    public async Task Generate_AfterTheLockoutEnds_SucceedsAgain()
    {
        var userId = NewUserId();
        var wrong = WrongCodeFor((await GenerateAsync(userId)).Code);
        for (var attempt = 0; attempt < 3; attempt++)
        {
            await ValidateAsync(userId, wrong);
        }

        _factory.Clock.Advance(TimeSpan.FromSeconds(61));
        var response = await PostGenerateAsync(userId);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Generate_ForAnotherUser_IsNotAffectedBySomeoneElsesLockout()
    {
        var locked = NewUserId();
        var wrong = WrongCodeFor((await GenerateAsync(locked)).Code);
        for (var attempt = 0; attempt < 3; attempt++)
        {
            await ValidateAsync(locked, wrong);
        }

        var response = await PostGenerateAsync(NewUserId());

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
