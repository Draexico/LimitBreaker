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
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel.GeneratedSheets;
using Dalamud.Game.ClientState.Party;
using Microsoft.VisualBasic;

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
        [PluginService] internal static IDutyState DutyState { get; private set; } = null!;
        [PluginService] internal static IPartyList PartyList { get; private set; } = null!;

        private const string CommandName = "/lb";
        private bool isMonitoring;
        private ushort newCurrentUnits; 
        private string version = "1.3.0.1";

        private readonly List<string> soundFiles = new List<string>
        {
            "darkness.wav",
            "doit.wav"
        };

        public Plugin()
        {
            isMonitoring = false;
            newCurrentUnits = 0;
            CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
            {
                HelpMessage = "Starts LimitBreaker"
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
                ChatGui.Print("LimitBreaker Activated");
            }
            else
            {
                StopMonitoring();
                ChatGui.Print("LimitBreaker Deactivated");
            }
        }

        // Start monitoring if a duty has been started
        private void StartMonitoring()
        {
            DutyState.DutyStarted += OnDutyStarted;
            DutyState.DutyCompleted += OnDutyCompleted;
        }
        // Stop monitoring everything
        private void StopMonitoring()
        {
            DutyState.DutyStarted -= OnDutyStarted;
            DutyState.DutyCompleted -= OnDutyCompleted;
            DutyState.DutyWiped -= OnDutyWiped;
            Framework.Update -= OnUpdate;
        }

        // New duty started. Start monitoring wipes
        private void OnDutyStarted(object? sender, ushort dutyId)
        {
            ChatGui.Print("Duty Started");
            DutyState.DutyWiped += OnDutyWiped;
            var partyList = PartyList.Length;
            if (partyList > 0) {
                Framework.Update -= OnUpdate;
                Framework.Update += OnUpdate;
            } 
            newCurrentUnits = 0; // Resets units on new duty
        }
        private void OnDutyCompleted(object? sender, ushort dutyId) {
            ChatGui.Print("Duty Complete. Stopping monitoring.");
            Framework.Update -= OnUpdate;
        }
        private void OnDutyWiped(object? sender, ushort dutyId) {
            // ChatGui.Print("Duty wiped");
            newCurrentUnits = 0; // Resets units on duty wipe
        }
        private void OnUpdate(IFramework framework)
        {
            unsafe
            {
                LimitBreakController* lbController = LimitBreakController.Instance();
                if (lbController != null)
                {
                    var barCount = lbController->BarCount;
                    var currentUnits = lbController->CurrentUnits;
                    var barUnits = lbController->BarUnits;

                    if (currentUnits < newCurrentUnits) {
                        PlayRandomSoundAsync();
                    }
                    newCurrentUnits = currentUnits;
                }
                else
                {
                    ChatGui.Print("Could not retrieve Limit Break Controller instance.");
                }
            }
        }

        private async void PlayRandomSoundAsync()
        {
            try
            {
                var random = new Random();
                var selectedSound = soundFiles[random.Next(soundFiles.Count)];
                var filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), $@"XIVLauncher\installedPlugins\LimitBreaker\{version}\{selectedSound}");
                if (!File.Exists(filePath))
                {
                    ChatGui.PrintError($"Error: The file located at {filePath} does not exist.");
                    return;
                }

                using (var player = new SoundPlayer(filePath))
                {
                    ChatGui.Print($"Playing: {selectedSound}");
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
