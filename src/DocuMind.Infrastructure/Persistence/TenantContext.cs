using DocuMind.Application.Abstractions;
using System;
using System.Collections.Generic;
using System.Text;

namespace DocuMind.Infrastructure.Persistence
{
    public class TenantContext:ICurrentTenant
    {
        public Guid? TenantId { get; set; } 
    }
}
