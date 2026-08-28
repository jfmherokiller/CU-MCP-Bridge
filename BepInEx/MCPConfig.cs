using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace CUMCP
{
    public class MCPConfig
    {
        public bool ai_player_enabled = false;
        public float ai_player_offset_x = 2.0f;

        private static MCPConfig _instance;

        public static MCPConfig Instance => _instance ?? (_instance = Load());

        private static string GetConfigPath()
        {
            var dir = Path.GetDirectoryName(Application.dataPath);
            return Path.Combine(dir ?? ".", "cu_mcp_config.json");
        }

        public static MCPConfig Load()
        {
            try
            {
                var path = GetConfigPath();
                if (File.Exists(path))
                {
                    var json = File.ReadAllText(path);
                    return JsonConvert.DeserializeObject<MCPConfig>(json) ?? new MCPConfig();
                }
            }
            catch (System.Exception e)
            {
                BridgePlugin.Log?.LogWarning($"[CU-MCP] Config load: {e.Message}");
            }
            return new MCPConfig();
        }
    }
}