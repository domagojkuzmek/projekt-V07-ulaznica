using System.Text.Encodings.Web;
using System.Text.Unicode;
using Ulaznica.Api.Autentikacija;
using Ulaznica.Api.Infrastruktura;
using Ulaznica.Api.Prodaja;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Hrvatski znakovi (č, ć, đ, š, ž) u JSON odgovorima ostaju čitljivi.
var jsonEncoder = JavaScriptEncoder.Create(
    UnicodeRanges.BasicLatin, UnicodeRanges.Latin1Supplement, UnicodeRanges.LatinExtendedA);

builder.Services
    .AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Encoder = jsonEncoder)
    .ConfigureApiBehaviorOptions(options => options.InvalidModelStateResponseFactory =
        _ => new BadRequestObjectResult(new { greska = "Neispravan zahtjev." }));
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Encoder = jsonEncoder);

builder.Services.AddSingleton<PostavkeAplikacije>();
builder.Services.AddSingleton<Baza>();
builder.Services.AddSingleton<Posta>();
builder.Services.AddSingleton<LozinkeServis>();
builder.Services.AddSingleton<KorisniciServis>();
builder.Services.AddSingleton<SesijeKorisnika>();
builder.Services.AddSingleton<ResetLozinkeServis>();
builder.Services.AddSingleton<NaplataKlijent>();

// Web sučelje (npr. Vite na portu 5173) poziva API s drugog origina.
builder.Services.AddCors();
builder.Services.AddOptions<CorsOptions>()
    .Configure<PostavkeAplikacije>((options, postavke) => options.AddDefaultPolicy(policy => policy
        .WithOrigins(postavke.DozvoljeniOrigini)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()));

// Ograniči pokušaje na javnim autentikacijskim rutama radi zaštite od pogađanja lozinki
// i zloupotrebe zahtjeva za obnovu lozinke.
builder.Services.AddRateLimiter(_ => { });
builder.Services.AddOptions<RateLimiterOptions>().Configure<IConfiguration>((options, configuration) =>
{
    var limit = configuration.GetValue("Auth:PokusajaUMinuti", 5);
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = Math.Clamp(limit, 1, 100),
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        }));
    options.OnRejected = async (context, cancellationToken) =>
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter =
                ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
        await context.HttpContext.Response.WriteAsJsonAsync(
            new { greska = "Previše pokušaja. Pokušajte ponovno kasnije." }, cancellationToken);
    };
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

// Mape za dokumente i poštu, shema baze i početni podaci.
Inicijalizacija.Pokreni(app.Services);

app.Use(async (context, next) =>
{
    context.Response.Headers["Content-Security-Policy"] =
        "default-src 'none'; style-src 'unsafe-inline'; form-action 'self'; base-uri 'none'; object-src 'none'; frame-ancestors 'none'";
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    context.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
    context.Response.Headers["Cache-Control"] = "no-store";
    await next();
});

app.UseExceptionHandler(greske => greske.Run(async context =>
{
    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
    await context.Response.WriteAsJsonAsync(new { greska = "Došlo je do pogreške." });
}));
app.UseRouting();
app.UseRateLimiter();
app.UseCors();
app.UseMiddleware<SesijaMiddleware>();
app.MapControllers();

app.Run();

/// <summary>Omogućuje <c>WebApplicationFactory&lt;Program&gt;</c> u integracijskim testovima.</summary>
public partial class Program
{
}
