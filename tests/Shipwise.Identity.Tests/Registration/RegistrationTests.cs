using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Shipwise.Identity.Application.Features.Auth.Commands.RegisterUser;
using Shipwise.Identity.Domain.Models;
using Shipwise.SharedKernel.Exceptions;
using Xunit;

namespace Shipwise.Identity.Tests.Registration;

public sealed class RegistrationTests
{
    private static readonly string[] Fields = ["email", "displayName", "password"];

    [Fact]
    public void ModelTrimsAndNormalizesEmailAndValidatesName()
    {
        var user = User.Create(" Ana@Example.com ", " Ana ");
        Assert.Equal("Ana@Example.com", user.Email);
        Assert.Equal("ana@example.com", user.NormalizedEmail);
        Assert.Equal("Ana", user.DisplayName);
        Assert.Throws<ValidationException>(() => User.Create("bad", "Ana"));
        Assert.Throws<ValidationException>(() => User.Create("ana@example.com", " "));
        Assert.Throws<ValidationException>(() => User.Create("ana@example.com", new string('a', 101)));
    }

    [Fact]
    public async Task ValidatorCollectsErrorsAndCommandHidesPassword()
    {
        var command = new RegisterUserCommand("bad", " ", "secret");
        var exception = await Assert.ThrowsAsync<ValidationException>(() => new RegisterUserCommandValidator().ValidateAsync(command, CancellationToken.None));
        Assert.Equal(Fields, exception.Errors.Select(error => error.PropertyName));
        Assert.DoesNotContain("secret", command.ToString());
    }

    [Theory]
    [InlineData("a", "Name", "long-password", "email")]
    [InlineData("a@@b.test", "Name", "long-password", "email")]
    [InlineData("a@b", "Name", "long-password", "email")]
    [InlineData("a b@c.test", "Name", "long-password", "email")]
    [InlineData("a@b.test", "bad\u0001name", "long-password", "displayName")]
    [InlineData("a@b.test", "Name", "short", "password")]
    public async Task ValidatorRejectsInvalidFields(string email, string name, string password, string field)
    {
        var validator = new RegisterUserCommandValidator();
        var exception = await Assert.ThrowsAsync<ValidationException>(() => validator.ValidateAsync(
            new RegisterUserCommand(email, name, password), CancellationToken.None));
        Assert.Contains(exception.Errors, error => error.PropertyName == field);
    }

    [Fact]
    public async Task ValidatorChecksLengthsWithoutTrimmingPassword()
    {
        var validator = new RegisterUserCommandValidator();
        await validator.ValidateAsync(new RegisterUserCommand("a@b.test", "Name", new string(' ', 12)), CancellationToken.None);
        var exception = await Assert.ThrowsAsync<ValidationException>(() => validator.ValidateAsync(
            new RegisterUserCommand(new string('a', 251) + "@b.c", new string('n', 101), new string('p', 129)), CancellationToken.None));
        Assert.Equal(3, exception.Errors.Count);
    }

    [Fact]
    public async Task InvalidFieldsReturnAllFieldErrors()
    {
        await using var factory = new RegistrationHostFactory();
        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/register", new { email = "bad", displayName = " ", password = "short" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<JsonObject>())!;
        JsonNode error = body["error"] !;
        Assert.Equal("Validation.Failure", error["code"] !.GetValue<string>());
        JsonNode errors = error["errors"] !;
        Assert.All(Fields, field => Assert.NotNull(errors[field]));
    }

    [Fact]
    public async Task MalformedJsonReturnsValidationFailure()
    {
        await using var factory = new RegistrationHostFactory();
        using var client = factory.CreateClient();
        using var content = new StringContent("{", System.Text.Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/api/v1/auth/register", content);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<JsonObject>())!;
        JsonNode error = body["error"] !;
        Assert.Equal("Validation.Failure", error["code"] !.GetValue<string>());
    }

    [Fact]
    public async Task DisabledTakesPrecedenceOverRateLimitAndValidation()
    {
        await using var factory = new RegistrationHostFactory(enabled: false);
        using var client = factory.CreateClient();
        for (int i = 0; i < 7; i++)
        {
            var response = await client.PostAsJsonAsync("/api/v1/auth/register", new { email = "bad" });
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            var body = (await response.Content.ReadFromJsonAsync<JsonObject>())!;
            JsonNode error = body["error"] !;
            Assert.Equal("Request.NotFound", error["code"] !.GetValue<string>());
        }
    }

    [Fact]
    public async Task SixthRequestIsRateLimited()
    {
        await using var factory = new RegistrationHostFactory();
        using var client = factory.CreateClient();
        for (int i = 0; i < 5; i++)
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/auth/register", new { email = "bad" })).StatusCode);
        }

        var response = await client.PostAsJsonAsync("/api/v1/auth/register", new { email = "bad" });
        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.NotNull(response.Headers.RetryAfter);
        var body = (await response.Content.ReadFromJsonAsync<JsonObject>())!;
        JsonNode error = body["error"] !;
        Assert.Equal("Request.TooManyRequests", error["code"] !.GetValue<string>());
    }
}
