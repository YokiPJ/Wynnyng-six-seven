using System;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using Wynnyng.Core;

namespace Wynnyng.Input
{
    /// <summary>One action-asset copy per local player. Bindings and device filters are isolated.</summary>
    public sealed class LocalInputHub : IPlayerCommandSource, IDisposable
    {
        public const int PlayerCount = 2;
        private const string GameplayMap = "Gameplay";
        private const string LabMap = "Laboratory";
        private readonly Slot[] slots = new Slot[PlayerCount];
        private readonly IGameLog log;
        private InputActionRebindingExtensions.RebindingOperation rebind;
        private InputAction rebindAction;
        private Action changed;
        private int rebindSlot;
        public bool IsRebinding => rebind != null;

        private sealed class Slot
        {
            public InputActionAsset Asset;
            public InputDevice Device;
            public InputAction Move, Shoot, Pass, Through, Lob, Sprint, Switch, Pause;
            public InputAction Previous, Next, Launch, Reset, Bindings;
        }

        public LocalInputHub(InputActionAsset source, IGameLog log)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            for (int i = 0; i < PlayerCount; i++)
            {
                var slot = new Slot { Asset = UnityEngine.Object.Instantiate(source) };
                slots[i] = slot;
                InputActionMap map = slot.Asset.FindActionMap(GameplayMap, true);
                slot.Move = map.FindAction("Move", true);
                slot.Shoot = map.FindAction("Shoot", true);
                slot.Pass = map.FindAction("Pass", true);
                slot.Through = map.FindAction("Through", true);
                slot.Lob = map.FindAction("Lob", true);
                slot.Sprint = map.FindAction("Sprint", true);
                slot.Switch = map.FindAction("Switch", true);
                slot.Pause = map.FindAction("Pause", true);
                map = slot.Asset.FindActionMap(LabMap, true);
                slot.Previous = map.FindAction("Previous", true);
                slot.Next = map.FindAction("Next", true);
                slot.Launch = map.FindAction("Launch", true);
                slot.Reset = map.FindAction("Reset", true);
                slot.Bindings = map.FindAction("Bindings", true);
                LoadBindings(i);
            }
            AssignDevices();
            InputSystem.onDeviceChange += OnDeviceChange;
        }

        public InputActionAsset Actions(int player) => slots[player].Asset;
        public string DeviceLabel(int player) => slots[player].Device?.displayName ?? "Sem controle conectado";
        public bool UsesKeyboard(int player) => slots[player].Device is Keyboard;

        public PlayerCommand ReadPlayer(int player)
        {
            Slot slot = slots[player];
            if (slot.Device == null || IsRebinding) return default;
            Vector2 move = slot.Move.ReadValue<Vector2>();
            return new PlayerCommand
            {
                MoveX = move.x, MoveY = move.y, Sprint = slot.Sprint.ReadValue<float>(),
                ShootHeld = slot.Shoot.IsPressed(), ShootPressed = slot.Shoot.WasPressedThisFrame(),
                ShootReleased = slot.Shoot.WasReleasedThisFrame(), PassPressed = slot.Pass.WasPressedThisFrame(),
                ThroughPressed = slot.Through.WasPressedThisFrame(), LobPressed = slot.Lob.WasPressedThisFrame(),
                SwitchPressed = slot.Switch.WasPressedThisFrame(), PausePressed = slot.Pause.WasPressedThisFrame()
            };
        }

        public void ReadLaboratory(out int direction, out bool launch, out bool reset, out bool bindings)
        {
            Slot slot = slots[0];
            direction = 0; launch = false; reset = false; bindings = false;
            if (slot.Device == null || IsRebinding) return;
            direction = (slot.Next.WasPressedThisFrame() ? 1 : 0) - (slot.Previous.WasPressedThisFrame() ? 1 : 0);
            launch = slot.Launch.WasPressedThisFrame();
            reset = slot.Reset.WasPressedThisFrame();
            bindings = slot.Bindings.WasPressedThisFrame();
        }

