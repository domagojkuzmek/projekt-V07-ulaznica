using Microsoft.AspNetCore.Authorization;
using Okviri.Api.Rezervacije;

namespace Okviri.Api.Sigurnost;

/// <summary>Zahtjev politike "VlasnikIliAdmin" (resursna autorizacija).</summary>
public sealed class VlasnikIliAdminZahtjev : IAuthorizationRequirement
{
}

/// <summary>
/// Pravilo vlasništva na JEDNOM mjestu: ADMIN smije sve, ostali samo svoje rezervacije.
/// Poziva se s resursom: <c>authorizationService.AuthorizeAsync(User, rezervacija, Politike.VlasnikIliAdmin)</c>.
/// </summary>
public sealed class VlasnikRezervacijeHandler : AuthorizationHandler<VlasnikIliAdminZahtjev, Rezervacija>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, VlasnikIliAdminZahtjev requirement, Rezervacija resurs)
    {
        if (context.User.IsInRole("ADMIN") || resurs.Pacijent == context.User.Identity?.Name)
        {
            context.Succeed(requirement);
        }
        return Task.CompletedTask;
    }
}
