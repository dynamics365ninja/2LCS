using LCS.Security;
using LCS.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Caching;
using Newtonsoft.Json;

namespace LCS.Cache
{
    class CredentialsCacheHelper
    {
        public static void AddCredentialsCache(string environmentId, Dictionary<string, string> credentialDict)
        {
            CacheUtil.Add(environmentId, credentialDict);
        }

        public static Dictionary<string, string> GetCredentialsCache(string environmentId)
        {
            return CacheUtil.Get<Dictionary<string, string>>(environmentId);
        }

        // Encrypted with DPAPI. The plain-text temp file of older versions (cachingStore) is migrated and deleted.
        private static string StorePath => Path.Combine(LocalProtection.DataFolder, "credentials.dat");

        public static void SaveCredentialsOffline()
        {
            var EnvironmentCredentialCache = new CredentialsStore();

            MemoryCache.Default.ToList().ForEach(c =>
            {
                EnvironmentCredentialCache.EnvironmentCredentials.Add(new EnvironmentCredentials
                {
                    EnvironmentId = c.Key,
                    Credentials = (Dictionary<string, string>)c.Value
                });
            });

            if (EnvironmentCredentialCache.EnvironmentCredentials.Count == 0)
            {
                DeleteStore();
                return;
            }
            LocalProtection.WriteProtectedFile(StorePath, JsonConvert.SerializeObject(EnvironmentCredentialCache));
            DeleteLegacyStore();
        }

        public static void LoadOffLineCredentials()
        {
            try
            {
                string cache = null;
                if (File.Exists(StorePath))
                {
                    cache = LocalProtection.ReadProtectedFile(StorePath);
                }
                else
                {
                    var legacyFile = Properties.Settings.Default.cachingStore;
                    if (!string.IsNullOrEmpty(legacyFile) && File.Exists(legacyFile))
                    {
                        cache = File.ReadAllText(legacyFile);
                    }
                }
                if (cache == null) return;

                var store = JsonConvert.DeserializeObject<CredentialsStore>(cache);
                store?.RebuildMemCache();

                if (!File.Exists(StorePath))
                {
                    // Migrate the plain text cache of an older version.
                    LocalProtection.WriteProtectedFile(StorePath, cache);
                }
                DeleteLegacyStore();
            }
            catch (Exception)
            {
                //deal with corrupted cache
                DisableCache();
                ClearCache();
            }
        }

        // Removes the stored cache (encrypted file and the plain-text file of older versions).
        public static void DeleteStore()
        {
            try
            {
                if (File.Exists(StorePath)) File.Delete(StorePath);
            }
            catch (IOException)
            {
                // Best effort.
            }
            catch (UnauthorizedAccessException)
            {
                // Best effort.
            }
            DeleteLegacyStore();
        }

        private static void DeleteLegacyStore()
        {
            var legacyFile = Properties.Settings.Default.cachingStore;
            if (string.IsNullOrEmpty(legacyFile)) return;
            try
            {
                // Temp file names are reused - only delete a file that really is a 2LCS cache.
                if (File.Exists(legacyFile) && File.ReadAllText(legacyFile).TrimStart().StartsWith("{\"EnvironmentCredentials\"", StringComparison.Ordinal))
                {
                    File.Delete(legacyFile);
                }
            }
            catch (IOException)
            {
                return;
            }
            catch (UnauthorizedAccessException)
            {
                return;
            }
            Properties.Settings.Default.cachingStore = "";
            Properties.Settings.Default.Save();
        }

        public static void DisableCache()
        {
            // No cached passwords on disk once caching is off.
            DeleteStore();
            Properties.Settings.Default.cachingEnabled = false;
            Properties.Settings.Default.keepCache = false;
            Properties.Settings.Default.cachingStore = "";
            Properties.Settings.Default.Save();
        }


        public static string ClearCache()
        {
            try
            {
                // Also the in-memory entries - they would be written back when 2LCS closes.
                CacheUtil.Clear();
                if (File.Exists(StorePath)) File.Delete(StorePath);
                DeleteLegacyStore();

                return null;
            }
            catch (Exception ex)
            {
                DisableCache();
                return ex.Message;
            }
        }

    }
}
