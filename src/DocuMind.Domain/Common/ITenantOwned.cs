using System;
using System.Collections.Generic;
using System.Text;

namespace DocuMind.Domain.Common
{
    public interface ITenantOwned
    {
        Guid TenantId { get; set; }
    }
}
