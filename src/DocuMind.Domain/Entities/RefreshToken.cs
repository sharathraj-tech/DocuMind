using DocuMind.Domain.Common;
using System;
using System.Collections.Generic;
using System.Text;

namespace DocuMind.Domain.Entities
{
    public class RefreshToken:BaseEntity
    {
        public Guid UserId { get; set; }
        public Guid TenantId { get; set; }
        public string TokenHash { get; set; } = string.Empty;
        public DateTime? ExpiresAt { get; set; }
        public DateTime? RevokedAt { get; set; }
    }
}
