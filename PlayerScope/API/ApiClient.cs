using FFXIVClientStructs.FFXIV.Common.Lua;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RestSharp;
using PlayerScope.API.Models;
using PlayerScope.API.Query;
using PlayerScope.GUI;
using PlayerScope.Handlers;
using PlayerScope.Properties;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace PlayerScope.API
{
    public class ApiClient
    {
        public static IRestClient _restClient = new RestClient();
        //private const string BaseUrl = "https://localhost:5001/v1/";
        public Configuration Config = PlayerScopePlugin.Instance.Configuration;
        internal static ApiClient Instance { get; private set; } = null!;
        public ApiClient()
        {
            if (Uri.IsWellFormedUriString(Config.BaseUrl, UriKind.Absolute))
                _restClient = new RestClient(Config.BaseUrl);
            Instance = this;
        }

        //---Server---//
        public string _ServerStatus = string.Empty;
        public bool IsCheckingServerStatus = false;
        public long _LastPingValue = -1;

        public async Task<bool> CheckServerStatus()
        {
            try
            {
                IsCheckingServerStatus = true;

                var request = new RestRequest($"server").AddHeader("api-key", Token).AddHeader("V", Util.clientVer).AddHeader("L", Config.Language);
                var response = await _restClient.ExecuteGetAsync(request).ConfigureAwait(false);
                long pingValue = -1;
                
                using (Ping pp = new Ping())
                {
                    Uri uri = new Uri($"{Config.BaseUrl}");
                    PingReply reply = pp.Send(uri.Host, 1000);

                    pingValue = reply.RoundtripTime;
                }

                if (response.IsSuccessful)
                {
                    _ServerStatus = "ONLINE"; _LastPingValue = pingValue;
                    IsCheckingServerStatus = false;
                    return true;
                }
                else if (!string.IsNullOrWhiteSpace(response.Content))
                    _ServerStatus = $"{Loc.ApiError} {response.Content}";
                else if (response.StatusCode == 0)
                    _ServerStatus = $"{Loc.ApiError} {Loc.ApiServiceUnavailable}";
                else
                    _ServerStatus = $"{Loc.ApiError} {response.StatusCode.ToString()}";

                IsCheckingServerStatus = false;
                return false;
            }
            catch (Exception ex)
            {
                _ServerStatus = ex.Message; 
                _LastPingValue = -1;
                IsCheckingServerStatus = false; return false;
            }
        }
        public (ServerStatsDto ServerStats, string Message) _LastServerStats = new();
        public async Task<(ServerStatsDto ServerStats, string Message)> CheckServerStats()
        {
            string Message = string.Empty;
            try
            {
                var request = new RestRequest($"server/stats").AddHeader("api-key", Token).AddHeader("V", Util.clientVer).AddHeader("L", Config.Language);
                var response = await _restClient.ExecuteGetAsync(request).ConfigureAwait(false);

                if (response.IsSuccessful)
                {
                    var _JsonResponse = JsonConvert.DeserializeObject<ServerStatsDto>(response.Content!);
                    if (_JsonResponse != null)
                    {
                        Message = Loc.ApiStatsRefreshed;
                        _LastServerStats = (_JsonResponse, Message);
                        return (_JsonResponse, Message);
                    }
                }
                else if (!string.IsNullOrWhiteSpace(response.Content))
                    Message = $"{Loc.ApiError} {response.Content}";
                else if (response.StatusCode == 0)
                    Message = $"{Loc.ApiError} {Loc.ApiServiceUnavailable}";
                else
                    Message = $"{Loc.ApiError} {response.StatusCode.ToString()}";

                return (null, Message);
            }
            catch (Exception ex)
            {
                Message = $"{Loc.ApiError} {ex.Message}";
                return (null, Message);
            }
        }
        //---Players---//
        //public ConcurrentDictionary<long, PlayerDto> _LastPlayerSearchResults = new ConcurrentDictionary<long, PlayerDto>();
        //public ConcurrentDictionary<long, PlayerDetailed> _LastPlayerByIdSearchResults = new ConcurrentDictionary<long, PlayerDetailed>();
        public string Token => $"{Config.Key}-{Config.AccountId}";
        public async Task<(PaginationBase<T>? Page, string Message)> GetPlayers<T>(PlayerQueryObject query)
        {
            var _GetPlayerSearchResult = new Dictionary<long, PlayerDto>();
            string Message = string.Empty;
            try
            {
                var request = new RestRequest($"players").AddHeader("api-key", Token).AddHeader("V", Util.clientVer).AddHeader("L", Config.Language);
                if (!string.IsNullOrWhiteSpace(query.Name))
                    request.AddQueryParameter("Name", query.Name, true);
                if (!string.IsNullOrWhiteSpace(query.LocalContentId.ToString()))
                    request.AddQueryParameter("LocalContentId", query.LocalContentId.ToString(), true);
                if (!string.IsNullOrWhiteSpace(query.Cursor.ToString()))
                    request.AddQueryParameter("Cursor", query.Cursor.ToString(), true);
                if (!string.IsNullOrWhiteSpace(query.IsFetching.ToString()))
                    request.AddQueryParameter("IsFetching", query.IsFetching.ToString(), true);
                if (!string.IsNullOrWhiteSpace(query.F_WorldId.ToString()))
                    request.AddQueryParameter("F_WorldId", query.F_WorldId.ToString(), true);
                if (!string.IsNullOrWhiteSpace(query.F_MatchAnyPartOfName.ToString()))
                    request.AddQueryParameter("F_MatchAnyPartOfName", query.F_MatchAnyPartOfName.ToString(), true);

                var response = await _restClient.ExecuteGetAsync(request).ConfigureAwait(false);

                if (response.IsSuccessful)
                {
                    var jsonResponse = JsonConvert.DeserializeObject<PaginationBase<T>>(response.Content!);
                    if (jsonResponse != null)
                    {
                        Message = $"- {Loc.ApiTotalFound} {jsonResponse.NextCount}";
                        return (jsonResponse, Message);
                    }
                }
                else if (!string.IsNullOrWhiteSpace(response.Content))
                    Message = $"{Loc.ApiError} {response.Content}";
                else if (response.StatusCode == 0)
                    Message = $"{Loc.ApiError} {Loc.ApiServiceUnavailable}";
                else
                    Message = $"{Loc.ApiError} {response.StatusCode.ToString()}";

                return (null, Message);
            }
            catch (Exception ex)
            {
                Message = $"{Loc.ApiError} {ex.Message}";
                return (null, Message);
            }
        }

        public async Task<(PlayerDetailed Player, string Message)> GetPlayerById(long id)
        {
            string Message = string.Empty;
            try
            {
                var request = new RestRequest($"players/{id}").AddHeader("api-key", Token).AddHeader("V", Util.clientVer).AddHeader("L", Config.Language);
                var response = await _restClient.ExecuteGetAsync(request).ConfigureAwait(false);

                if (response.IsSuccessful)
                {
                    var _JsonResponse = JsonConvert.DeserializeObject<PlayerDetailed>(response.Content!);
                    Message = "Player found.";
                    return (_JsonResponse, Message);
                }
                else if (!string.IsNullOrWhiteSpace(response.Content))
                    Message = $"{Loc.ApiError} {response.Content}";
                else if (response.StatusCode == 0)
                    Message = $"{Loc.ApiError} {Loc.ApiServiceUnavailable}";
                else
                    Message = $"{Loc.ApiError} {response.StatusCode.ToString()}";

                return (null, Message);
            }
            catch (Exception ex)
            {
                Message = $"{Loc.ApiError} {ex.Message}";
                return (null, Message);
            }
        }

        public async Task<bool> PostPlayers(List<PostPlayerRequest> players)
        {
            try
            {
                var request = new RestRequest($"players").AddHeader("api-key", Token).AddHeader("V", Util.clientVer).AddHeader("L", Config.Language);
                request.AddJsonBody(players);
                var response = await _restClient.ExecutePostAsync(request).ConfigureAwait(false);
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                return false;
            }
        }
        //---Retainers---//
        public async Task<(PaginationBase<T>? Page, string Message)> GetRetainers<T>(RetainerQueryObject query)
        {
            string Message = string.Empty;
            try
            {
                var request = new RestRequest($"retainers").AddHeader("api-key", Token).AddHeader("V", Util.clientVer).AddHeader("L", Config.Language); ;
                if (!string.IsNullOrWhiteSpace(query.Name))
                    request.AddQueryParameter("Name", query.Name, true);
                if (!string.IsNullOrWhiteSpace(query.Cursor.ToString()))
                    request.AddQueryParameter("Cursor", query.Cursor.ToString(), true);
                if (!string.IsNullOrWhiteSpace(query.IsFetching.ToString()))
                    request.AddQueryParameter("IsFetching", query.IsFetching.ToString(), true);
                if (!string.IsNullOrWhiteSpace(query.F_WorldId.ToString()))
                    request.AddQueryParameter("F_WorldId", query.F_WorldId.ToString(), true);
                if (!string.IsNullOrWhiteSpace(query.F_MatchAnyPartOfName.ToString()))
                    request.AddQueryParameter("F_MatchAnyPartOfName", query.F_MatchAnyPartOfName.ToString(), true);

                var response = await _restClient.ExecuteGetAsync(request).ConfigureAwait(false);
                if (response.IsSuccessful)
                {
                    var jsonResponse = JsonConvert.DeserializeObject<PaginationBase<T>>(response.Content!);
                    if (jsonResponse != null)
                    {
                        Message = $"- {Loc.ApiTotalFound} {jsonResponse.NextCount}";
                        return (jsonResponse, Message);
                    }
                }
                else if (!string.IsNullOrWhiteSpace(response.Content))
                    Message = $"{Loc.ApiError} {response.Content}";
                else if (response.StatusCode == 0)
                    Message = $"{Loc.ApiError} {Loc.ApiServiceUnavailable}";
                else
                    Message = $"{Loc.ApiError} {response.StatusCode.ToString()}";

                return (null, Message);
            }
            catch (Exception ex)
            {
                Message = $"{Loc.ApiError} {ex.Message}";
                return (null, Message);
            }
        }
        public async Task<bool> PostRetainers(List<PostRetainerRequest> retainers)
        {
            try
            {
                var request = new RestRequest($"retainers").AddHeader("api-key", Token).AddHeader("V", Util.clientVer).AddHeader("L", Config.Language);
                request.AddJsonBody(retainers);
                var response = await _restClient.ExecutePostAsync(request).ConfigureAwait(false);
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                return false;
            }
        }
        //---Users---//
        private static readonly HttpClient _httpClient = new HttpClient();
        public bool IsLoggingIn = false;
        public string authUrl = string.Empty;
        public async Task<(User? User, string Message)> DiscordAuth(UserRegister register)
        {
            IsLoggingIn = true;
            string Message = string.Empty;
            try
            {
                string output = JsonConvert.SerializeObject(register);
                byte[] bytes = Encoding.UTF8.GetBytes(output);
                string data = System.Convert.ToBase64String(bytes);
                authUrl = Config.BaseUrl.Replace("v1/", "Auth/DiscordAuth?") + data;

                Process.Start(new ProcessStartInfo
                {
                    FileName = authUrl,
                    UseShellExecute = true,
                });

                var response = await _httpClient.GetAsync($"{Config.BaseUrl.Replace("v1/", "")}waitforlogin?data={data}", HttpCompletionOption.ResponseHeadersRead);
                using (var stream = await response.Content.ReadAsStreamAsync())
                using (var reader = new StreamReader(stream))
                {
                    while (IsLoggingIn)
                    {
                        var line = await reader.ReadLineAsync();
                        if (line == null) break;
                        if (line.StartsWith("data:"))
                        {
                            var message = line.Substring("data:".Length).Trim();
                            if (message.Contains("Login successful"))
                            {
                                IsLoggingIn = false;
                                SettingsWindow.Instance.RefreshUserProfileInfo();
                                break;
                            }
                        }
                    }
                }

                return (null, Message);
            }
            catch (Exception ex)
            {
                Message = $"{Loc.ApiError} {ex.Message}";
                return (null, Message);
            }
        }
        public async Task<(User? User, string Message)> UserLogin(UserRegister loginUser)
        {
            string Message = string.Empty;
            try
            {
                var request = new RestRequest($"users/login");
                request.AddJsonBody(loginUser);

                var response = await _restClient.ExecutePostAsync(request).ConfigureAwait(true);

                if (response.IsSuccessful)
                {
                    var _JsonResponse = JsonConvert.DeserializeObject<User>(response.Content!);
                    if (_JsonResponse != null)
                    {
                        Message = "Logged in successfully.";
                        return (_JsonResponse, Message);
                    }
                }
                else if (!string.IsNullOrWhiteSpace(response.Content))
                    Message = $"{Loc.ApiError} {response.Content}";
                else if (response.StatusCode == 0)
                    Message = $"{Loc.ApiError} {Loc.ApiServiceUnavailable}";
                else
                    Message = $"{Loc.ApiError} {response.StatusCode.ToString()}";

                return (null, Message);
            }
            catch (Exception ex)
            {
                Message = $"{Loc.ApiError} {ex.Message}";
                return (null, Message);
            }
        }
        public async Task<string> UserUpdate(UserUpdateDto config)
        {
            string Message = string.Empty;
            try
            {
                var request = new RestRequest($"users/update").AddHeader("api-key", Token).AddHeader("V", Util.clientVer).AddHeader("L", Config.Language);
                request.AddJsonBody(config);
                var response = await _restClient.ExecutePostAsync(request).ConfigureAwait(true);

                if (response.IsSuccessful && !string.IsNullOrWhiteSpace(response.Content))
                {
                    return response.Content;
                }
                else if (!string.IsNullOrWhiteSpace(response.Content))
                    Message = $"{Loc.ApiError} {response.Content}";
                else if (response.StatusCode == 0)
                    Message = $"{Loc.ApiError} {Loc.ApiServiceUnavailable}";
                else
                    Message = $"{Loc.ApiError} {response.StatusCode.ToString()}";

                return Message;
            }
            catch (Exception ex)
            {
                Message = $"{Loc.ApiError} {ex.Message}";
                return Message;
            }
        }
        public async Task<(User? User, string Message)> UserRefreshMyInfo()
        {
            string Message = string.Empty;
            try
            {
                var request = new RestRequest($"users/me").AddHeader("api-key", Token).AddHeader("V", Util.clientVer).AddHeader("L", Config.Language);
                var response = await _restClient.ExecuteGetAsync(request).ConfigureAwait(true);

                if (response.IsSuccessful)
                {
                    var _JsonResponse = JsonConvert.DeserializeObject<User>(response.Content!);
                    if (_JsonResponse != null)
                    {
                        Message = Loc.ApiProfileRefreshed;
                        return (_JsonResponse,Message);
                    }
                }
                else if (!string.IsNullOrWhiteSpace(response.Content))
                    Message = $"{Loc.ApiError} {response.Content}";
                else if (response.StatusCode == 0)
                    Message = $"{Loc.ApiError} {Loc.ApiServiceUnavailable}";
                else
                    Message = $"{Loc.ApiError} {response.StatusCode.ToString()}"; 

                return (null, Message);
            }
            catch (Exception ex)
            {
                Message = $"{Loc.ApiError} {ex.Message}";
                return (null, Message);
            }
        }
    }
}
