using System;
using System.Collections.Generic;
using System.Text;

namespace DocuMind.Application.Abstractions
{
    public interface ICurrentUser
    {
        Guid? UserId { get; }
        bool IsAdmin { get; }
    }
}
