using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Text;

namespace DocuMind.Infrastructure.Identity
{
    public class ApplicationUser:IdentityUser<Guid>
    {
        public string? DisplayName { get; set; }
    }
}
