using RoboZhando.Entities;
using RoboZhando.Redis;
using RoboZhando.Redis.Serialize;
using DSharpPlus.Entities;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace RoboZhando.Extensions
{
    public class GuildSettings
    {
        /// <summary>
        /// The default prefix for a guild
        /// </summary>
        public static string DefaultPrefix { get; set; } = "?";

        //Cache of all the prefixes
        private static Dictionary<ulong, string> _prefixCache = new Dictionary<ulong, string>();

        /// <summary>Current redis connection</summary>
        [RedisIgnore]
        public IRedisClient Redis { get; internal set; }

        /// <summary>
        /// The prefix of the guild
        /// </summary>
        [RedisProperty]
        public string Prefix
        {
            get => _prefix;
            set
            {
                _prefix = value;
                _prefixCache[GuildId] = value;
            }
        }
        private string _prefix;

        /// <summary>
        /// The guild the settings belong too. Maybe null.
        /// </summary>
        [RedisIgnore]
        public DiscordGuild Guild
        {
            get => _guild;
            set
            {
                _guild = value;
                GuildId = value.Id;
            }
        }
        private DiscordGuild _guild;
        
        /// <summary>
        /// The ID of the guild the settings belongs too.
        /// </summary>
        [RedisProperty]
        public ulong GuildId { get; private set; }


        public GuildSettings() { }
        public GuildSettings(DiscordGuild guild, string prefix)
        {
            Guild = guild;
            Prefix = prefix;
        }


        /// <summary>
        /// Gets the prefix for a guild
        /// </summary>
        /// <param name="guild"></param>
        /// <returns></returns>
        public static async Task<string> GetPrefixAsync(IRedisClient redis, DiscordGuild guild)
        {
            if (_prefixCache.TryGetValue(guild.Id, out var prefix))
                return prefix;

            var settings = await GetSettingsAsync(redis, guild);
            return settings.Prefix;
        }

        /// <summary>
        /// Gets the guild settings
        /// </summary>
        /// <param name="guild"></param>
        /// <returns></returns>
        public static async Task<GuildSettings> GetSettingsAsync(IRedisClient redis, DiscordGuild guild)
        {
            var settings = await redis.FetchObjectAsync<GuildSettings>(Namespace.Combine(guild, "settings"));
            if (settings == null)
            {
                settings = new GuildSettings(guild, DefaultPrefix) { Redis = redis };
                await settings.SaveAsync();
            }

            settings.Redis = redis;
            settings.Guild = guild;
            return settings;
        }
        
        /// <summary>
        /// Saves the settings to the Redis
        /// </summary>
        /// <returns></returns>
        public Task SaveAsync()
        {
            return Redis.StoreObjectAsync(Namespace.Combine(GuildId, "settings"), this);
        }
    }
}
