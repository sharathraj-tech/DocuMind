using DocuMind.Application.Abstractions;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Channels;

namespace DocuMind.Infrastructure.Ingestion
{
    public sealed class ChannelIngestionQueue : IIngestionQueue
    {
        private readonly Channel<IngestionJob> _channel = Channel.CreateUnbounded<IngestionJob>(
            new UnboundedChannelOptions { SingleReader = true });

        public ChannelReader<IngestionJob> Reader => _channel.Reader;

        public ValueTask EnqueueAsync(IngestionJob job, CancellationToken ct = default)
            => _channel.Writer.WriteAsync(job, ct);
    }
}
