using LibraryBarcodeApp.Configuration;
using LibraryBarcodeApp.Models;
using Newtonsoft.Json;
using Supabase;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;

namespace LibraryBarcodeApp.Services;

public class SupabaseService
{
    private const string EmployeeRole = "employee";
    private const string AdminRole = "admin";
    private Client? _client;
    private bool _isInitialized;
    private User? _currentUser;
    private SupabaseSettings? _settings;

    public async Task Initialize()
    {
        if (_isInitialized)
        {
            return;
        }

        _settings = LoadSupabaseSettings();
        if (string.IsNullOrWhiteSpace(_settings.Url) || string.IsNullOrWhiteSpace(_settings.ApiKey))
        {
            throw new InvalidOperationException(
                "Supabase URL/API key is not configured. Set values in appsettings.json.");
        }

        _client = new Client(
            _settings.Url,
            _settings.ApiKey,
            new SupabaseOptions
            {
                AutoRefreshToken = true,
                AutoConnectRealtime = false
            });

        await _client.InitializeAsync();
        _isInitialized = true;
    }

    public async Task<AuthResponse> SignIn(string email, string password)
    {
        try
        {
            email = email.Trim();
            if (await IsBlocked(email))
            {
                await RecordLoginAttempt(email, success: false);
                return new AuthResponse
                {
                    Success = false,
                    Message = "Вход запрещён: пользователь заблокирован."
                };
            }

            var client = EnsureInitialized();
            var session = await client.Auth.SignIn(email, password);
            var authUser = client.Auth.CurrentUser;

            var userIdValue = GetPropertyValueAsString(authUser, "Id", "id");
            if (!Guid.TryParse(userIdValue, out var userId))
            {
                return new AuthResponse
                {
                    Success = false,
                    Message = "Не удалось получить данные пользователя."
                };
            }

            var role = await GetUserRole(userId);
            var fullName = await GetUserFullName(userId);
            var isActive = await GetUserIsActive(userId);
            var accessToken = GetPropertyValueAsString(session, "AccessToken", "access_token", "Token") ?? string.Empty;
            var refreshToken = GetPropertyValueAsString(session, "RefreshToken", "refresh_token") ?? string.Empty;

            if (!isActive)
            {
                await RecordLoginAttempt(email, success: false);
                await SignOut();
                return new AuthResponse
                {
                    Success = false,
                    Message = "Вход запрещён: пользователь заблокирован."
                };
            }

            _currentUser = new User
            {
                Id = userId,
                Email = GetPropertyValueAsString(authUser, "Email", "email") ?? email,
                FullName = fullName,
                Role = role,
                IsActive = isActive,
                LastSignIn = GetPropertyValueAsString(authUser, "LastSignInAt", "last_sign_in_at") is { } last && DateTimeOffset.TryParse(last, out var dto) ? dto : null
            };

            await SaveSessionToFile(accessToken, refreshToken, userId);
            await RecordLoginAttempt(email, success: true);

            return new AuthResponse
            {
                Success = true,
                Message = "Вход выполнен успешно.",
                User = _currentUser,
                AccessToken = accessToken
            };
        }
        catch (Exception ex)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(email))
                {
                    await RecordLoginAttempt(email, success: false);
                }
            }
            catch
            {
                // ignore audit failures
            }

            return new AuthResponse
            {
                Success = false,
                Message = $"Ошибка входа: {ex.Message}"
            };
        }
    }

    public async Task SignOut()
    {
        var client = EnsureInitialized();
        await client.Auth.SignOut();
        _currentUser = null;
        DeleteSessionFile();
    }

    public User? GetCurrentUser()
    {
        return _currentUser;
    }

    public async Task<string> GetUserRole(Guid userId)
    {
        var client = EnsureInitialized();
        var response = await client
            .From<Profile>()
            .Filter("id", Supabase.Postgrest.Constants.Operator.Equals, userId.ToString())
            .Get();

        return response.Models.FirstOrDefault()?.Role ?? EmployeeRole;
    }

    public bool IsAdmin()
    {
        return string.Equals(_currentUser?.Role, AdminRole, StringComparison.OrdinalIgnoreCase);
    }

    public void LoginAsDemo(string role = "employee")
    {
        _currentUser = new User
        {
            Id = Guid.Empty,
            Email = "demo@test.local",
            FullName = "Тестовый пользователь",
            Role = string.Equals(role, AdminRole, StringComparison.OrdinalIgnoreCase) ? AdminRole : EmployeeRole,
            IsActive = true
        };
    }

    public async Task<User> CreateUserByAdmin(string email, string password, string fullName, string role)
    {
        EnsureAdminAccess();
        var created = await AdminCreateUser(email, password);
        var userId = created.Id;

        var normalizedRole = string.Equals(role, AdminRole, StringComparison.OrdinalIgnoreCase) ? AdminRole : EmployeeRole;
        await UpsertProfile(userId, fullName, normalizedRole, isActive: true);

        return new User
        {
            Id = userId,
            Email = created.Email ?? email,
            FullName = fullName,
            Role = normalizedRole,
            IsActive = true,
            LastSignIn = created.LastSignInAt
        };
    }

    public async Task<List<User>> GetAllUsers()
    {
        EnsureAdminAccess();

        var authUsers = await AdminListUsers();
        var users = new List<User>();
        foreach (var authUser in authUsers)
        {
            var userId = authUser.Id;
            var role = await GetUserRole(userId);
            var fullName = await GetUserFullName(userId);
            var isActive = await GetUserIsActive(userId);

            users.Add(new User
            {
                Id = userId,
                Email = authUser.Email ?? string.Empty,
                FullName = fullName,
                Role = role,
                IsActive = isActive,
                LastSignIn = authUser.LastSignInAt
            });
        }

        return users;
    }

    public async Task UpdateUserRole(Guid userId, string newRole)
    {
        EnsureAdminAccess();
        var role = string.Equals(newRole, AdminRole, StringComparison.OrdinalIgnoreCase)
            ? AdminRole
            : EmployeeRole;

        var client = EnsureInitialized();
        var response = await client
            .From<Profile>()
            .Filter("id", Supabase.Postgrest.Constants.Operator.Equals, userId.ToString())
            .Get();

        var profile = response.Models.FirstOrDefault()
            ?? throw new InvalidOperationException("Профиль пользователя не найден.");

        profile.Role = role;
        await profile.Update<Profile>();
    }

    public async Task SetUserActive(Guid userId, bool isActive)
    {
        EnsureAdminAccess();
        var client = EnsureInitialized();
        var response = await client
            .From<Profile>()
            .Filter("id", Supabase.Postgrest.Constants.Operator.Equals, userId.ToString())
            .Get();

        var profile = response.Models.FirstOrDefault()
            ?? throw new InvalidOperationException("Профиль пользователя не найден.");

        profile.IsActive = isActive;
        await profile.Update<Profile>();
    }

    public async Task DeleteUser(Guid userId)
    {
        EnsureAdminAccess();
        await AdminDeleteUser(userId);

        var client = EnsureInitialized();
        try
        {
            var response = await client
                .From<Profile>()
                .Filter("id", Supabase.Postgrest.Constants.Operator.Equals, userId.ToString())
                .Get();

            var profile = response.Models.FirstOrDefault();
            if (profile is not null)
            {
                await profile.Delete<Profile>();
            }
        }
        catch
        {
            // ignore profile delete failures
        }
    }

    public async Task ResetUserPassword(Guid userId, string newPassword)
    {
        EnsureAdminAccess();
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 6)
        {
            throw new InvalidOperationException("Новый пароль должен быть не короче 6 символов.");
        }

        await AdminSetUserPassword(userId, newPassword);
    }

    public async Task RecordLoginAttempt(string email, bool success)
    {
        try
        {
            var client = EnsureInitialized();
            await client.From<global::LibraryBarcodeApp.Models.LoginAttempt>().Insert(new global::LibraryBarcodeApp.Models.LoginAttempt
            {
                Id = Guid.NewGuid(),
                Email = email.Trim(),
                Success = success,
                Timestamp = DateTime.UtcNow
            });
        }
        catch
        {
            // ignore if table missing or RLS blocks
        }
    }

    public async Task<bool> IsBlocked(string email)
    {
        try
        {
            var client = EnsureInitialized();
            var response = await client
                .From<global::LibraryBarcodeApp.Models.BlockedEmail>()
                .Filter("email", Supabase.Postgrest.Constants.Operator.Equals, email.Trim())
                .Get();

            return response.Models.FirstOrDefault()?.IsBlocked ?? false;
        }
        catch
        {
            // if blocked list isn't configured, do not block logins
            return false;
        }
    }

    public async Task<bool> TryRestoreSession()
    {
        var client = EnsureInitialized();
        var savedSession = LoadSessionFromFile();
        if (savedSession is null
            || string.IsNullOrWhiteSpace(savedSession.AccessToken)
            || string.IsNullOrWhiteSpace(savedSession.RefreshToken))
        {
            return false;
        }

        try
        {
            await client.Auth.SetSession(savedSession.AccessToken, savedSession.RefreshToken);
            var authUser = client.Auth.CurrentUser;
            var userIdText = GetPropertyValueAsString(authUser, "Id", "id");
            if (!Guid.TryParse(userIdText, out var userId))
            {
                return false;
            }

            var role = await GetUserRole(userId);
            var fullName = await GetUserFullName(userId);
            var isActive = await GetUserIsActive(userId);

            _currentUser = new User
            {
                Id = userId,
                Email = GetPropertyValueAsString(authUser, "Email", "email") ?? string.Empty,
                FullName = fullName,
                Role = role,
                IsActive = isActive
            };

            return true;
        }
        catch
        {
            DeleteSessionFile();
            _currentUser = null;
            return false;
        }
    }

    public async Task<List<Book>> GetBooks()
    {
        var client = EnsureInitialized();
        var response = await client.From<Book>().Get();
        return response.Models;
    }

    public async Task AddBook(Book book)
    {
        var client = EnsureInitialized();
        await client.From<Book>().Insert(book);
    }

    public async Task DeleteBook(Guid bookId)
    {
        EnsureAdminAccess();

        var client = EnsureInitialized();
        var response = await client
            .From<Book>()
            .Filter("id", Supabase.Postgrest.Constants.Operator.Equals, bookId.ToString())
            .Get();

        var book = response.Models.FirstOrDefault()
            ?? throw new InvalidOperationException("Книга не найдена.");

        await book.Delete<Book>();
    }

    public async Task<Book?> GetBookByBarcode(string barcode)
    {
        var client = EnsureInitialized();
        var response = await client
            .From<Book>()
            .Filter("barcode", Supabase.Postgrest.Constants.Operator.Equals, barcode)
            .Get();

        return response.Models.FirstOrDefault();
    }

    public async Task UpdateBookStatus(Guid bookId, string status)
    {
        var client = EnsureInitialized();
        var response = await client
            .From<Book>()
            .Filter("id", Supabase.Postgrest.Constants.Operator.Equals, bookId.ToString())
            .Single();

        var book = response ?? throw new InvalidOperationException("Book not found.");
        book.Status = status;

        await book.Update<Book>();
    }

    public async Task<List<Operation>> GetOperations()
    {
        var client = EnsureInitialized();
        var response = await client.From<Operation>().Get();
        return response.Models;
    }

    public async Task<List<Operation>> GetOperationsByDateRange(DateTime startDate, DateTime endDate)
    {
        var client = EnsureInitialized();
        var response = await client
            .From<Operation>()
            .Filter("timestamp", Supabase.Postgrest.Constants.Operator.GreaterThanOrEqual, startDate.ToUniversalTime().ToString("o"))
            .Filter("timestamp", Supabase.Postgrest.Constants.Operator.LessThanOrEqual, endDate.ToUniversalTime().ToString("o"))
            .Get();

        return response.Models;
    }

    public async Task<Dictionary<string, int>> GetBooksByCategory()
    {
        var books = await GetBooks();
        return books
            .GroupBy(b => string.IsNullOrWhiteSpace(b.Category) ? "Без категории" : b.Category)
            .ToDictionary(g => g.Key, g => g.Count());
    }

    public async Task<Dictionary<string, int>> GetBooksByStatus()
    {
        var books = await GetBooks();
        return books
            .GroupBy(b => string.IsNullOrWhiteSpace(b.Status) ? "unknown" : b.Status)
            .ToDictionary(g => g.Key, g => g.Count());
    }

    public async Task AddOperation(Operation operation)
    {
        var client = EnsureInitialized();
        await client.From<Operation>().Insert(operation);
    }

    private async Task UpsertProfile(Guid userId, string fullName, string role, bool isActive)
    {
        var client = EnsureInitialized();
        var existing = await client
            .From<Profile>()
            .Filter("id", Supabase.Postgrest.Constants.Operator.Equals, userId.ToString())
            .Get();

        var profile = existing.Models.FirstOrDefault();
        if (profile is null)
        {
            await client.From<Profile>().Insert(new Profile
            {
                Id = userId,
                FullName = fullName,
                Role = role,
                IsActive = isActive
            });
            return;
        }

        profile.FullName = fullName;
        profile.Role = role;
        profile.IsActive = isActive;
        await profile.Update<Profile>();
    }

    private async Task<string> GetUserFullName(Guid userId)
    {
        var client = EnsureInitialized();
        var response = await client
            .From<Profile>()
            .Filter("id", Supabase.Postgrest.Constants.Operator.Equals, userId.ToString())
            .Get();

        return response.Models.FirstOrDefault()?.FullName ?? string.Empty;
    }

    private async Task<bool> GetUserIsActive(Guid userId)
    {
        var client = EnsureInitialized();
        var response = await client
            .From<Profile>()
            .Filter("id", Supabase.Postgrest.Constants.Operator.Equals, userId.ToString())
            .Get();

        return response.Models.FirstOrDefault()?.IsActive ?? true;
    }

    private static SupabaseSettings LoadSupabaseSettings()
    {
        var configPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        if (!File.Exists(configPath))
        {
            throw new FileNotFoundException("appsettings.json not found.", configPath);
        }

        var json = File.ReadAllText(configPath);
        var root = JsonConvert.DeserializeObject<AppSettingsRoot>(json)
            ?? throw new InvalidOperationException("Invalid appsettings.json.");

        return root.Supabase;
    }

    private Client EnsureInitialized()
    {
        if (!_isInitialized || _client is null)
        {
            throw new InvalidOperationException("SupabaseService is not initialized. Call Initialize() first.");
        }

        return _client;
    }

    private SupabaseSettings GetSettings()
    {
        if (_settings is null)
        {
            throw new InvalidOperationException("Supabase settings not loaded. Call Initialize() first.");
        }

        return _settings;
    }

    private string GetServiceRoleKeyOrThrow()
    {
        var key = GetSettings().ServiceRoleKey?.Trim();
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new InvalidOperationException(
                "ServiceRoleKey не задан в appsettings.json. " +
                "Админские операции (Admin API) недоступны без service-role ключа.");
        }

        return key;
    }

    private HttpClient CreateAdminHttpClient()
    {
        var settings = GetSettings();
        var serviceKey = GetServiceRoleKeyOrThrow();

        var client = new HttpClient
        {
            BaseAddress = new Uri(settings.Url.TrimEnd('/') + "/")
        };
        client.DefaultRequestHeaders.Add("apikey", serviceKey);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", serviceKey);
        return client;
    }

    private async Task<AdminUserDto> AdminCreateUser(string email, string password)
    {
        using var http = CreateAdminHttpClient();

        var body = new
        {
            email = email.Trim(),
            password,
            email_confirm = true
        };

        var json = JsonConvert.SerializeObject(body);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        var resp = await http.PostAsync("auth/v1/admin/users", content);
        var payload = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Admin API create user failed: {payload}");
        }

        return JsonConvert.DeserializeObject<AdminUserDto>(payload)
               ?? throw new InvalidOperationException("Admin API returned empty user.");
    }

    private async Task<List<AdminUserDto>> AdminListUsers()
    {
        using var http = CreateAdminHttpClient();

        var users = new List<AdminUserDto>();
        var page = 1;
        while (true)
        {
            var resp = await http.GetAsync($"auth/v1/admin/users?page={page}&per_page=100");
            var payload = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"Admin API list users failed: {payload}");
            }

            var batch = JsonConvert.DeserializeObject<List<AdminUserDto>>(payload) ?? new List<AdminUserDto>();
            if (batch.Count == 0)
            {
                break;
            }

            users.AddRange(batch);
            if (batch.Count < 100)
            {
                break;
            }

            page++;
        }

        return users;
    }

    private async Task AdminDeleteUser(Guid userId)
    {
        using var http = CreateAdminHttpClient();
        var resp = await http.DeleteAsync($"auth/v1/admin/users/{userId}");
        var payload = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Admin API delete user failed: {payload}");
        }
    }

    private async Task AdminSetUserPassword(Guid userId, string newPassword)
    {
        using var http = CreateAdminHttpClient();
        var body = new { password = newPassword };
        var json = JsonConvert.SerializeObject(body);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        var resp = await http.PutAsync($"auth/v1/admin/users/{userId}", content);
        var payload = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Admin API set password failed: {payload}");
        }
    }

    private void EnsureAdminAccess()
    {
        if (!IsAdmin())
        {
            throw new InvalidOperationException("Доступ запрещён: требуется роль администратора.");
        }
    }

    private sealed class AdminUserDto
    {
        [JsonProperty("id")]
        public Guid Id { get; set; }

        [JsonProperty("email")]
        public string? Email { get; set; }

        [JsonProperty("last_sign_in_at")]
        public DateTimeOffset? LastSignInAt { get; set; }
    }

    private static string? GetPropertyValueAsString(object? source, params string[] propertyNames)
    {
        if (source is null)
        {
            return null;
        }

        var type = source.GetType();
        foreach (var propertyName in propertyNames)
        {
            var property = type.GetProperty(propertyName);
            if (property is null)
            {
                continue;
            }

            var value = property.GetValue(source);
            if (value is null)
            {
                continue;
            }

            return value.ToString();
        }

        return null;
    }

    private static object? GetPropertyValue(object? source, params string[] propertyNames)
    {
        if (source is null)
        {
            return null;
        }

        var type = source.GetType();
        foreach (var propertyName in propertyNames)
        {
            var property = type.GetProperty(propertyName);
            if (property is null)
            {
                continue;
            }

            return property.GetValue(source);
        }

        return null;
    }

    private static string GetSessionFilePath()
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LibraryBarcodeApp");
        Directory.CreateDirectory(folder);
        return Path.Combine(folder, "session.json");
    }

    private static async Task SaveSessionToFile(string accessToken, string refreshToken, Guid userId)
    {
        var sessionFilePath = GetSessionFilePath();
        var payload = new LocalSession
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            UserId = userId
        };

        var json = JsonConvert.SerializeObject(payload, Formatting.Indented);
        await File.WriteAllTextAsync(sessionFilePath, json);
    }

    private static LocalSession? LoadSessionFromFile()
    {
        var sessionFilePath = GetSessionFilePath();
        if (!File.Exists(sessionFilePath))
        {
            return null;
        }

        var json = File.ReadAllText(sessionFilePath);
        return JsonConvert.DeserializeObject<LocalSession>(json);
    }

    private static void DeleteSessionFile()
    {
        var sessionFilePath = GetSessionFilePath();
        if (File.Exists(sessionFilePath))
        {
            File.Delete(sessionFilePath);
        }
    }

    private sealed class AppSettingsRoot
    {
        [JsonProperty("Supabase")]
        public SupabaseSettings Supabase { get; set; } = new();
    }

    private sealed class LocalSession
    {
        [JsonProperty("accessToken")]
        public string AccessToken { get; set; } = string.Empty;

        [JsonProperty("refreshToken")]
        public string RefreshToken { get; set; } = string.Empty;

        [JsonProperty("userId")]
        public Guid UserId { get; set; }
    }
}
