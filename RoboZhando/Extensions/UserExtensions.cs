using DSharpPlus.Entities;
using RoboZhando.Entities;
using RoboZhando.Redis;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace RoboZhando.Extensions
{
    public static class UserExtensions
    {
        /// <summary>
        /// Gets the settings of the guild. It is not cached.
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        public static Task<ProfileSettings> GetSettingsAsync(this DiscordUser user, IRedisClient redis) 
                => ProfileSettings.GetSettingsAsync(redis, user);
    }
}
