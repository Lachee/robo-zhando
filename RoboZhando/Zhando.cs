using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Exceptions;
using DSharpPlus.Entities;
using DSharpPlus.VoiceNext;
using Microsoft.CognitiveServices.Speech;
using Microsoft.Extensions.DependencyInjection;
using RoboZhando.CommandNext;
using RoboZhando.Extensions;
using RoboZhando.Logging;
using RoboZhando.Modules;
using RoboZhando.Modules.Configuration;
using RoboZhando.Redis;

namespace RoboZhando
{
    public class Zhando : IDisposable
    {
        public static Zhando Instance { get; private set; }

        public BotConfig Configuration { get; }

        public Logger Logger { get; }

        public IRedisClient Redis { get; }

        public DiscordClient Discord { get; }
        public CommandsNextExtension CommandsNext { get; }
        public VoiceNextExtension VoiceNext { get; }

        internal Synthesizer Synthesizer { get; }

        /// <summary>TTS queues, mapping channel id to the voice connection.</summary>
        private Dictionary<DiscordGuild, Listener> listeners = new Dictionary<DiscordGuild, Listener>();

        public Zhando(BotConfig config)
        {
            Instance = this;
            Configuration = config;
            Logger = new Logger("BOT");

            Logger.Log("Creating new bot");
            Discord = new DiscordClient(new DiscordConfiguration() { Token = config.Token });
            Discord.ClientErrored += async (client, error) => await LogException(error.Exception);

            Logger.Log("Creating Stack Exchange Client");
            Redis = new StackExchangeClient(config.Redis.Address, config.Redis.Database, Logger.CreateChild("REDIS"));
            Namespace.SetRoot(config.Redis.Prefix);
            GuildSettings.DefaultPrefix = config.Prefix;

            var deps = new ServiceCollection()
                    .AddSingleton(this)
                    .BuildServiceProvider(true);

            // Setup command 
            Logger.Log("Creating CommandNext");
            CommandsNext = Discord.UseCommandsNext(new CommandsNextConfiguration() { 
                PrefixResolver = ResolvePrefixAsync,
                Services = deps 
            });
            CommandsNext.RegisterConverter(new QueryConverter());
            CommandsNext.RegisterConverter(new CommandQueryArgumentConverter());
            CommandsNext.RegisterCommands<VoiceModule>();
            CommandsNext.RegisterCommands<ConfigurationModule>();
            CommandsNext.CommandErrored +=  async (client, error) => await HandleCommandErrorAsync(error);

            // Setup voice next
            Logger.Log("Creating VoiceNext & Synth");
            VoiceNext = Discord.UseVoiceNext(new VoiceNextConfiguration() { 
                // We need to manually set the audio-format here because the synth is mono,
                // and if we dont set it to 1 channel, it freaks out and doubles the speed
                AudioFormat = new AudioFormat(48000, 1, VoiceApplication.Voice) 
            });
            Synthesizer = new Synthesizer(config.AuzreKey, config.AzureRegion, Logger.CreateChild("SYNTH"))
            {
                Redis = Redis,
                AnouncerVoice = config.AnouncerVoice,
            };
        }

        internal async Task InitAsync()
        {
            // Listen to messages
            Logger.Log("Initializing");
            Discord.MessageCreated += OnMessageCreatedAsync;

            await Discord.ConnectAsync(activity: new DiscordActivity("Zhando", ActivityType.ListeningTo), status: UserStatus.Online);
        }

        internal Task DeinitAsync()
        {
            // Listen to messages
            Logger.Log("Denitializing");
            Discord.MessageCreated -= OnMessageCreatedAsync;
            return Task.CompletedTask;
        }

