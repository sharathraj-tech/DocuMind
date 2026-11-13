using DocuMind.Infrastructure.Persistence;
using System.Security.Claims;

namespace DocuMind.Api.Middleware
{
    public sealed class TenantResolutionMiddleware(RequestDelegate next)
    {
        public async Task InvokeAsync(HttpContext context, TenantContext tenant)
        {
            if(context.User.Identity?.IsAuthenticated==true && Guid.TryParse(context.User.FindFirstValue("tenant_id"),out var tenantId))
            {
                tenant.TenantId = tenantId;
            }

            await next(context);
        }
    }
}
