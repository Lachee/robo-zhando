using DSharpPlus.Entities;
using RoboZhando.Redis;
using RoboZhando.Redis.Serialize;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace RoboZhando.Entities
{
    public class ProfileSettings
    {

        /// <summary>Current redis connection</summary>
        [RedisIgnore]
        public IRedisClient Redis { get; internal set; }
        
        /// <summary>
        /// The guild the settings belong too. Maybe null.
        /// </summary>
        [RedisIgnore]
        public DiscordUser User
        {
            get => _user;
            set
            {
                _user = value;
                UserId = value.Id;
            }
        }
        private DiscordUser _user;

        /// <summary>
        /// The ID of the guild the settings belongs too.
        /// </summary>
        [RedisProperty]
        public ulong UserId { get; private set; }

        /// <summary>The Neural Voice for the Synthesizer</summary>
        /// <see cref="https://docs.microsoft.com/en-us/azure/cognitive-services/speech-service/language-support#neural-voices"/>
        [RedisProperty]
        public string Voice { get; set; } = "";

        public ProfileSettings() { }
        public ProfileSettings(DiscordUser user)
        {
            User = user;
        }

        /// <summary>
        /// Gets the guild settings
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        public static async Task<ProfileSettings> GetSettingsAsync(IRedisClient redis, DiscordUser user)
        {
            var settings = await redis.FetchObjectAsync<ProfileSettings>(Namespace.Combine(user.Id, "settings"));
            if (settings == null)
            {
                settings = new ProfileSettings(user) { Redis = redis };
                await settings.SaveAsync();
            }

            settings.Redis = redis;
            settings.User = user;
            return settings;
        }

        /// <summary>
        /// Saves the settings to the Redis
        /// </summary>
        /// <returns></returns>
        public Task SaveAsync()
        {
            return Redis.StoreObjectAsync(Namespace.Combine(UserId, "settings"), this);
        }
    }
}
