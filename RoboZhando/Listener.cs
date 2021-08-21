using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.VoiceNext;
using RoboZhando.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RoboZhando
{
    class Listener : IDisposable
    {
        public DiscordChannel TextChannel { get; }
        public Synthesizer Synthesizer { get; }

        #region State
        public DiscordChannel VoiceChannel { get; private set;  }
        public VoiceNextConnection VoiceConnection { get; private set; }
        private TTSQueue queue;
        #endregion

        public Listener(DiscordChannel textChannel, Synthesizer synth)
        {

            // Ensure the datatypes
            if (textChannel.Type != ChannelType.Text)
                throw new ArgumentException("textChannel", "has to be of type Text");

            TextChannel = textChannel;
            Synthesizer = synth;
        }

        /// <summary>Connects to the voice channel</summary>
        public async Task ConnectAsync(DiscordChannel voiceChannel)
        {
            if (voiceChannel.Type != ChannelType.Voice)
                throw new ArgumentException("voiceChannel", "has to be of type Voice");

            VoiceConnection = await voiceChannel.ConnectAsync();
            
            if (queue != null) queue.Dispose();
            queue = new TTSQueue(Synthesizer, VoiceConnection);

        }

        /// <summary>Queues a message</summary>
        public async Task<bool> QueueAsync(DiscordMessage message, CancellationToken cancelToken = default)
        {
            // Cannot possible queue if there is no queue
            if (queue == null)
                return false;

            // Cannot queue if the text channel doesnt match
            if (message.Channel != TextChannel)
                return false;

            await queue.QueueAsync(message);
            return true;
        }

        /// <summary>Disconnects from the voice channel</summary>
        public bool Disconnect()
        {
            if (VoiceConnection == null)
                return false;

            VoiceConnection.Disconnect();
            VoiceChannel = null;
            queue?.Dispose();
            queue = null;
            return true;
        }

        public void Dispose()
        {
            queue?.Dispose();
        }
    }
}