        public void Rebind(int player, InputAction action, int bindingIndex, Action onChanged)
        {
            CancelRebind();
            if (slots[player].Device == null) return;
            rebindSlot = player;
            rebindAction = action;
            changed = onChanged;
            action.Disable();
            rebind = action.PerformInteractiveRebinding(bindingIndex)
                .WithCancelingThrough("<Keyboard>/escape")
                .WithControlsExcluding("<Mouse>")
                .OnPotentialMatch(OnPotentialRebindMatch)
                .OnComplete(OnRebindComplete)
                .OnCancel(OnRebindCancelled);
            rebind.Start();
        }

        public void CancelRebind() { rebind?.Cancel(); }
        public void ResetBindings(int player)
        {
            CancelRebind();
            slots[player].Asset.RemoveAllBindingOverrides();
            SaveBindings(player);
        }

        private void OnPotentialRebindMatch(InputActionRebindingExtensions.RebindingOperation operation)
        {
            InputControl control = operation.selectedControl;
            if (control == null) return;
            if (control.device == slots[rebindSlot].Device) operation.Complete();
            else operation.RemoveCandidate(control);
        }

        private void OnRebindComplete(InputActionRebindingExtensions.RebindingOperation operation)
        {
            FinishRebind(operation);
            SaveBindings(rebindSlot);
            changed?.Invoke();
            changed = null;
        }
        private void OnRebindCancelled(InputActionRebindingExtensions.RebindingOperation operation)
        {
            FinishRebind(operation);
            changed?.Invoke();
            changed = null;
        }
        private void FinishRebind(InputActionRebindingExtensions.RebindingOperation operation)
        {
            rebind = null;
            operation.Dispose();
            rebindAction.Enable();
            rebindAction = null;
        }

        private void OnDeviceChange(InputDevice device, InputDeviceChange change)
        {
            if (change != InputDeviceChange.Added && change != InputDeviceChange.Removed &&
                change != InputDeviceChange.Disconnected && change != InputDeviceChange.Reconnected &&
                change != InputDeviceChange.Enabled && change != InputDeviceChange.Disabled) return;
            CancelRebind();
            AssignDevices();
        }

        private void AssignDevices()
        {
            for (int i = 0; i < PlayerCount; i++)
            {
                Slot slot = slots[i];
                slot.Asset.Disable();
                if (slot.Device == null || !slot.Device.added || !slot.Device.enabled || slot.Device is Keyboard)
                    slot.Device = null;
            }
            for (int i = 0; i < Gamepad.all.Count; i++)
            {
                Gamepad pad = Gamepad.all[i];
                if (!pad.enabled) continue;
                bool used = false;
                for (int p = 0; p < PlayerCount; p++) if (slots[p].Device == pad) used = true;
                if (used) continue;
                for (int p = 0; p < PlayerCount; p++)
                {
                    if (slots[p].Device != null) continue;
                    slots[p].Device = pad;
                    break;
                }
            }
            if (slots[0].Device == null) slots[0].Device = Keyboard.current;
            for (int i = 0; i < PlayerCount; i++)
            {
                Slot slot = slots[i];
                slot.Asset.devices = slot.Device == null ? Array.Empty<InputDevice>() : new[] { slot.Device };
                if (slot.Device != null) slot.Asset.Enable();
            }
        }

        private static string BindingPath(int player) => Path.Combine(Application.persistentDataPath, "bindings-player-" + player + ".json");
        private void LoadBindings(int player)
        {
            try
            {
                string path = BindingPath(player);
                if (File.Exists(path)) slots[player].Asset.LoadBindingOverridesFromJson(File.ReadAllText(path));
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is ArgumentException)
            { log.Error("Could not load input bindings; using defaults. " + exception.Message); }
        }
        private void SaveBindings(int player)
        {
            try
            {
                Directory.CreateDirectory(Application.persistentDataPath);
                File.WriteAllText(BindingPath(player), slots[player].Asset.SaveBindingOverridesAsJson());
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            { log.Error("Could not save input bindings. " + exception.Message); }
        }
        public void Dispose()
        {
            changed = null;
            CancelRebind();
            InputSystem.onDeviceChange -= OnDeviceChange;
            for (int i = 0; i < PlayerCount; i++)
            {
                slots[i].Asset.Disable();
                UnityEngine.Object.Destroy(slots[i].Asset);
            }
        }
    }
}
