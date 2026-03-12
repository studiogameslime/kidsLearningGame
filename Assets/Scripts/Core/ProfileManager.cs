using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Singleton that manages user profiles with local JSON storage.
/// Persists across scenes via DontDestroyOnLoad.
/// </summary>
public class ProfileManager : MonoBehaviour
{
    public static ProfileManager Instance { get; private set; }
    public static UserProfile ActiveProfile { get; private set; }

    private ProfileStore _store;
    private string _savePath;

    public List<UserProfile> Profiles => _store?.profiles ?? new List<UserProfile>();
    public bool HasProfiles => _store != null && _store.profiles.Count > 0;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoCreate()
    {
        if (Instance != null) return;
        var go = new GameObject("ProfileManager");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<ProfileManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        _savePath = Path.Combine(Application.persistentDataPath, "profiles.json");
        Load();
    }

    public void Load()
    {
        if (File.Exists(_savePath))
        {
            try
            {
                string json = File.ReadAllText(_savePath);
                _store = JsonUtility.FromJson<ProfileStore>(json);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"ProfileManager: Failed to load profiles: {e.Message}");
                _store = new ProfileStore();
            }
        }
        else
        {
            _store = new ProfileStore();
        }

        // Restore active profile
        if (!string.IsNullOrEmpty(_store.activeProfileId))
        {
            ActiveProfile = _store.profiles.Find(p => p.id == _store.activeProfileId);
        }
    }

    public void Save()
    {
        try
        {
            string json = JsonUtility.ToJson(_store, true);
            File.WriteAllText(_savePath, json);
        }
        catch (Exception e)
        {
            Debug.LogError($"ProfileManager: Failed to save profiles: {e.Message}");
        }
    }

    public UserProfile CreateProfile(string name, int age, string colorHex)
    {
        var profile = new UserProfile
        {
            displayName = name,
            age = age,
            avatarColorHex = colorHex
        };
        _store.profiles.Add(profile);
        Save();
        return profile;
    }

    public void SetActiveProfile(UserProfile profile)
    {
        ActiveProfile = profile;
        _store.activeProfileId = profile?.id;
        if (profile != null)
            profile.lastPlayedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        Save();
    }

    public void DeleteProfile(string profileId)
    {
        _store.profiles.RemoveAll(p => p.id == profileId);
        if (_store.activeProfileId == profileId)
        {
            _store.activeProfileId = null;
            ActiveProfile = null;
        }

        // Delete associated files (avatar image, name audio)
        string profileDir = Path.Combine(Application.persistentDataPath, "profiles", profileId);
        if (Directory.Exists(profileDir))
        {
            try { Directory.Delete(profileDir, true); }
            catch (Exception e) { Debug.LogWarning($"Failed to delete profile files: {e.Message}"); }
        }

        Save();
    }

    public void UpdateProfile(UserProfile profile)
    {
        Save();
    }

    /// <summary>Returns the folder for a profile's files (avatar, audio). Creates it if needed.</summary>
    public string GetProfileFolder(string profileId)
    {
        string dir = Path.Combine(Application.persistentDataPath, "profiles", profileId);
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);
        return dir;
    }

    public void RecordGamePlayed(string gameId)
    {
        if (ActiveProfile == null) return;
        var stat = ActiveProfile.progress.GetOrCreate(gameId);
        stat.timesPlayed++;
        stat.lastPlayedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        ActiveProfile.lastPlayedAt = stat.lastPlayedAt;
        Save();
    }
}
