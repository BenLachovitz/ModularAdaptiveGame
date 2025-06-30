using System;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using UnityEditor;
using System.Text;
using System.Threading.Tasks;

[System.Serializable]
public class LicenseData
{
    public string licenseKey = "";
    public string userInfo = "";
    public bool isActive = false;
    public string activatedDate = "";
    public string lastValidated = "";
    public long lastValidatedTimestamp = 0;
}

[System.Serializable]
public class LicenseRequest
{
    public string licenseKey;
    public string userInfo;
    public string action;
}

[System.Serializable]
public class LicenseResponse
{
    public bool valid;
    public string message;
    public string[] features;
}

public static class LicenseValidator
{
    private const string API_URL = "https://fpjgxaivlwlbbhjircsf.supabase.co/functions/v1/validate-license";
    private const string ANON_KEY = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6ImZwamd4YWl2bHdsYmJoamlyY3NmIiwicm9sZSI6ImFub24iLCJpYXQiOjE3NDk0NDY1MzgsImV4cCI6MjA2NTAyMjUzOH0.cSyxpv8-PvOUjqq-T0QWWFU72K6uExslefOdyG5yf9g";

    // Validation intervals (in hours)
    private const int BACKGROUND_VALIDATION_HOURS = 8;  // Check every 8 hours in background
    private const int CRITICAL_VALIDATION_HOURS = 48;   // Force validation every 2 days max

    // Smart validation controls
    private static bool _isValidating = false;
    private static DateTime _lastUIValidationCheck = DateTime.MinValue;
    private static bool _hasShownRevokedDialog = false;
    private const int UI_VALIDATION_COOLDOWN_MINUTES = 1; // Only validate every 5 minutes from UI
    private const int SMART_VALIDATION_INTERVAL_MINUTES = 2; // Validate every 30 minutes when using Pro features

    private static LicenseData _currentLicense;
    private static string LicenseFilePath => Path.Combine(Application.persistentDataPath, "license.json");

    public static bool isRevoked = false;

    public static bool IsProActive
    {
        get
        {
            if (_currentLicense == null)
            {
                _currentLicense = LoadLicenseData();

                // Only do startup validation once
                if (_currentLicense.isActive && ShouldValidateInBackground())
                {
                    PerformBackgroundValidation();
                }
            }
            return _currentLicense.isActive;
        }
    }

    // Smart pro feature check that doesn't spam the database
    public static bool IsProFeatureEnabledSmart(string featureName = "Pro Feature")
    {
        // First check local license
        if (!IsProActive)
            return false;

        // Check if we should do a UI-triggered validation
        if (ShouldDoUIValidation())
        {
            TriggerSmartValidation(featureName);
        }

        return _currentLicense.isActive;
    }

    // Initialize license system when Unity loads
    [InitializeOnLoadMethod]
    static void InitializeLicenseSystem()
    {
        EditorApplication.delayCall += () => {
            Debug.Log("License system initialized");

            // Perform startup validation if needed
            if (_currentLicense == null)
                _currentLicense = LoadLicenseData();

            if (_currentLicense.isActive && ShouldValidateInBackground())
            {
                Debug.Log("Performing startup license validation...");
                PerformBackgroundValidation();
            }
        };
    }

    private static bool ShouldValidateInBackground()
    {
        if (_currentLicense == null || !_currentLicense.isActive)
            return false;

        // Check if validation is overdue
        long currentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        long timeSinceLastValidation = currentTime - _currentLicense.lastValidatedTimestamp;
        long hoursElapsed = timeSinceLastValidation / 3600;

        return hoursElapsed >= BACKGROUND_VALIDATION_HOURS;
    }

    private static bool ShouldDoUIValidation()
    {
        // Don't validate if already validating
        if (_isValidating)
            return false;

        // Don't validate too frequently
        TimeSpan timeSinceLastCheck = DateTime.Now - _lastUIValidationCheck;
        if (timeSinceLastCheck.TotalMinutes < UI_VALIDATION_COOLDOWN_MINUTES)
            return false;

        // Check if it's been a while since last validation
        long currentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        long timeSinceLastValidation = currentTime - _currentLicense.lastValidatedTimestamp;
        long minutesElapsed = timeSinceLastValidation / 60;

        // Validate if it's been more than 30 minutes
        return minutesElapsed >= SMART_VALIDATION_INTERVAL_MINUTES;
    }