        private async Task OnMessageCreatedAsync(DiscordClient sender, DSharpPlus.EventArgs.MessageCreateEventArgs e)
        {
            // Skip if bot or DM
            if (e.Author == null || e.Author.IsBot || e.Guild == null) return;

            // Skip if we are a command
            if (await ResolvePrefixAsync(e.Message) >= 0)
                return;

            // Skip if not me temp
            if (e.Author.Id != 360601946194706453L &&
                e.Author.Id != 130973321683533824L) 
                    return;

            // Skip if we dont have a connection
            Listener listener;
            if (!listeners.TryGetValue(e.Guild, out listener))
                return;

            // Send the message to the listener if the user has a voice.
            var settings = await e.Message.Author.GetSettingsAsync(Redis);
            if (!string.IsNullOrEmpty(settings.Voice))
                await listener.QueueAsync(e.Message);
        }


        /// <summary>Starts listening to the text channel on the given voice channel.</summary>
        public async Task ListenAsync(DiscordChannel textChannel, DiscordChannel voiceChannel)
        {
            Listener listener;
            if (listeners.TryGetValue(voiceChannel.Guild, out listener)) {
                listener.Disconnect();
                listener.Dispose();
            }

            listeners[textChannel.Guild] = listener = new Listener(textChannel, Synthesizer);
            await listener.ConnectAsync(voiceChannel);
        }

        /// <summary>
        /// Resolves the prefix of the message and returns a index to trim from.
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        public async Task<int> ResolvePrefixAsync(DiscordMessage message)
        {
            try
            {
                // We do not listen to bots
                if (message.Author == null || message.Author.IsBot)
                    return -1;

                // Get the position of the prefix
                string prefix = await GuildSettings.GetPrefixAsync(Redis, message.Channel.Guild);
                var pos = message.GetStringPrefixLength(prefix);

                // Return the index of the prefix
                return pos;
            }
            catch (Exception e)
            {
                this.Logger.LogError(e);
                return -1;
            }
        }

        /// <summary>
        /// Handles Failed Executions
        /// </summary>
        /// <param name="e"></param>
        /// <returns></returns>
        private async Task HandleCommandErrorAsync(CommandErrorEventArgs e)
        {
            //Log the exception
            Logger.LogError(e.Exception);

            //Check if we have permission
            if (e.Exception is ChecksFailedException cfe)
            {
                var first = cfe.FailedChecks.FirstOrDefault();
                //Generic bad permissions
                await e.Context.RespondAsync($"You failed the check {first.GetType().Name} and cannot execute the function.");

                //Save the execution to the database
                //await (new CommandLog(e.Context, failure: $"Failed {first.GetType().Name} check.")).SaveAsync(DbContext);
                return;
            }

            //The bot itself is unable to do it.
            if (e.Exception is DSharpPlus.Exceptions.UnauthorizedException)
            {
                var trace = e.Exception.StackTrace.Split(" in ", 2)[0].Trim().Substring(3);
                await e.Context.RespondAsync($"I do not have permission to do that, sorry.\n`{trace}`");

                //Save the execution to the database
                //await (new CommandLog(e.Context, failure: $"Unauthorized")).SaveAsync(DbContext);
                return;
            }

            //We dont know the command, so just skip
            if (e.Exception is DSharpPlus.CommandsNext.Exceptions.CommandNotFoundException)
            {
                //Save the execution to the database
                //await (new CommandLog(e.Context, failure: $"Command Not Found")).SaveAsync(DbContext);
                return;
            }

            //If all else fails, then we will log it
            //await e.Context.RespondAsync(e.Exception, false);

            //Save the execution to the database
            //await (new CommandLog(e.Context, failure: $"Exception: {e.Exception.Message}")).SaveAsync(DbContext);
            return;
        }


        /// <summary>
        /// Logs an exception.
        /// </summary>
        /// <param name="exception"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public Task LogException(Exception exception, DiscordMessage context = null)
        {
            Logger.LogError(exception);
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            Discord?.Dispose();
            Synthesizer?.Dispose();

            // Clear listeners
            foreach(var kp in listeners)
            {
                kp.Value.Dispose();
            }
        }
    }
}