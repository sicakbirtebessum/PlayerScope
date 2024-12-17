using Dalamud.Interface.Textures.TextureWraps;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace PlayerScope.Handlers
{
    public class AvatarCacheManager : IDisposable
    {
        private readonly HttpClient _httpClient;
        public readonly ConcurrentDictionary<string, AvatarCacheEntry> _avatarCache;
        private readonly ConcurrentDictionary<string, Task> _ongoingDownloads;

        public AvatarCacheManager()
        {
            _httpClient = new HttpClient();
            _avatarCache = new ConcurrentDictionary<string, AvatarCacheEntry>();
            _ongoingDownloads = new ConcurrentDictionary<string, Task>();
        }

        public void ClearAvatarCache()
        {
            foreach (var entry in _avatarCache.Values)
            {
                entry.Texture?.Dispose();
            }
            _avatarCache.Clear();
        }

        public nint GetAvatarHandle(string avatarUrl)
        {
            if (_avatarCache != null && _avatarCache.TryGetValue(avatarUrl, out var cachedEntry))
            {
                if (DateTime.UtcNow < cachedEntry.Expiration)
                    return cachedEntry.TextureHandle;

                cachedEntry.Texture?.Dispose();
                _avatarCache.TryRemove(avatarUrl, out _);
            }

            if (!_ongoingDownloads.ContainsKey(avatarUrl))
            {
                _ongoingDownloads[avatarUrl] = Task.Run(async () =>
                {
                    try
                    {
                        var texture = await DownloadImageAsync(avatarUrl);

                        if (texture != null)
                        {
                            var expiration = DateTime.UtcNow.AddMinutes(60);

                            var newEntry = new AvatarCacheEntry
                            {
                                Texture = texture,
                                TextureHandle = texture.ImGuiHandle,
                                Expiration = expiration
                            };

                            if (_avatarCache != null)
                            {
                                _avatarCache[avatarUrl] = newEntry;
                            }
                        }
                    }
                    catch (Exception) { }
                    finally
                    {
                        _ongoingDownloads.TryRemove(avatarUrl, out _);
                    }
                });
            }

            return 0;
        }


        private async Task<IDalamudTextureWrap> DownloadImageAsync(string avatarUrl)
        {
            try
            {
                var imageData = await _httpClient.GetByteArrayAsync(avatarUrl);

                var imageMemory = new ReadOnlyMemory<byte>(imageData);

                var texture = await Plugin.TextureProvider.CreateFromImageAsync(imageMemory);

                return texture;
            }
            catch (HttpRequestException httpEx) { }
            catch (Exception ex) { }
            return null;
        }

        public void Dispose()
        {
            foreach (var entry in _avatarCache.Values)
            {
                entry.Texture?.Dispose();
            }

            _httpClient.Dispose();
        }

        public class AvatarCacheEntry
        {
            public IDalamudTextureWrap Texture { get; set; } = null!;
            public nint TextureHandle { get; set; }
            public DateTime Expiration { get; set; }
        }
    }
}
