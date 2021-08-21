using DSharpPlus.Entities;
using DSharpPlus.VoiceNext;
using Microsoft.CognitiveServices.Speech;
using RoboZhando.Extensions;
using RoboZhando.Redis;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RoboZhando
{
    internal class Synthesizer : IDisposable
    {
        public const string DEFAULT_VOICE = "en-AU-NatashaNeural";

        private const string SSML_TEMPLATE = "<speak version=\"1.0\" xmlns=\"https://www.w3.org/2001/10/synthesis\" xml:lang=\"en-US\">{speak}</speak>";
        private const string SSML_SPEAK_TEMPLATE = "<voice name=\"{voice}\">{message}</voice>";

        public IRedisClient Redis { get; set; }
        public Logging.Logger Logger { get; }

        private SpeechSynthesizer Synth { get; }

        /// <summary>Includes the Author Says</summary>
        public string AnouncerVoice { get; set; } = DEFAULT_VOICE;

        /// <summary>Saves the content as a temporary wav file before transmitting</summary>
        public bool UseFileIntermStrategy { get; set; } = false;
        /// <summary>The audio format to get from the synth. Formats that are not RAW PCM require the use of <see cref="UseFileIntermStrategy"/></summary>
        public SpeechSynthesisOutputFormat AudioFormat { get; init; } = SpeechSynthesisOutputFormat.Raw48Khz16BitMonoPcm;

        public Synthesizer(string azureKey, string azureRegion, Logging.Logger logger = null)
        {
            var config = SpeechConfig.FromSubscription(azureKey, azureRegion);
            config.SetSpeechSynthesisOutputFormat(AudioFormat);

            Synth = new SpeechSynthesizer(config, null);
            Logger = logger ?? new Logging.Logger("SYNTH");
        }

        /// <summary>Checks if the given voice name exists and returns the appropriate full length one.</summary>
        public async Task<string> GetVoiceNameAsync(string voiceName)
        {
            voiceName = voiceName.Trim();
            var voices = await Synth.GetVoicesAsync();
            return voices.Voices.Where(voice =>
                voice.Name.Equals(voiceName, StringComparison.InvariantCultureIgnoreCase) ||
                voice.ShortName.Equals(voiceName, StringComparison.InvariantCultureIgnoreCase)
            ).Select(voice => voice.ShortName).FirstOrDefault();
        }

        /// <summary>Gets the prefered language for that given user</summary>
        /// <seealso cref="https://docs.microsoft.com/en-us/azure/cognitive-services/speech-service/language-support#neural-voices"/>
        protected async Task<string> GetPreferedVoice(DiscordUser user) {
            if (Redis != null) {
                var settings = await user.GetSettingsAsync(Redis);
                if (!string.IsNullOrWhiteSpace(settings.Voice)) {
                    return settings.Voice;
                }
            }
            return DEFAULT_VOICE;
        }

        /// <summary>Creates a speakable message from the given discord message</summary>
        protected async Task<string> CreateSSMLMessage(DiscordMessage message)
        {
            // Generate the message
            string voice = await GetPreferedVoice(message.Author);
            string speak = SSML_SPEAK_TEMPLATE.Replace("{voice}", voice).Replace("{message}", message.Content);

            // Add the "Author Says"
            if (!string.IsNullOrEmpty(AnouncerVoice))
            {
                // Get the author name
                string author = message.Author.Username;
                if (message.Channel?.Guild != null) {
                    var member = await message.Channel.Guild.GetMemberAsync(message.Author.Id);
                    if (member != null) 
                        author = member.DisplayName;
                }

                // Create the new speak and append it to the end
                var additionalSpeak = SSML_SPEAK_TEMPLATE.Replace("{voice}", AnouncerVoice).Replace("{message}", $"{author} says");
                speak = additionalSpeak + speak;
            }

            // Return the combined speak
            return SSML_TEMPLATE.Replace("{speak}", speak);
        }

        /// <summary>Speaks the message to the given voice next connection</summary>
        public async Task SpeakAsync(DiscordMessage message, VoiceNextConnection connection, CancellationToken cancellationToken = default)
        {
            // Get the result
            var result = await SythesizeAsync(message);
            if (result.Reason == ResultReason.Canceled)
            {
                var cancellation = SpeechSynthesisCancellationDetails.FromResult(result);
                if (cancellation.Reason == CancellationReason.Error)
                {
                    Logger.LogError("Failed to parse the given message.");
                    return;
                }
            }

            // Save the data
            // TODO: Make this a queue
            if (UseFileIntermStrategy)
            {
                var tempFileName = Path.GetTempFileName();
                try
                {
                    using var stream = AudioDataStream.FromResult(result);
                    await stream.SaveToWaveFileAsync(tempFileName);

                    // Transmit it away
                    var transmit = connection.GetTransmitSink();
                    var pcm = ConvertAudioToPcm(tempFileName);
                    await pcm.CopyToAsync(transmit, cancellationToken: cancellationToken);
                    await pcm.DisposeAsync();
                }
                finally
                {
                    if (File.Exists(tempFileName))
                        File.Delete(tempFileName);
                }
            }
            else
            {
                var transmit = connection.GetTransmitSink();
                await transmit.WriteAsync(result.AudioData, cancellationToken);
            }
        }

        /// <summary>Speaks the discord message directly to the speaker</summary>
        protected async Task<SpeechSynthesisResult> SythesizeAsync(DiscordMessage message)
        {
            var ssml = await CreateSSMLMessage(message);
            return await SythesizeAsync(ssml);
        }
        /// <summary>Speaks the text directly to the speaker.</summary>
        protected async Task<SpeechSynthesisResult> SythesizeAsync(string text)
        {
            SpeechSynthesisResult result;
            if (text.StartsWith("<speak"))
                 result = await Synth.SpeakSsmlAsync(text);
            else
                result = await Synth.SpeakTextAsync(text);

            if (result.Reason == ResultReason.SynthesizingAudioCompleted)
            {
                Logger.Log($"Speech synthesized to speaker for text [{text}]");
            }
            else if (result.Reason == ResultReason.Canceled)
            {
                var cancellation = SpeechSynthesisCancellationDetails.FromResult(result);
                Logger.LogError($"CANCELED: Reason={cancellation.Reason}");

                if (cancellation.Reason == CancellationReason.Error)
                {
                    Logger.LogError($"CANCELED: ErrorCode={cancellation.ErrorCode}");
                    Logger.LogError($"CANCELED: ErrorDetails=[{cancellation.ErrorDetails}]");
                    Logger.LogError($"CANCELED: Did you update the subscription info?");
                }
            }

            return result;
        }

        /// <summary>Uses FFMPEG to restream the audio</summary>
        private static Stream ConvertAudioToPcm(string filePath)
        {
            var ffmpeg = Process.Start(new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = $@"-i ""{filePath}"" -ac 2 -f s16le -ar 48000 pipe:1",
                RedirectStandardOutput = true,
                UseShellExecute = false
            });

            return ffmpeg.StandardOutput.BaseStream;
        }

        public void Dispose()
        {
            Synth?.Dispose();
        }
    }
}
