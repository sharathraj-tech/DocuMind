using System;
using System.Collections.Generic;
using System.Text;

namespace DocuMind.Application.Abstractions
{
    public interface ICurrentTenant
    {
        Guid? TenantId { get; set; }
    }
}
