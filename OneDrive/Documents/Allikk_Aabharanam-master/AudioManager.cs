using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NAudio.Wave;

namespace DesktopKeychainApp
{
    /// <summary>
    /// Small isolated audio manager for Allikk Aabharanam sounds.
    /// Only plays when a valid accessory-attached desktop item is actively tracked.
    /// </summary>
    public sealed class AudioManager : IDisposable
    {
        private readonly object _syncRoot = new object();
        private readonly Dictionary<string, DateTime> _lastPlayTimes = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<AudioPlayback> _activePlayers = new HashSet<AudioPlayback>();
        private readonly TimeSpan _duplicateWindow = TimeSpan.FromMilliseconds(250);
        private static readonly string AudioLogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "audio.log");

        public bool PlayOpen() => PlaySound("Faaah.mp3");
        public bool PlayRename() => PlaySound("Scary Music - Meme Sound Effect.mp3");
        public bool PlayDelete() => PlaySound("oh hell na.mp3");
        public bool PlayCopy() => PlaySound("Helicopter.mp3");
        public bool PlayPaste() => PlaySound("67 - Sound Effect.mp3");
        public bool PlayMove() => PlaySound("Aww   Sound Effect.mp3");
        public bool PlayDrag() => PlaySound("OMG (meme) - Sound Effect [L6CMypl1q8I].mp3");
        public bool PlayDrop() => PlaySound("Cat Laughing Meme Sound Effect..mp3");

        public string ResolveAudioPath(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return null;

            string[] fileNameCandidates = string.Equals(
                fileName,
                "Cat Laughing Meme Sound Effect..mp3",
                StringComparison.OrdinalIgnoreCase)
                ? new[] { fileName, "Cat Laughing Meme Sound Effect.mp3" }
                : new[] { fileName };

            var root = AppDomain.CurrentDomain.BaseDirectory;
            var candidateDirectories = new[]
            {
                root,
                Path.Combine(root, "Audios"),
                Path.Combine(root, "Assets"),
                Path.Combine(root, "Assets", "Audio"),
                Path.Combine(root, "assets"),
                Path.Combine(root, "assets", "Audio"),
                Path.Combine(root, "..", "..", ".."),
            };

            foreach (var candidateName in fileNameCandidates)
            {
                foreach (var directory in candidateDirectories)
                {
                    var directPath = Path.Combine(directory, candidateName);
                    if (File.Exists(directPath))
                    {
                        if (!string.Equals(candidateName, fileName, StringComparison.OrdinalIgnoreCase))
                            Debug.WriteLine($"Audio filename fallback: requested='{fileName}' resolved='{directPath}'");
                        return directPath;
                    }
                }
            }

            foreach (var candidateName in fileNameCandidates)
            {
                foreach (var directory in candidateDirectories)
                {
                    if (!Directory.Exists(directory))
                        continue;

                    var match = Directory
                        .EnumerateFiles(directory, "*.mp3", SearchOption.AllDirectories)
                        .FirstOrDefault(file => string.Equals(Path.GetFileName(file), candidateName, StringComparison.OrdinalIgnoreCase));

                    if (match != null)
                    {
                        if (!string.Equals(candidateName, fileName, StringComparison.OrdinalIgnoreCase))
                            Debug.WriteLine($"Audio filename fallback: requested='{fileName}' resolved='{match}'");
                        return match;
                    }
                }
            }

            return null;
        }

        private bool PlaySound(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return false;

            AudioPlayback playback = null;
            try
            {
                var targetPath = ResolveAudioPath(fileName);
                if (string.IsNullOrEmpty(targetPath) || !File.Exists(targetPath))
                {
                    LogAudio($"MISSING fileName='{fileName}' path='{targetPath ?? "<not found>"}' baseDirectory='{AppDomain.CurrentDomain.BaseDirectory}'");
                    return false;
                }

                lock (_syncRoot)
                {
                    var now = DateTime.UtcNow;
                    if (_lastPlayTimes.TryGetValue(fileName, out var lastPlayed) && now - lastPlayed < _duplicateWindow)
                    {
                        return false;
                    }

                    _lastPlayTimes[fileName] = now;
                }

                playback = new AudioPlayback(fileName, targetPath);
                playback.Output.PlaybackStopped += (_, __) => ReleasePlayback(playback);

                lock (_syncRoot)
                {
                    _activePlayers.Add(playback);
                }

                playback.Output.Init(playback.Reader);
                playback.Output.Play();
                LogAudio($"STARTED fileName='{fileName}' path='{targetPath}'");

                return true;
            }
            catch (Exception ex)
            {
                if (playback != null)
                {
                    ReleasePlayback(playback);
                }
                LogAudio($"FAILED fileName='{fileName}' path='{ResolveAudioPath(fileName) ?? "<not found>"}' error='{ex}'");
                return false;
            }
        }

        private static void LogAudio(string message)
        {
            string line = $"{DateTime.Now:O} {message}{Environment.NewLine}";
            Debug.Write(line);
            try
            {
                File.AppendAllText(AudioLogPath, line);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Audio log write failed: {ex}");
            }
        }

        private void ReleasePlayback(AudioPlayback playback)
        {
            lock (_syncRoot)
            {
                if (!_activePlayers.Remove(playback))
                    return;
            }

            playback.Output.Dispose();
            playback.Reader.Dispose();
        }

        public void Dispose()
        {
            lock (_syncRoot)
            {
                _lastPlayTimes.Clear();
                foreach (var playback in _activePlayers.ToArray())
                {
                    try
                    {
                        playback.Output.Stop();
                        playback.Output.Dispose();
                        playback.Reader.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Audio dispose failed: {ex}");
                    }
                }
                _activePlayers.Clear();
            }
        }

        private sealed class AudioPlayback
        {
            public AudioPlayback(string fileName, string path)
            {
                FileName = fileName;
                Path = path;
                Reader = new AudioFileReader(path);
                Output = new WaveOutEvent();
            }

            public string FileName { get; }
            public string Path { get; }
            public AudioFileReader Reader { get; }
            public WaveOutEvent Output { get; }
        }
    }
}
