using DSharpPlus.Entities;
using DSharpPlus.VoiceNext;
using Microsoft.CognitiveServices.Speech;
using RoboZhando.Extensions;
using RoboZhando.Redis;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Threading.Channels;

namespace RoboZhando
{
    internal class TTSQueue : IDisposable
    {
        public Synthesizer Synthesizer { get; }
        public VoiceNextConnection VoiceConnection { get; }

        private CancellationTokenSource _cancellationTokenSource;
        private readonly Channel<DiscordMessage> _queue;

        public TTSQueue(Synthesizer synth, VoiceNextConnection voiceConnection)
        {
            this.Synthesizer = synth;
            this.VoiceConnection = voiceConnection;
            this._queue = Channel.CreateUnbounded<DiscordMessage>();
            this._cancellationTokenSource = new CancellationTokenSource();
            _ = this.StartProcessing(this._cancellationTokenSource.Token);
        }

        public async Task QueueAsync(DiscordMessage message, CancellationToken cancellationToken = default)
        {
            await _queue.Writer.WriteAsync(message, cancellationToken);
        }

        /// <summary>Returns a task that processes the message queue</summary>
        private async Task StartProcessing(CancellationToken cancellationToken = default)
        {
            await foreach(var message in _queue.Reader.ReadAllAsync(cancellationToken))
            {
                await Synthesizer.SpeakAsync(message, VoiceConnection, cancellationToken);
            }
        }

        public void Dispose()
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
        }
    }
}
