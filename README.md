The Source is only for the mods UI

Mod itself can be found in [Releases](https://github.com/sibercat/AiAdminUi/releases)
![alt text](https://raw.githubusercontent.com/sibercat/AiAdminUi/refs/heads/main/uiScreen.png)

# IsleServerMod

A C++ DLL injection mod for The Isle dedicated servers (Unreal Engine 5.6) that adds advanced server management features including AI spawning, chat commands, fish respawning, and a Hunger Corpse system.

## Features

### Core Systems
- **AI Spawning System** - Automated and manual dinosaur spawning with rule-based limits
- **WPF Admin UI** - Modern Windows desktop interface for server management
- **Chat Commands** - In-game admin commands for spawning and server control
- **Fish Respawning** - Automated fish school respawning with distance checks
- **Hunger Corpse System** - Feature that spawns corpses when players get hungry, Carnivore-only filter option

### Configuration
All features are controlled via a Lua configuration file with hot-reload support:
- Enable/disable individual features
- Set spawn intervals and distances
- Configure debug logging levels
- Define spawn rules per species
- Adjust performance parameters


### Auto-Reload
The config file is automatically reloaded every 60 seconds, allowing changes without restarting the server.

## Usage

/*
 * The Isle Server Mod - Admin-Only Command System
 *
 * COMMAND METHODS:
 * --------------------------------------------------
 * 1. IN-GAME CHAT COMMANDS:
 *    - Type /ai <command> in chat
 *    - Requires admin authorization (checked against AdminsSteamIDs)
 *    - Enable with: chat_commands_enabled = true (default: false)
 *
 * 2. COMMAND FILE (IsleServerMod_commands.txt):
 *    - Write commands to file for server owner access
 *    - No admin check (server owner has direct file access)
 *    - File is auto-cleared after processing
 *
 * AVAILABLE COMMANDS:
 * --------------------------------------------------
 * /ai start                                    - Enable auto spawn
 * /ai stop                                     - Disable auto spawn
 * /ai spawn_for <player> <species> [count]     - Spawn near specific player (by name or ID)
 * /ai spawn_corpse_for <player> <species> [count] - Spawn dead dino near specific player
 * /ai spawn_random <species> [count]           - Spawn near random player
 * /ai spawn_at <species> <x> <y> <z> [count]   - Spawn at coordinates
 * /ai despawn <species>                        - Despawn all of species
 * /ai despawn_all                              - Despawn all spawned dinos
 * /ai kill <species>                           - Kill all of species
 * /ai kill_all                                 - Kill all spawned dinos
 * /ai fish respawn                             - Manually respawn fish schools
 *
 * ADMIN AUTHORIZATION:
 * --------------------------------------------------
 * - Admins are loaded from TIGameStateBase->AdminsSteamIDs every 60 seconds
 * - Only players in the admin list in Game.ini can use /ai chat commands
 * - Non-admin attempts are logged with "Access denied" message
 */

## Security Note

This is a **server administration tool** for authorized use on your own dedicated servers. The DLL injection mechanism may be flagged by antivirus software, which is expected behavior for memory injection tools. Use only on servers you own or have explicit permission to modify.


## Contributing

This is a personal server mod project. If you encounter issues or have suggestions, please open an issue on GitHub.

## License
THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

This project is for educational and personal server administration purposes. Not affiliated with or endorsed by The Isle developers.
