using BepInEx;
using BepInEx.Logging;
using Mirror;
using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GWYF.AutoPhone
{
    [BepInProcess("Gamble With Your Friends.exe")]
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        internal static new ManualLogSource Logger;
        
        private void Awake()
        {
            // Plugin startup logic
            Logger = base.Logger;

            SceneManager.sceneLoaded += OnSceneLoaded;

            Logger.LogInfo($"[AutoPhone] Plugin is loaded!");
        }
        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Logger.LogInfo($"[AutoPhone] Scene loaded: {scene.name}. Will wait for clients before triggering PhoneBooths.");
            // Wait a frame before searching — Mirror may not have finished
            // spawning networked objects at the exact moment sceneLoaded fires.
            StartCoroutine(WaitForClientsAndTrigger());
        }

        private IEnumerator WaitForClientsAndTrigger()
        {
            // Only the server should trigger this
            if (!NetworkServer.active)
            {
                Logger.LogInfo("[AutoPhone] Not the server – skipping.");
                yield break;
            }

            Logger.LogInfo("[AutoPhone] Waiting for at least one ready client...");

            // Poll until at least one client is ready, with a timeout so we
            // don't loop forever in solo/offline modes.
            float timeout = 30f;
            float elapsed = 0f;

            while (elapsed < timeout)
            {
                // NetworkServer.connections includes all connected clients.
                // A connection is "ready" once it has sent the ReadyMessage to
                // the server (i.e. it has finished loading the scene).
                bool anyReady = false;
                foreach (var conn in NetworkServer.connections.Values)
                {
                    if (conn != null && conn.isReady)
                    {
                        anyReady = true;
                        break;
                    }
                }

                if (anyReady)
                    break;

                elapsed += Time.deltaTime;
                yield return null; // check again next frame
            }

            if (elapsed >= timeout)
                Logger.LogWarning("[AutoPhone] Timed out waiting for a ready client. Triggering anyway.");
            else
                Logger.LogInfo($"[AutoPhone] Client ready after ~{elapsed:F1}s. Triggering PhoneBooths.");

            // Extra small buffer so the client RPC subscription is fully settled
            yield return new WaitForSeconds(0.5f);

            TriggerAllPhoneBooths();
        }

        private void TriggerAllPhoneBooths()
        {
            PhoneBooth[] booths = FindObjectsByType<PhoneBooth>(FindObjectsSortMode.None);

            if (booths.Length == 0)
            {
                Logger.LogInfo("[AutoPhone] No PhoneBooths found in scene.");
                return;
            }

            MethodInfo method = typeof(PhoneBooth).GetMethod(
                "ServerFirstInteraction",
                BindingFlags.NonPublic | BindingFlags.Instance
            );

            if (method == null)
            {
                Logger.LogError("[AutoPhone] Could not find ServerFirstInteraction. Dumping PhoneBooth methods:");
                foreach (MethodInfo m in typeof(PhoneBooth).GetMethods(
                    BindingFlags.Public | BindingFlags.NonPublic |
                    BindingFlags.Instance | BindingFlags.Static))
                {
                    Logger.LogInfo($"  [{m.DeclaringType?.Name}] {m.Name}");
                }
                return;
            }

            Logger.LogInfo($"[AutoPhone] Triggering ServerFirstInteraction on {booths.Length} PhoneBooth(s).");
            foreach (PhoneBooth booth in booths)
            {
                if (booth == null) continue;
                Logger.LogInfo($"[AutoPhone] Triggering: {booth.gameObject.name}");
                method.Invoke(booth, null);
            }
        }
    }

    public static class MyPluginInfo
    {
        public const string PLUGIN_GUID = "GWYF.Epoch.AutoPhone";
        public const string PLUGIN_NAME = "Auto Phone!";
        public const string PLUGIN_VERSION = "1.0.0";
    }
}
