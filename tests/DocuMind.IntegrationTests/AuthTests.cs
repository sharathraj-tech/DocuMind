using DocuMind.Application.Auth;
using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using Xunit;

namespace DocuMind.IntegrationTests
{
    public class AuthTests(ApiFactory factory):IClassFixture<ApiFactory>
    {
        private const string Password = "CorrectHash1";
        private readonly HttpClient _http = factory.CreateClient();

        private async Task<(string Email,AuthResponse Auth)> RegisterAsync()
        {
            var email = $"{Guid.NewGuid():N}@test.dev";
            var res = await _http.PostAsJsonAsync("/api/auth/register",
                new RegisterRequest(email, Password, "Tester", "Acme Inc"));
            res.StatusCode.Should().Be(HttpStatusCode.OK);
            return (email, (await res.Content.ReadFromJsonAsync<AuthResponse>())!);
        }

        private Task<HttpResponseMessage> RefreshAsync(string token)=>
            _http.PostAsJsonAsync("/api/auth/refresh",new RefreshRequest(token));
        
        private Task<HttpResponseMessage> MeAsync(string accessToken)
        {
            var req = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
            req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            return _http.SendAsync(req);
        }

        [Fact]
        public async Task Register_creates_workspace_and_returns_tokens()
        {
            var (_, auth) = await RegisterAsync();
            auth.AccessToken.Should().NotBeNullOrEmpty();
            auth.RefreshToken.Should().NotBeNullOrEmpty();
            auth.TenantId.Should().NotBeEmpty();
            auth.Role.Should().Be("Admin");
        }

        [Fact]
        public async Task Me_without_token_is_unauthorized()
        {
            var res = await _http.GetAsync("/api/auth/me");
            res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task Me_returns_tenant_from_token_claims()
        {
            var (email, auth) = await RegisterAsync();

            var res = await MeAsync(auth.AccessToken);
            res.StatusCode.Should().Be(HttpStatusCode.OK);

            var me = (await res.Content.ReadFromJsonAsync<CurrentUserDto>())!;
            me.TenantId.Should().Be(auth.TenantId);
            me.Email.Should().Be(email);
            me.Role.Should().Be("Admin");
        }

        [Fact]
        public async Task Each_registration_gets_its_own_tenant()
        {
            var (_, a) = await RegisterAsync();
            var (_, b) = await RegisterAsync();

            a.TenantId.Should().NotBe(b.TenantId);
        }

        [Fact]
        public async Task Duplicate_email_is_conflict()
        {
            var (email, _) = await RegisterAsync();

            var res = await _http.PostAsJsonAsync("/api/auth/register",
                new RegisterRequest(email, Password, null, "Other"));

            res.StatusCode.Should().Be(HttpStatusCode.Conflict);
        }

        [Fact]
        public async Task Wrong_password_is_unauthorized()
        {
            var (email, _) = await RegisterAsync();

            var res = await _http.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "WrongPassword1"));

            res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task Refresh_rotates_the_token()
        {
            var (_, first) = await RegisterAsync();

            var res = await RefreshAsync(first.RefreshToken);
            res.StatusCode.Should().Be(HttpStatusCode.OK);

            var second = (await res.Content.ReadFromJsonAsync<AuthResponse>())!;
            second.RefreshToken.Should().NotBe(first.RefreshToken);
            second.TenantId.Should().Be(first.TenantId);
        }

        [Fact]
        public async Task Replaying_a_rotated_token_revokes_the_whole_chain()
        {
            var (_, first) = await RegisterAsync();

            var second = (await (await RefreshAsync(first.RefreshToken)).Content.ReadFromJsonAsync<AuthResponse>())!;

            (await RefreshAsync(first.RefreshToken)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            (await RefreshAsync(second.RefreshToken)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }
    }
}
