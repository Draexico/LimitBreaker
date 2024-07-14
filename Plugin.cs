using System;
using System.Collections.Generic;
using System.IO;
using System.Media;
using System.Threading.Tasks;
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Game.ClientState.Objects.Types;

namespace LimitBreaker
{
    public sealed class Plugin : IDalamudPlugin
    {
        [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
        [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
        [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;
        [PluginService] internal static IClientState ClientState { get; private set; } = null!;
        [PluginService] internal static IObjectTable ObjectTable { get; private set; } = null!;
        [PluginService] internal static IFramework Framework { get; private set; } = null!;
        [PluginService] internal static IDataManager DataManager { get; private set; } = null!;

        private const string CommandName = "/lb";
        private bool isMonitoring;
        private uint previousActionId;

        private readonly HashSet<uint> limitBreakList = new()
        {
            206, 207, 208, 24859, 4248, 4247,  // Healers
            200, 201, 202, 4242, 7861, 24858, 4243,  // Melees
            203, 204, 205, 7862, 4246, 34867,  // Casters
            197, 198, 199, 4240, 17105, 4241 // Tanks
        };

        private readonly List<string> soundFiles = new List<string>
        {
            "darkness.wav",
            "doit.wav"
        };

        public Plugin()
        {
            isMonitoring = false;
            previousActionId = 0;

            CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
            {
                HelpMessage = "Monitors specific actions."
            });
        }

        public void Dispose()
        {
            CommandManager.RemoveHandler(CommandName);
            StopMonitoring();
        }

        private void OnCommand(string command, string args)
        {
            isMonitoring = !isMonitoring;

            if (isMonitoring)
            {
                StartMonitoring();
                ChatGui.Print("Action monitoring enabled.");
            }
            else
            {
                StopMonitoring();
                ChatGui.Print("Action monitoring disabled.");
            }
        }

        private void StartMonitoring()
        {
            Framework.Update += OnUpdate;
        }

        private void StopMonitoring()
        {
            Framework.Update -= OnUpdate;
        }

        private void OnUpdate(IFramework framework)
        {
            var player = ClientState.LocalPlayer as IBattleChara;
            if (player == null) return;

            var currentActionId = player.CastActionId;
            if (currentActionId != previousActionId && IsValidAction(currentActionId))
            {
                PlayRandomSoundAsync();
                previousActionId = currentActionId;
            }
            else if (!player.IsCasting && previousActionId != 0)
            {
                previousActionId = 0;
            }
        }

        private bool IsValidAction(uint actionId)
        {
            return limitBreakList.Contains(actionId);
        }

        private async void PlayRandomSoundAsync()
        {
            try
            {
                var random = new Random();
                var selectedSound = soundFiles[random.Next(soundFiles.Count)];
                var filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), $@"XIVLauncher\installedPlugins\LimitBreaker\{selectedSound}");

                if (!File.Exists(filePath))
                {
                    ChatGui.PrintError($"Error: The file located at {filePath} does not exist.");
                    return;
                }

                using (var player = new SoundPlayer(filePath))
                {
                    player.Load();
                    await Task.Run(() => player.PlaySync());
                }
            }
            catch (InvalidOperationException ex)
            {
                ChatGui.PrintError($"Error playing sound: {ex.Message}");
            }
            catch (Exception ex)
            {
                ChatGui.PrintError($"Unexpected error: {ex.Message}");
            }
        }

        public string Name => "Limit Breaker Plugin";
    }
}
