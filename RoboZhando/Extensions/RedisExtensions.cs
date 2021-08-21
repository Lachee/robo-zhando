using DSharpPlus.Entities;
using RoboZhando.Redis;
using System;
using System.Collections.Generic;
using System.Text;

namespace RoboZhando.Extensions
{
    public static class RedisExtensions
    {
        public static Namespace ToNamespace(this DiscordGuild guild) => new Namespace(guild.Id);
    }
}
