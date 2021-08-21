using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using DSharpPlus.VoiceNext;
using Microsoft.Extensions.Logging;
using RoboZhando.Extensions;
using RoboZhando.Logging;
using RoboZhando.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RoboZhando.Modules
{
    [Group("voice"), Aliases("v")]
    [Description("Voice commands")]
    public class VoiceModule : BaseCommandModule
    {
        public Zhando Bot { get; }
        private IRedisClient Redis => Bot.Redis;

        public Logger Logger { get; }

        public VoiceModule(Zhando bot)
        {
            this.Bot = bot;
            this.Logger = new Logger("VOICE", bot.Logger);
        }

        [Command("join"), Description("Joins the current channel and reads the given text channel")]
        public async Task Join(CommandContext ctx) //, DiscordChannel textChannel)
        {
            // Check the text channel
            var textChannel = ctx.Channel;
            if (textChannel == null)
                throw new ArgumentNullException("channel", "channel cannot be null");

            // Get the voice channel
            var voiceChannel = ctx.Member.VoiceState?.Channel;
            if (voiceChannel == null)
                throw new Exception("Cannot join voice channel because you are not in one");

            // Join the voice channel
            var response    = await ctx.RespondAsync("Joining Channel...");
            await Bot.ListenAsync(textChannel, voiceChannel);
            await response.ModifyAsync("Listening...");
        }

        [Command("set")]
        [Description("Sets your desired voice.\n" +
    "See Microsoft's [list of voices](https://docs.microsoft.com/en-us/azure/cognitive-services/speech-service/language-support#neural-voices).")]
        public async Task SetVoice(CommandContext ctx, string voice)
        {
            var response = await ctx.RespondAsync($"Updating your voice... 👨🏻‍🎤");

            //Validate the voice
            string voiceName = null;
            if (!string.IsNullOrWhiteSpace(voice))
                voiceName = await Bot.Synthesizer.GetVoiceNameAsync(voice);

            // Update the settings
            var settings = await ctx.Member.GetSettingsAsync(Redis);
            settings.Voice = voiceName ?? "";
            await settings.SaveAsync();

            // Send what we actually have
            settings = await ctx.Member.GetSettingsAsync(Redis);
            await response.ModifyAsync($"Your voice is now `{settings.Voice}`.");
        }
    }
}
