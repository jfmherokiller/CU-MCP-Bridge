# CU-MCP-Bridge

BepInEx mod + Python MCP server — lets AI assistants control [Casualties Unknown](https://store.steampowered.com/app/Casualties_Unknown) gameplay in real time.

## What it does

The AI can read player state, move around the map, pick up and use items, heal wounds, query terrain, and even clone an AI companion to act alongside the player. Everything is driven by natural language through any MCP-compatible AI client (e.g. [opencode](https://opencode.ai)).

## Requirements

- Windows 10/11
- [Casualties Unknown](https://store.steampowered.com/app/Casualties_Unknown) (Steam)
- Python 3.10+
- .NET SDK (for building the mod)
- [BepInEx](https://docs.bepinex.dev/articles/user_guide/installation/index.html) installed in the game

## Quick start

### 1. Install the mod

Copy the two DLLs from `mod/` into your game's `BepInEx/plugins/` folder:

```
<game directory>/BepInEx/plugins/CU-MCP-Mod.dll
<game directory>/BepInEx/plugins/Newtonsoft.Json.dll
```

### 2. Install Python dependencies

```bash
pip install -r requirements.txt
```

### 3. Configure your AI client

Add to your MCP configuration (e.g. `opencode.json`):

```json
{
  "mcp": {
    "servers": {
      "cu-mcp-bridge": {
        "type": "stdio",
        "command": "python",
        "args": ["src/bridge_server/server.py"],
        "cwd": "/path/to/CU-MCP-Bridge"
      }
    }
  }
}
```

### 4. Play

1. Start the game, enter a level
2. Start the Python server: `python src/bridge_server/server.py`
3. Ask your AI assistant to control the player

> Order matters: the server must start *after* the game is running.

## MCP Tools

| Tool | Description |
|---|---|
| `get_game_state` | Player state (position, health, inventory) + environment snapshot |
| `get_map_info` | Terrain and entity info around the player |
| `get_nearby_items` | List dropped items near the player |
| `query_position` | Query entities/terrain at a specific world coordinate |
| `search_blocks` | Search terrain by material name (e.g. "sand", "rock") |
| `move_to` | Move the active player to a world position |
| `move_to_player` | Move the AI companion to the human player |
| `follow` | Make the AI companion follow the human |
| `jump` | Jump (supports horizontal direction) |
| `use_item` | Use an inventory item |
| `pick_up_item` | Pick up a nearby item (by name or nearest) |
| `drop_item` | Drop an inventory item |
| `sleep` | Rest to recover energy |
| `heal_ai` | Heal a specific limb on the AI companion |
| `create_ai_player` | Clone an AI companion next to the human |
| `destroy_ai_player` | Remove the AI companion |
| `set_contingency` | Set condition-action rules (e.g. "heal if HP < 30%") |
| `update_contingency` | Update contingency rules at runtime |
| `user_interact` | Highest-priority human intervention |

## Building from source

```bash
# Build the C# mod
dotnet build BepInEx/CU-MCP-Mod.csproj -c Release

# Deploy to game (kills the game process, rebuilds, copies DLL)
.\deploy.ps1 -Action deploy
```

Output: `BepInEx/bin/Release/net472/CU-MCP-Mod.dll`

## Project structure

```
CU-MCP-Bridge/
├── BepInEx/                # C# mod source (BepInEx plugin)
│   ├── Executor/           # Order execution, pathfinding, movement
│   ├── Collector/          # Game state collection (player, environment)
│   ├── Contingency/        # Local condition-action rules
│   ├── Pipe/               # Named pipe client + protocol
│   ├── AIPlayerManager.cs  # AI companion creation/destruction
│   ├── BridgePlugin.cs     # Entry point, tick loop
│   └── DebugGUI.cs         # F6 debug panel
├── src/bridge_server/      # Python MCP server
│   ├── server.py           # MCP tool definitions
│   ├── state_manager.py    # Pipe reader, state cache
│   ├── order_manager.py    # Order queue
│   ├── pipe_server.py      # Named pipe server (win32pipe)
│   └── contingency.py      # Contingency rule manager
├── tests/                  # Unit & integration tests
├── deploy.ps1              # Build + deploy script
└── requirements.txt        # Python dependencies
```

## Communication

```
AI Client (opencode)  ←→  Python MCP Server  ←→  Named Pipe  ←→  C# BepInEx Mod  ←→  Unity Game
```

- **AI → Game**: Orders are sent over the named pipe and executed on Unity's main thread
- **Game → AI**: Player state and query results flow back over the same pipe
- **Blocking**: Each command blocks until the mod reports completion (success/failure/timeout)

## Known issues

- **Skin mod compatibility**: Installing third-party skin mods (e.g. "Skin Sync") may cause the AI companion to display a duplicate tail that mirrors the human player's tail. This is a skin mod compatibility issue and does not occur in the vanilla game.

## License

MIT
