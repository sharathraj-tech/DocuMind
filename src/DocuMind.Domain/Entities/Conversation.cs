using DocuMind.Domain.Common;
using DocuMind.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace DocuMind.Domain.Entities
{
    public class Conversation:BaseEntity,ITenantOwned
    {
        public Guid TenantId { get; set; }
        public Guid UserId { get; set; }
        public string Title { get; set; } = "New Chat";
        public List<Message> Messages { get; set; } = [];
    }

    public class Message:BaseEntity,ITenantOwned
    {
        public Guid TenantId { get; set; }
        public Guid ConversationId { get; set; }
        public Conversation Conversation { get; set; } = null!;
        public MessageRole Role { get; set; }
        public string Content { get; set; } = string.Empty;
        public string? Model { get; set; }
        public int? PromptTokens { get; set; }
        public int? CompletionTokens { get; set; }
        public int? LatencyMs { get; set; }
        public List<Citation> Citations { get; set; } = [];

    }

    public class Citation:BaseEntity,ITenantOwned
    {
        public Guid TenantId { get; set; }
        public Guid MessageId { get; set; }
        public Message Message { get; set; } = null!;
        public Guid? ChunkId { get; set; }
        public string DocumentName { get; set; } = string.Empty;
        public int? PageNumber { get; set; }
        public string Snippet { get; set; } = string.Empty;
        public double Score { get; set; }   

    }

    public class Feedback:BaseEntity,ITenantOwned
    {
        public Guid TenantId { get; set; }
        public Guid MessageId { get; set; }
        public Message Message { get; set; } = null!;
        public Guid UserId { get; set; }
        public bool IsHelpful { get; set; }
        public string? Comment { get; set; }    
    }
}
