namespace Okviri.Api.Sigurnost;

/// <summary>Sigurnosna zaglavlja za svaki odgovor (ASP.NET Core ih ne dodaje sam, osim HSTS-a).</summary>
public static class SigurnosnaZaglavlja
{
    public static IApplicationBuilder UseSigurnosnaZaglavlja(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            context.Response.OnStarting(() =>
            {
                var h = context.Response.Headers;
                h.ContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'";
                h.XContentTypeOptions = "nosniff";
                h.XFrameOptions = "DENY";
                h["Referrer-Policy"] = "no-referrer";
                h.CacheControl = "no-store";
                return Task.CompletedTask;
            });
            await next(context);
        });
}
