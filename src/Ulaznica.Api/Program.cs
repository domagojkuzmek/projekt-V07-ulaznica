using System.Text.Encodings.Web;
using System.Text.Unicode;
using Ulaznica.Api.Autentikacija;
using Ulaznica.Api.Infrastruktura;
using Ulaznica.Api.Prodaja;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Mvc;

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

var app = builder.Build();

// Mape za dokumente i poštu, shema baze i početni podaci.
Inicijalizacija.Pokreni(app.Services);

app.Use(async (context, next) =>
{
    context.Response.Headers["Content-Security-Policy"] =
        "default-src 'none'; style-src 'unsafe-inline'; form-action 'self'; base-uri 'none'; object-src 'none'; frame-ancestors 'none'";
    await next();
});

app.UseExceptionHandler(greske => greske.Run(async context =>
{
    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
    await context.Response.WriteAsJsonAsync(new { greska = "Došlo je do pogreške." });
}));
app.UseRouting();
app.UseCors();
app.UseMiddleware<SesijaMiddleware>();
app.MapControllers();

app.Run();

/// <summary>Omogućuje <c>WebApplicationFactory&lt;Program&gt;</c> u integracijskim testovima.</summary>
public partial class Program
{
}
