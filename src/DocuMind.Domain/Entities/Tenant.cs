using DocuMind.Domain.Common;
using DocuMind.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace DocuMind.Domain.Entities
{
    public class Tenant:BaseEntity
    {
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public List<TenantMember> Members { get; set; } = [];
    }

    public class TenantMember
    {
        public Guid TenantId { get; set; }
        public Tenant Tenant { get; set; } = null!;
        public Guid UserId { get; set; }
        public TenantRole Role { get; set; } = TenantRole.Member;
        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
    }
}
