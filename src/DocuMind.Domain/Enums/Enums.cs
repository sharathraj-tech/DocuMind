using System;
using System.Collections.Generic;
using System.Text;

namespace DocuMind.Domain.Enums
{
    public enum DocumentStatus
    {
        Pending,
        Processing,
        Ready,
        Failed
    }

    public enum MessageRole
    {
        User,
        Assistant,
        System
    }

    public enum TenantRole
    {
        Member,
        Admin
    }
}
