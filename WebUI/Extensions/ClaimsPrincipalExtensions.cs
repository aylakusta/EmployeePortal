using System.Security.Claims;

namespace WebUI
{
    public static class ClaimsPrincipalExtensions
    {
        public static string GetUserId(this ClaimsPrincipal user)
            => user.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
    }
}