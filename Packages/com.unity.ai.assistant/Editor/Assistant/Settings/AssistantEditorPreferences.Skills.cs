using System.Collections.Generic;
using System.IO;
using Unity.AI.Assistant.Skills;
using UnityEditor;
using UnityEngine;

namespace Unity.AI.Assistant.Editor
{
    static partial class AssistantEditorPreferences
    {
        // EditorPrefs keys use composite "name:normalizedPath"; see CompositeKey() / NormalizePath().
        // Each skill instance gets its own allowed/seen state even when multiple share the same name.
        const string k_SkillSeenPrefix = k_SettingsPrefix + "SkillSeen.";
        const string k_SkillAllowedPrefix = k_SettingsPrefix + "SkillAllowed.";
        const string k_CurrentSkillKeysKey = k_SettingsPrefix + "CurrentSkillKeys";

        // Immutable HashSet snapshots keyed by composite key; replaced atomically so IsSkillAllowedFilter
        // can read from any thread without a lock (HashSet reads are safe on a frozen instance).
        static volatile HashSet<string> s_AllowedSkillNamesCache;

        public static event System.Action SkillAllowedStateChanged;

        // Resets the skill filter to null (no filtering). Use in test TearDown to undo any
        // per-test SetSkillFilter calls without assuming InitSkillOptIn fired during the test run.
        internal static void RestoreDefaultFilter() => SkillsRegistry.SetSkillFilter(null);

        [InitializeOnLoadMethod]
        static void InitSkillOptIn()
        {
            // Eagerly populate the cache (optimization: runs before GetSkills() or the settings UI accesses it).
            // IsSkillAllowedFilter also calls EnsureAllowedKeysLoaded() as a safety net if this fires late.
            EnsureAllowedKeysLoaded();
            SkillsRegistry.SetSkillFilter(IsSkillAllowedFilter);
            SkillsScanner.OnSkillsRescanned += OnSkillsRescanned;
        }

        // Reads persisted composite keys from EditorPrefs rather than live registry candidates,
        // so the filter is correct before the first scan completes. No-op if already loaded.
        // Use RebuildAllowedSkillsCache() after a scan to sync from actual registered skills.
        static void EnsureAllowedKeysLoaded()
        {
            if (s_AllowedSkillNamesCache != null)
                return;
            var storedKeys = LoadCurrentSkillKeys();
            var cache = new HashSet<string>(System.StringComparer.Ordinal);
            foreach (var key in storedKeys)
            {
                if (EditorPrefs.GetBool(k_SkillAllowedPrefix + key, false))
                    cache.Add(key);
            }
            s_AllowedSkillNamesCache = cache;
        }

        // Registered with SkillsRegistry.SetSkillFilter; gates which skills GetSkills() returns.
        // Internal and BuiltIn skills always pass; others must be explicitly allowed in EditorPrefs.
        static bool IsSkillAllowedFilter(SkillDefinition skill)
        {
            if (SkillRegistryTags.IsInternalOrBuiltIn(skill.Tags))
                return true;
            if (s_AllowedSkillNamesCache == null)
                EnsureAllowedKeysLoaded();
            var cache = s_AllowedSkillNamesCache;
            return cache != null && cache.Contains(CompositeKey(skill));
        }

        static void OnSkillsRescanned()
        {
            var allSkills = SkillsRegistry.GetAllSkillsNoWait();

            // Always rebuild so GetSkills() is correctly filtered after every partial scan.
            RebuildAllowedSkillsCache(allSkills);

            // New-skill detection only runs once all scans have finished.
            if (!SkillsRegistry.IsLoadComplete)
                return;

            CleanupRemovedSkills(allSkills);

            var newSkillNames = new List<string>();
            foreach (var skill in allSkills)
            {
                if (SkillRegistryTags.IsInternalOrBuiltIn(skill.Tags))
                    continue;

                var key = CompositeKey(skill);
                if (!GetSkillSeen(key))
                {
                    MarkSkillSeen(key);
                    newSkillNames.Add(skill.MetaData.Name);
                }
            }

            if (newSkillNames.Count > 0)
            {
                const int maxListed = 10;
                var listed = newSkillNames.Count <= maxListed
                    ? newSkillNames
                    : newSkillNames.GetRange(0, maxListed);
                var extra = newSkillNames.Count - listed.Count;
                var nameList = string.Join(", ", listed) + (extra > 0 ? $", and {extra} more" : "");
                var header = newSkillNames.Count == 1
                    ? "A new AI Assistant skill was discovered"
                    : $"{newSkillNames.Count} new AI Assistant skills were discovered";
                var message = $"{header}: {nameList}. New skills are denied by default. Open Preferences/AI/Skills to review and allow them.";

                Debug.Log($"[AI Assistant] {message}");
            }
        }

