using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Okviri.Api.Obavijesti;
using Okviri.Api.Prijava;
using Okviri.Api.Rezervacije;
using Okviri.Api.Sigurnost;

var builder = WebApplication.CreateBuilder(args);
// U okruženju Development CreateBuilder automatski čita i user-secrets (UserSecretsId u .csproj).

builder.Services.AddControllers();

// ---- Tajne i konfiguracija: provjera pri pokretanju (fail fast), ne tek kod prvog poziva ----
builder.Services.AddOptions<ObavijestiPostavke>()
    .BindConfiguration("Obavijesti")
    .ValidateDataAnnotations()
    .ValidateOnStart();

// ---- 1. Autentikacija: JWT od Keycloaka (umjesto vlastitih tokena iz T2) ----
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IConfiguration>((o, cfg) =>
    {
        o.Authority = cfg["Keycloak:Authority"];
        o.Audience = cfg["Keycloak:Audience"];
        o.RequireHttpsMetadata = cfg.GetValue("Keycloak:RequireHttpsMetadata", true);
        o.MapInboundClaims = false;                                  // nazivi claimova kao u tokenu
        o.TokenValidationParameters.NameClaimType = "preferred_username";
        o.TokenValidationParameters.RoleClaimType = KeycloakUloge.TipUloge;
    });
builder.Services.AddTransient<IClaimsTransformation, KeycloakUloge>(); // realm_access.roles -> uloge

// ---- 2. Autorizacija: politike + resursna provjera vlasništva ----
builder.Services.AddAuthorizationBuilder()
    // Zadano ZABRANJENO: svaki endpoint bez [AllowAnonymous] traži prijavljenog korisnika.
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
    .AddPolicy(Politike.Admin, p => p.RequireRole("ADMIN"))
    .AddPolicy(Politike.VlasnikIliAdmin, p => p.AddRequirements(new VlasnikIliAdminZahtjev()));
builder.Services.AddSingleton<IAuthorizationHandler, VlasnikRezervacijeHandler>();
builder.Services.AddSingleton<IAuthorizationMiddlewareResultHandler, JsonOdgovorAutorizacije>();

// ---- 3. Ograničenje broja pokušaja prijave (ugrađeni rate limiter) ----
builder.Services.AddRateLimiter(_ => { });
builder.Services.AddOptions<RateLimiterOptions>().Configure<IConfiguration>((o, cfg) =>
{
    var pokusaja = cfg.GetValue("Prijava:PokusajaUMinuti", 5);
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy(Politike.Prijava, http => RateLimitPartition.GetFixedWindowLimiter(
        http.Connection.RemoteIpAddress?.ToString() ?? "nepoznato",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = pokusaja, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    o.OnRejected = async (ctx, ct) =>
    {
        if (ctx.Lease.TryGetMetadata(MetadataName.RetryAfter, out var cekaj))
        {
            ctx.HttpContext.Response.Headers.RetryAfter = ((int)Math.Ceiling(cekaj.TotalSeconds)).ToString();
        }
        await ctx.HttpContext.Response.WriteAsJsonAsync(
            new { greska = "Previše pokušaja prijave. Pokušajte ponovno kasnije." }, ct);
    };
});

// ---- 4. CORS iz konfiguracije (Cors:AllowedOrigins) – samo navedeni origini ----
builder.Services.AddCors();
builder.Services.AddOptions<CorsOptions>().Configure<IConfiguration>((o, cfg) =>
    o.AddDefaultPolicy(p => p
        .WithOrigins(cfg.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
        .WithMethods("GET", "POST", "DELETE")
        .WithHeaders("Authorization", "Content-Type")));

// ---- 5. Lozinke: PasswordHasher<T> (PBKDF2, sol, iteracije) uz migraciju starih SHA-256 hasheva ----
builder.Services.AddSingleton<IPasswordHasher<LokalniKorisnik>, HasherSMigracijom>();
builder.Services.AddSingleton<LokalniKorisnici>();

builder.Services.AddSingleton<RezervacijeRepozitorij>();
builder.Services.AddSingleton<ObavijestiKlijent>();

var app = builder.Build();

app.UseSigurnosnaZaglavlja();
app.UseExceptionHandler(greske => greske.Run(async ctx =>
{
    ctx.Response.StatusCode = StatusCodes.Status500InternalServerError;
    await ctx.Response.WriteAsJsonAsync(new { greska = "Došlo je do pogreške." });
}));
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

/// <summary>Omogućuje <c>WebApplicationFactory&lt;Program&gt;</c> u testovima.</summary>
public partial class Program
{
}
