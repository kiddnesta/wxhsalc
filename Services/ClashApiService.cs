using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using ClashXW.Models;

namespace ClashXW.Services
{
    public class ClashApiService
    {
        private readonly HttpClient _httpClient;
        private readonly string? _apiBaseUrl;

        public ClashApiService(string? apiBaseUrl, string? apiSecret)
        {
            _apiBaseUrl = apiBaseUrl;
            _httpClient = new HttpClient();
            if (!string.IsNullOrEmpty(apiSecret))
            {
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiSecret);
            }
        }

        public Task<ClashConfig?> GetConfigsAsync()
        {
            if (string.IsNullOrEmpty(_apiBaseUrl)) return Task.FromResult<ClashConfig?>(null);
            return _httpClient.GetFromJsonAsync<ClashConfig>($"{_apiBaseUrl}/configs");
        }

        public Task<ProxiesResponse?> GetProxiesAsync()
        {
            if (string.IsNullOrEmpty(_apiBaseUrl)) return Task.FromResult<ProxiesResponse?>(null);
            return _httpClient.GetFromJsonAsync<ProxiesResponse>($"{_apiBaseUrl}/proxies");
        }

        public Task<ProxyProvidersResponse?> GetProxyProvidersAsync()
        {
            if (string.IsNullOrEmpty(_apiBaseUrl)) return Task.FromResult<ProxyProvidersResponse?>(null);
            return _httpClient.GetFromJsonAsync<ProxyProvidersResponse>($"{_apiBaseUrl}/providers/proxies");
        }

        public Task UpdateModeAsync(string newMode)
        {
            if (string.IsNullOrEmpty(_apiBaseUrl)) return Task.CompletedTask;
            var payload = new ModeUpdateRequest(newMode);
            return _httpClient.PatchAsJsonAsync($"{_apiBaseUrl}/configs", payload);
        }

        public async Task UpdateTunModeAsync(bool isEnabled)
        {
            if (string.IsNullOrEmpty(_apiBaseUrl)) return;
            var payload = new TunUpdateRequest(new TunEnableRequest(isEnabled));
            var response = await _httpClient.PatchAsJsonAsync($"{_apiBaseUrl}/configs", payload);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Status: {response.StatusCode}, Response: {errorContent}");
            }
        }

        public Task SelectProxyNodeAsync(string groupName, string nodeName)
        {
            if (string.IsNullOrEmpty(_apiBaseUrl)) return Task.CompletedTask;
            var payload = new ProxySelectionRequest(nodeName);
            return _httpClient.PutAsJsonAsync($"{_apiBaseUrl}/proxies/{Uri.EscapeDataString(groupName)}", payload);
        }

        public async Task ReloadConfigAsync(string configPath)
        {
            if (string.IsNullOrEmpty(_apiBaseUrl)) return;
            var payload = new ConfigReloadRequest(configPath);
            var response = await _httpClient.PutAsJsonAsync($"{_apiBaseUrl}/configs?force=true", payload);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Status: {response.StatusCode}, Response: {errorContent}");
            }
        }

        public async Task<IReadOnlyList<string>> UpdateProxyProvidersAsync()
        {
            if (string.IsNullOrEmpty(_apiBaseUrl)) return Array.Empty<string>();

            using var providersResponse = await _httpClient.GetAsync($"{_apiBaseUrl}/providers/proxies");
            providersResponse.EnsureSuccessStatusCode();

            using var document = JsonDocument.Parse(await providersResponse.Content.ReadAsStringAsync());
            if (!document.RootElement.TryGetProperty("providers", out var providers)
                || providers.ValueKind != JsonValueKind.Object)
            {
                return Array.Empty<string>();
            }

            var providerNames = providers.EnumerateObject()
                .Where(provider => provider.Value.TryGetProperty("vehicleType", out var vehicleType)
                    && vehicleType.ValueKind == JsonValueKind.String
                    && string.Equals(vehicleType.GetString(), "HTTP", StringComparison.OrdinalIgnoreCase))
                .Select(provider => provider.Name)
                .ToList();

            foreach (var providerName in providerNames)
            {
                using var response = await _httpClient.PutAsync(
                    $"{_apiBaseUrl}/providers/proxies/{Uri.EscapeDataString(providerName)}", null);

                if (!response.IsSuccessStatusCode)
                {
                    throw new HttpRequestException(
                        $"Failed to update provider '{providerName}' (HTTP {(int)response.StatusCode}).");
                }
            }

            return providerNames;
        }

        public async Task TestGroupLatencyAsync(string groupName)
        {
            if (string.IsNullOrEmpty(_apiBaseUrl)) return;

            var testUrl = Uri.EscapeDataString("https://www.gstatic.com/generate_204");
            var timeout = 5000;

            try
            {
                var response = await _httpClient.GetAsync($"{_apiBaseUrl}/group/{Uri.EscapeDataString(groupName)}/delay?url={testUrl}&timeout={timeout}");
                response.EnsureSuccessStatusCode();
            }
            catch
            {
                // Ignore errors - latency test failures are not critical
            }
        }

        public async Task TestProxyProviderLatencyAsync(string providerName)
        {
            if (string.IsNullOrEmpty(_apiBaseUrl)) return;

            using var response = await _httpClient.GetAsync(
                $"{_apiBaseUrl}/providers/proxies/{Uri.EscapeDataString(providerName)}/healthcheck");

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException(
                    $"Failed to test provider '{providerName}' (HTTP {(int)response.StatusCode}): {errorContent}");
            }
        }

        public async Task TestProxyLatencyAsync(string proxyName)
        {
            if (string.IsNullOrEmpty(_apiBaseUrl)) return;

            var testUrl = Uri.EscapeDataString("https://www.gstatic.com/generate_204");
            var timeout = 5000;

            try
            {
                var response = await _httpClient.GetAsync($"{_apiBaseUrl}/proxies/{Uri.EscapeDataString(proxyName)}/delay?url={testUrl}&timeout={timeout}");
                response.EnsureSuccessStatusCode();
            }
            catch
            {
                // Ignore errors - latency test failures are not critical
            }
        }
    }
}