        static void RebuildAllowedSkillsCache(IReadOnlyList<SkillDefinition> skills = null)
        {
            skills ??= SkillsRegistry.GetAllSkillsNoWait();
            var newCache = new HashSet<string>(System.StringComparer.Ordinal);
            foreach (var skill in skills)
            {
                if (!SkillRegistryTags.IsInternalOrBuiltIn(skill.Tags) && GetSkillAllowed(skill))
                    newCache.Add(CompositeKey(skill));
            }
            s_AllowedSkillNamesCache = newCache;
            SkillsRegistry.InvalidateCache();
        }

        public static bool GetSkillAllowed(SkillDefinition skill)
            => EditorPrefs.GetBool(k_SkillAllowedPrefix + CompositeKey(skill), false);

        public static void SetSkillAllowed(SkillDefinition skill, bool allowed)
        {
            if (GetSkillAllowed(skill) == allowed)
                return;

            EditorPrefs.SetBool(k_SkillAllowedPrefix + CompositeKey(skill), allowed);

            // Copy-on-write update so in-flight filter reads remain safe.
            var compositeKey = CompositeKey(skill);
            var current = s_AllowedSkillNamesCache;
            var updated = current != null
                ? new HashSet<string>(current, System.StringComparer.Ordinal)
                : new HashSet<string>(System.StringComparer.Ordinal);

            if (allowed)
                updated.Add(compositeKey);
            else
                updated.Remove(compositeKey);

            s_AllowedSkillNamesCache = updated;

            SkillsRegistry.InvalidateCache();
            SkillAllowedStateChanged?.Invoke();
        }

        static bool GetSkillSeen(string compositeKey) => EditorPrefs.GetBool(k_SkillSeenPrefix + compositeKey, false);
        static void MarkSkillSeen(string compositeKey) => EditorPrefs.SetBool(k_SkillSeenPrefix + compositeKey, true);

        // Forgets skills no longer present so they are rediscovered as new if re-added.
        static void CleanupRemovedSkills(IReadOnlyList<SkillDefinition> currentSkills)
        {
            var previous = LoadCurrentSkillKeys();

            var current = new HashSet<string>(System.StringComparer.Ordinal);
            foreach (var skill in currentSkills)
            {
                if (!SkillRegistryTags.IsInternalOrBuiltIn(skill.Tags))
                    current.Add(CompositeKey(skill));
            }

            var anyRemoved = false;
            foreach (var key in previous)
            {
                if (current.Contains(key))
                    continue;
                EditorPrefs.DeleteKey(k_SkillSeenPrefix + key);
                EditorPrefs.DeleteKey(k_SkillAllowedPrefix + key);
                anyRemoved = true;
            }

            if (!current.SetEquals(previous))
                EditorPrefs.SetString(k_CurrentSkillKeysKey, string.Join("\n", current));
            if (anyRemoved)
                RebuildAllowedSkillsCache(currentSkills);
        }

        static HashSet<string> LoadCurrentSkillKeys()
        {
            var raw = EditorPrefs.GetString(k_CurrentSkillKeysKey, "");
            return string.IsNullOrEmpty(raw)
                ? new HashSet<string>(System.StringComparer.Ordinal)
                : new HashSet<string>(raw.Split('\n'), System.StringComparer.Ordinal);
        }

        static string CompositeKey(SkillDefinition skill)
            => skill.MetaData.Name + ":" + NormalizePath(skill.Path);

        static string NormalizePath(string path)
            => string.IsNullOrEmpty(path) ? "" : Path.GetFullPath(path).ToLowerInvariant().Replace('\\', '/');
    }
}
