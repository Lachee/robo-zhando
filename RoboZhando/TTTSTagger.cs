using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace RoboZhando
{
    class TTTSTagger
    {
        public delegate string TagDelegate(string tag, string value);
        private Dictionary<string, TagDelegate> tags;
        private Dictionary<string, string> aliases;

        public TTTSTagger()
        {
            tags = new Dictionary<string, TagDelegate>();
            aliases = new Dictionary<string, string>();

            AddAliases("audio", "a", "sound", "s", "clip", "mp3");
            AddTag("audio", (tag, value) =>
            {
                string url = value.Replace("https://", "").Replace("http://", "");
                return $"<audio src=\"://{url}\">failed to load audio</audio>";
            });

            AddAliases("break", "b", "");
            AddTag("break", (tag, value) => "<break time=\"" + (string.IsNullOrWhiteSpace(value) ? "200ms" : value) + "\" />");
        }

        public void AddTag(string tag, TagDelegate tagDelegate)
        {
            tags[tag] = tagDelegate;
        }
        public void AddAlias(string tag, string alias)
        {
            aliases[alias] = tag;
        }
        public void AddAliases(string tag, params string[] aliases)
        {
            foreach (var alias in aliases)
                AddAlias(tag, alias);
        }

        public TagDelegate GetTag(string tag)
        {
            tag = tag.ToLower().Trim();

            // This can cause cyclic aliases lol, making infinite loop
            if (aliases.TryGetValue(tag, out var alias))
                return GetTag(alias);

            // Return the delegate
            if (tags.TryGetValue(tag, out var del))
                return del;

            return null;
        }


        public string Tag(string tag, string value)
        {
            var tagDelegate = GetTag(tag);
            if (tagDelegate == null) return null;
            return tagDelegate.Invoke(tag, value);
        }
        public string Tag(string text)
        {
            return Regex.Replace(text, "{([a-z]*):([^}]*)}", (Match m) =>
            {
                string tag = m.Groups[1].Value;
                string value = m.Groups[2].Value;
                string result = Tag(tag, value);
                return result == null ? m.Groups[0].Value : result;
            });
        }
    }
}
