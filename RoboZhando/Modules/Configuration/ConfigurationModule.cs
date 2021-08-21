using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using RoboZhando.Redis;
using RoboZhando.Extensions;
using RoboZhando.Logging;
using System.IO;
using DSharpPlus.Entities;
using RoboZhando.CommandNext;
using RoboZhando.Entities;
using System.Linq;

namespace RoboZhando.Modules.Configuration
{
    [Group("config")]
    public partial class ConfigurationModule : BaseCommandModule
    {
        private Zhando Bot { get; }
        private IRedisClient Redis => Bot.Redis;
        private Logger Logger { get; }

        public ConfigurationModule(Zhando bot)
        {
            this.Bot = bot;
            this.Logger = new Logger("CMD-CONFIG", bot.Logger);
        }

        [Command("prefix")]
        [Description("Sets the prefix of the bot for the guild.")]
        [RequireUserPermissions(DSharpPlus.Permissions.Administrator)]
        public async Task SetPrefix(CommandContext ctx, string prefix)
        {
            if (string.IsNullOrWhiteSpace(prefix))
                throw new ArgumentNullException("prefix", "Prefix cannot be null or empty.");

            //Fetch the settings, update its prefix then save again
            var settings = await GuildSettings.GetSettingsAsync(Redis, ctx.Guild);
            settings.Prefix = prefix;
            await settings.SaveAsync();

            //Respond that we did that.
            await ctx.Message.CreateReactionAsync(DiscordEmoji.FromUnicode("✔"));
            settings = await GuildSettings.GetSettingsAsync(Redis, ctx.Guild);
            await ctx.RespondAsync($"The prefix has been set. I will now only respond to `{settings.Prefix}`.");
        }
    }
}
