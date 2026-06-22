// METADATA file_path: Assets/1. Scripts/7. EmployeeSystem/AvatarRegistry.cs
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tracks which avatar textures (male/female) have been assigned to employees.
/// Prevents duplicate avatar assignments across the lifetime of a save.
/// 
/// Persists via EmployeeRegistry serialization.
/// </summary>
[Serializable]
public class AvatarRegistry
{
    [Serializable]
    public class AvatarUseRecord
    {
        public string resourceKey;      // e.g. "Male/avatar_01" or "Female/avatar_03"
        public string assignedToGuid;   // Employee GUID who is using this avatar
    }

    public List<AvatarUseRecord> usedAvatars = new List<AvatarUseRecord>();

    // ─── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Request the next available avatar for the given gender.
    /// Returns a resource key (e.g. "Male/avatar_01") and records it as used.
    /// If all avatars in that gender are exhausted, recycles the oldest assignment.
    /// </summary>
    public string AssignAvatar(EmployeeGender gender, string employeeGuid)
    {
        string folderPrefix = gender == EmployeeGender.Female ? "Female" : "Male";
        var availableAvatars = LoadAvatarsForGender(gender);

        if (availableAvatars.Count == 0)
        {
            Debug.LogWarning($"[AvatarRegistry] No avatars found for gender {gender}. Using fallback.");
            return $"{folderPrefix}/avatar_default";
        }

        // Find the first unused avatar
        string assignedKey = null;
        foreach (var avatarKey in availableAvatars)
        {
            bool isUsed = usedAvatars.Exists(u => u.resourceKey == avatarKey);
            if (!isUsed)
            {
                assignedKey = avatarKey;
                break;
            }
        }

        // If all are used, recycle the oldest (first in list)
        if (assignedKey == null)
        {
            if (usedAvatars.Count > 0 && usedAvatars[0].resourceKey.StartsWith(folderPrefix))
            {
                usedAvatars.RemoveAt(0);
            }
            assignedKey = availableAvatars[0];
        }

        // Record the assignment
        usedAvatars.Add(new AvatarUseRecord
        {
            resourceKey = assignedKey,
            assignedToGuid = employeeGuid
        });

        return assignedKey;
    }

    /// <summary>
    /// Release an avatar back to the pool (e.g. if an employee is deleted).
    /// </summary>
    public void ReleaseAvatar(string employeeGuid)
    {
        usedAvatars.RemoveAll(u => u.assignedToGuid == employeeGuid);
    }

    /// <summary>
    /// Get the avatar assigned to an employee (or null if not found).
    /// </summary>
    public string GetAvatarForEmployee(string employeeGuid)
    {
        var record = usedAvatars.Find(u => u.assignedToGuid == employeeGuid);
        return record?.resourceKey;
    }

    // ─── Private helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Hardcoded fallback list of known sprite file names (without extension) per gender.
    /// Used only when Resources.LoadAll returns empty (e.g. in builds where the folder
    /// isn't included or path has changed).
    /// </summary>
    private static readonly string[] MaleFallbackNames =
    {
        "African_american_bald_Haired_S",
        "Black_Haired_Small_sized_Eyes_",
        "Black_and_Gray_Haired_Small_si",
        "Blonde_and_Gray_Haired_Regular",
        "Regular_sized_Eyes_Centered_me",
        "bald_Haired_Small_sized_Eyes_m",
        "low-poly_style_3D_video_game_a"
    };

    private static readonly string[] FemaleFallbackNames =
    {
        "African_Black_Haired_Small_siz",
        "Asian_Black_Haired_Small_sized",
        "Black_and_Gray_Haired_Small_si",
        "Blonde_and_Gray_Haired_Regular",
        "Centered_medium_shot_waist-up_",
        "Hispanic_Black_Haired_Small_si",
        "Regular_sized_Eyes_Centered_me",
        "low-poly_style_3D_video_game_a"
    };

    private static List<string> LoadAvatarsForGender(EmployeeGender gender)
    {
        string resourcesPath = gender == EmployeeGender.Female
            ? "EmployeeAssets/Female/FemaleSprites"
            : "EmployeeAssets/Male/Male Sprites";

        // The key prefix matches what EmployeeIdentity.cs prepends with "EmployeeAssets/"
        // e.g. key "Male/Male Sprites/foo" → load "EmployeeAssets/Male/Male Sprites/foo"
        string keyPrefix = gender == EmployeeGender.Female
            ? "Female/FemaleSprites"
            : "Male/Male Sprites";

        var textures = Resources.LoadAll<Texture2D>(resourcesPath);

        var result = new List<string>();
        foreach (var tex in textures)
        {
            string resourceKey = $"{keyPrefix}/{tex.name}";
            result.Add(resourceKey);
        }

        // If empty, use hardcoded fallback names that match the actual .jpeg files
        if (result.Count == 0)
        {
            Debug.LogWarning($"[AvatarRegistry] No textures loaded from \"{resourcesPath}\". Using hardcoded fallback names.");
            string[] fallbackNames = gender == EmployeeGender.Female ? FemaleFallbackNames : MaleFallbackNames;
            foreach (string name in fallbackNames)
                result.Add($"{keyPrefix}/{name}");
        }

        return result;
    }

}
