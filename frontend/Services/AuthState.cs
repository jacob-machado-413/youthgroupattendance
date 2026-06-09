using Microsoft.JSInterop;

namespace YouthGroupAttendance.Frontend.Services;

public class AuthState
{
    private readonly IJSRuntime _js;
    private string? _apiKey;

    private const string StorageKey = "youth_group_api_key";

    public AuthState(IJSRuntime js)
    {
        _js = js;
    }

    public bool IsAuthenticated => !string.IsNullOrEmpty(_apiKey);

    public string? ApiKey => _apiKey;

    public async Task<string?> TryLoadKeyAsync()
    {
        try
        {
            _apiKey = await _js.InvokeAsync<string?>("sessionStorage.getItem", StorageKey);
        }
        catch
        {
            _apiKey = null;
        }
        return _apiKey;
    }

    public async Task SetKeyAsync(string key)
    {
        _apiKey = key;
        try
        {
            await _js.InvokeVoidAsync("sessionStorage.setItem", StorageKey, key);
        }
        catch { }
    }

    public async Task ClearKeyAsync()
    {
        _apiKey = null;
        try
        {
            await _js.InvokeVoidAsync("sessionStorage.removeItem", StorageKey);
        }
        catch { }
    }
}