    private static async void TriggerSmartValidation(string featureName)
    {
        _isValidating = true;
        _lastUIValidationCheck = DateTime.Now;

        try
        {
            Debug.Log($"Smart validation triggered by {featureName}");

            bool isValid = await ValidateWithDatabase(_currentLicense.licenseKey, _currentLicense.userInfo, "activate");

            if (!isValid)
            {
                // License was revoked
                _currentLicense.isActive = false;
                SaveLicenseData(_currentLicense);
                isRevoked = true;

                Debug.LogWarning("License validation failed - license was revoked!");

                // Only show dialog once per session
                if (!_hasShownRevokedDialog)
                {
                    _hasShownRevokedDialog = true;
                    EditorApplication.delayCall += () => {
                        EditorUtility.DisplayDialog("License Revoked",
                            "Your license has been revoked or deleted by an administrator. Pro features are now disabled.",
                            "OK");
                    };
                }
            }
            else
            {
                // Update validation timestamp
                _currentLicense.lastValidated = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                _currentLicense.lastValidatedTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                SaveLicenseData(_currentLicense);
                Debug.Log("Smart validation successful");
                isRevoked = false;
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Smart validation failed: {e.Message}");
        }
        finally
        {
            _isValidating = false;
        }
    }

    public static bool getIsRevoked()
    {
        return isRevoked;
    }

    private static async void PerformBackgroundValidation()
    {
        if (_currentLicense == null || string.IsNullOrEmpty(_currentLicense.licenseKey))
            return;

        try
        {
            Debug.Log("Performing background license validation...");

            bool isValid = await ValidateWithDatabase(_currentLicense.licenseKey, _currentLicense.userInfo, "activate");

            if (isValid)
            {
                // Update last validation time
                _currentLicense.lastValidated = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                _currentLicense.lastValidatedTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                _currentLicense.isActive = true;
                SaveLicenseData(_currentLicense);
                Debug.Log("Background validation successful - license still valid");
            }
            else
            {
                // License is no longer valid (deleted, deactivated, etc.)
                _currentLicense.isActive = false;
                SaveLicenseData(_currentLicense);
                Debug.LogWarning("Background validation failed - license revoked or deleted!");
                Debug.LogWarning("Pro features have been automatically disabled");

                // Show dialog to user if not already shown
                if (!_hasShownRevokedDialog)
                {
                    _hasShownRevokedDialog = true;
                    EditorApplication.delayCall += () => {
                        EditorUtility.DisplayDialog("License Revoked",
                            "Your license is no longer valid and has been deactivated. Pro features are now disabled.\n\nThis may happen if your license was deleted by an administrator.",
                            "OK");
                    };
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Background validation failed due to network error: {e.Message}");

            // Check if license is too old without validation
            long currentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            long timeSinceLastValidation = currentTime - _currentLicense.lastValidatedTimestamp;
            long hoursElapsed = timeSinceLastValidation / 3600;

            if (hoursElapsed >= CRITICAL_VALIDATION_HOURS)
            {
                Debug.LogError("License too old without validation - forcing deactivation for security");
                _currentLicense.isActive = false;
                SaveLicenseData(_currentLicense);

                if (!_hasShownRevokedDialog)
                {
                    _hasShownRevokedDialog = true;
                    EditorApplication.delayCall += () => {
                        EditorUtility.DisplayDialog("License Validation Required",
                            "Your license requires validation but network connection failed. For security reasons, Pro features have been temporarily disabled.\n\nPlease check your internet connection and reactivate your license.",
                            "OK");
                    };
                }
            }
        }
    }

    public static async void ValidateLicense(string licenseKey, System.Action<bool, string> callback)
    {
        try
        {
            Debug.Log("Starting license activation for key: " + licenseKey);

            var request = new LicenseRequest
            {
                licenseKey = licenseKey,
                userInfo = GetUserInfo(),
                action = "activate"
            };
            string jsonData = JsonUtility.ToJson(request);
            Debug.Log("JSON data prepared: " + jsonData);

            using (UnityWebRequest www = new UnityWebRequest(API_URL, "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);
                www.uploadHandler = new UploadHandlerRaw(bodyRaw);
                www.downloadHandler = new DownloadHandlerBuffer();
                www.SetRequestHeader("Content-Type", "application/json");
                www.SetRequestHeader("apikey", ANON_KEY);
                www.SetRequestHeader("Authorization", "Bearer " + ANON_KEY);

                Debug.Log("Sending activation request to: " + API_URL);
                var operation = www.SendWebRequest();

                while (!operation.isDone)
                {
                    await Task.Delay(100);
                }

                Debug.Log("Request completed with result: " + www.result);

                if (www.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError("License activation failed: " + www.error);
                    callback?.Invoke(false, "Network error: " + www.error);
                    return;
                }

                Debug.Log("Response received: " + www.downloadHandler.text);

                try
                {
                    LicenseResponse response = JsonUtility.FromJson<LicenseResponse>(www.downloadHandler.text);
                    Debug.Log("Response parsed successfully");

                    if (response.valid)
                    {
                        // Reset dialog flag for new license
                        _hasShownRevokedDialog = false;

                        // Save to JSON file with validation timestamp
                        _currentLicense = new LicenseData
                        {
                            licenseKey = licenseKey,
                            userInfo = GetUserInfo(),
                            isActive = true,
                            activatedDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                            lastValidated = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                            lastValidatedTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                        };

                        SaveLicenseData(_currentLicense);

                        isRevoked = false;

                        // Log different scenarios based on server response
                        if (response.message.Contains("reactivated successfully"))
                        {
                            Debug.Log("REACTIVATION: License reactivated successfully! Welcome back!");
                        }
                        else if (response.message.Contains("synchronized successfully"))
                        {
                            Debug.Log("SYNC: License synchronized successfully - welcome back!");
                        }
                        else if (response.message.Contains("activated successfully"))
                        {
                            Debug.Log("NEW ACTIVATION: License activated successfully! Pro features unlocked!");
                        }
                        else
                        {
                            Debug.Log("LICENSE VALID: " + response.message);
                        }

                        callback?.Invoke(true, response.message);
                    }
                    else
                    {
                        Debug.LogWarning("License activation failed: " + response.message);

                        // Log specific failure reasons
                        if (response.message.Contains("already owned by another user"))
                        {
                            Debug.LogWarning("KEY SHARING BLOCKED: This license key belongs to someone else");
                        }
                        else if (response.message.Contains("Invalid license key"))
                        {
                            Debug.LogWarning("INVALID KEY: License key not found in database");
                        }

                        callback?.Invoke(false, response.message);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError("Failed to parse response: " + e.Message);
                    callback?.Invoke(false, "Invalid response: " + e.Message);
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError("License activation exception: " + e.Message);
            callback?.Invoke(false, "Activation error: " + e.Message);
        }
    }

    public static async void DeactivateLicense()
    {
        try
        {
            if (_currentLicense != null && !string.IsNullOrEmpty(_currentLicense.licenseKey))
            {
                Debug.Log("Starting license deactivation");

                var request = new LicenseRequest
                {
                    licenseKey = _currentLicense.licenseKey,
                    userInfo = _currentLicense.userInfo,
                    action = "deactivate"
                };
                string jsonData = JsonUtility.ToJson(request);

                using (UnityWebRequest www = new UnityWebRequest(API_URL, "POST"))
                {
                    byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);
                    www.uploadHandler = new UploadHandlerRaw(bodyRaw);
                    www.downloadHandler = new DownloadHandlerBuffer();
                    www.SetRequestHeader("Content-Type", "application/json");
                    www.SetRequestHeader("apikey", ANON_KEY);
                    www.SetRequestHeader("Authorization", "Bearer " + ANON_KEY);

                    Debug.Log("Sending deactivation request");
                    var operation = www.SendWebRequest();
                    while (!operation.isDone)
                    {
                        await Task.Delay(100);
                    }

                    if (www.result == UnityWebRequest.Result.Success)
                    {
                        Debug.Log("License deactivated in database successfully");
                    }
                    else
                    {
                        Debug.LogError("Failed to deactivate in database: " + www.error);
                    }
                }
            }
            else
            {
                Debug.LogWarning("No license key stored to deactivate");
            }
        }
        catch (Exception e)
        {
            Debug.LogError("Deactivation error: " + e.Message);
        }
        finally
        {
            // Always clear local license
            _currentLicense = new LicenseData();
            SaveLicenseData(_currentLicense);
            _hasShownRevokedDialog = false; // Reset dialog flag
            Debug.Log("License deactivated locally and JSON file cleared");
        }
    }

    private static async Task<bool> ValidateWithDatabase(string licenseKey, string userInfo, string action)
    {
        var request = new LicenseRequest
        {
            licenseKey = licenseKey,
            userInfo = userInfo,
            action = action
        };
        string jsonData = JsonUtility.ToJson(request);

        using (UnityWebRequest www = new UnityWebRequest(API_URL, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");
            www.SetRequestHeader("apikey", ANON_KEY);
            www.SetRequestHeader("Authorization", "Bearer " + ANON_KEY);

            var operation = www.SendWebRequest();

            while (!operation.isDone)
            {
                await Task.Delay(100);
            }

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("Database validation failed: " + www.error);
                return false;
            }

            try
            {
                LicenseResponse response = JsonUtility.FromJson<LicenseResponse>(www.downloadHandler.text);
                return response.valid;
            }
            catch (Exception e)
            {
                Debug.LogError("Failed to parse validation response: " + e.Message);
                return false;
            }
        }
    }

    private static LicenseData LoadLicenseData()
    {
        try
        {
            if (File.Exists(LicenseFilePath))
            {
                string json = File.ReadAllText(LicenseFilePath);
                var data = JsonUtility.FromJson<LicenseData>(json);
                Debug.Log($"License loaded from: {LicenseFilePath}");
                return data ?? new LicenseData();
            }
        }
        catch (Exception e)
        {
            Debug.LogError("Failed to load license: " + e.Message);
        }

        return new LicenseData();
    }

    private static void SaveLicenseData(LicenseData data)
    {
        try
        {
            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(LicenseFilePath, json);
            Debug.Log($"License saved to: {LicenseFilePath}");
        }
        catch (Exception e)
        {
            Debug.LogError("Failed to save license: " + e.Message);
        }
    }

    private static string GetUserInfo()
    {
        return System.Environment.UserName;
    }

    // Development/testing methods
    [MenuItem("Tools/License/Clear All License Data")]
    public static void ClearLicenseData()
    {
        try
        {
            if (File.Exists(LicenseFilePath))
                File.Delete(LicenseFilePath);
            _currentLicense = new LicenseData();
            _hasShownRevokedDialog = false;
            Debug.Log("License file deleted - simulating fresh installation");
        }
        catch (Exception e)
        {
            Debug.LogError("Failed to clear license: " + e.Message);
        }
    }

    [MenuItem("Tools/License/Show License File Path")]
    public static void ShowLicenseFilePath()
    {
        Debug.Log($"License file path: {LicenseFilePath}");
        Debug.Log($"File exists: {File.Exists(LicenseFilePath)}");

        if (File.Exists(LicenseFilePath))
        {
            Debug.Log("File contents:");
            Debug.Log(File.ReadAllText(LicenseFilePath));
        }
    }

    [MenuItem("Tools/License/Reset Validation Timers")]
    public static void ResetValidationTimers()
    {
        _lastUIValidationCheck = DateTime.MinValue;
        _hasShownRevokedDialog = false;
        _isValidating = false;
        Debug.Log("Validation timers reset");
    }

    [MenuItem("Tools/License/Force Smart Validation")]
    public static void ForceSmartValidation()
    {
        if (_currentLicense != null && _currentLicense.isActive)
        {
            TriggerSmartValidation("Manual Test");
        }
        else
        {
            Debug.LogWarning("No active license to validate");
        }
    }
}