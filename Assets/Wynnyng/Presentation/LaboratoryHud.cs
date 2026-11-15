using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using Wynnyng.Configuration;
using Wynnyng.Core;
using Wynnyng.Input;

namespace Wynnyng.Presentation
{
    /// <summary>Diagnostic tools, not the final match HUD. Objects and callbacks are created on menu changes only.</summary>
    public sealed class LaboratoryHud : MonoBehaviour
    {
        private LocalInputHub inputs;
        private ILaboratoryControls lab;
        private PresentationConfig config;
        private TMP_FontAsset font;
        private TextMeshProUGUI telemetry, scenario, status, rebindTitle;
        private RectTransform rebindPanel, rows;
        private InputSystemUIInputModule uiModule;
        private EventSystem eventSystem;
        private CanvasGroup laboratoryGroup;
        private Button laboratoryFocus, menuFocus;
        private float nextRefresh;
        private int selectedPlayer;
        private string previousStatus, previousScenario, previousDevice;
        private bool refreshBindings;
        public bool IsInitialized { get; private set; }
        public bool RebindVisible => IsInitialized && rebindPanel != null && rebindPanel.gameObject.activeSelf;

        private void Awake() { if (!IsInitialized) enabled = false; }

        public void Initialize(LocalInputHub hub, ILaboratoryControls controls, PresentationConfig presentation,
            TMP_FontAsset defaultFont)
        {
            if (IsInitialized) throw new System.InvalidOperationException("HUD is already initialized.");
            if (defaultFont == null) throw new System.ArgumentNullException(nameof(defaultFont), TmpResources.MissingResourcesMessage);
            if (hub == null) throw new System.ArgumentNullException(nameof(hub));
            if (controls == null) throw new System.ArgumentNullException(nameof(controls));
            if (presentation == null) throw new System.ArgumentNullException(nameof(presentation));
            inputs = hub; lab = controls; config = presentation;
            font = defaultFont;
            Canvas canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = config.ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;
            gameObject.AddComponent<GraphicRaycaster>();
            var events = new GameObject("Lab EventSystem");
            events.transform.SetParent(transform, false);
            eventSystem = events.AddComponent<EventSystem>();
            uiModule = events.AddComponent<InputSystemUIInputModule>();
            uiModule.AssignDefaultActions();

            float pad = config.PanelPadding, h = config.ButtonHeight;
            RectTransform top = Panel("Telemetry", transform, new Vector2(0f, 1f), new Vector2(pad, -pad),
                new Vector2(config.HudWidth, h * 4f + pad * 2f));
            Label(top, "WYNNYNG  /  FASES 0 + 1A", new Vector2(pad, -pad), config.HudWidth - pad * 2f, h, config.Accent);
            telemetry = Label(top, "", new Vector2(pad, -pad - h), config.HudWidth - pad * 2f, h * 2f, Color.white);
            // Reserve TMP's number/mesh buffers before regular updates.
            telemetry.SetText("Tick {0:0}  |  Bola {1:1} m/s\nGols +Z {2:0}  -Z {3:0}  |  Fora {4:0}", 99999999f, 999f, 9999f, 9999f, 9999f);
            telemetry.ForceMeshUpdate();
            status = Label(top, "", new Vector2(pad, -pad - h * 3f), config.HudWidth - pad * 2f, h, config.Accent);

            RectTransform bottom = Panel("Laboratory", transform, Vector2.zero, new Vector2(pad, pad),
                new Vector2(config.HudWidth, h * 5f + pad * 2f));
            bottom.pivot = Vector2.zero;
            laboratoryGroup = bottom.gameObject.AddComponent<CanvasGroup>();
            Label(bottom, "BANCADA DE FISICA", new Vector2(pad, -pad), config.HudWidth - pad * 2f, h, config.Accent);
            scenario = Label(bottom, "", new Vector2(pad, -pad - h), config.HudWidth - pad * 2f, h, Color.white);
            float third = (config.HudWidth - pad * 2f) / 3f;
            ButtonAt(bottom, "< Anterior", new Vector2(pad, -pad - h * 2f), third, () => lab.SelectScenario(-1));
            ButtonAt(bottom, "Lancar bola", new Vector2(pad + third, -pad - h * 2f), third, lab.RequestLaunch);
            ButtonAt(bottom, "Proximo >", new Vector2(pad + third * 2f, -pad - h * 2f), third, () => lab.SelectScenario(1));
            ButtonAt(bottom, "Repor bola", new Vector2(pad, -pad - h * 3f), third, lab.RequestReset);
            laboratoryFocus = ButtonAt(bottom, "Controles", new Vector2(pad + third, -pad - h * 3f), third, ToggleBindings);
            ButtonAt(bottom, "Sair", new Vector2(pad + third * 2f, -pad - h * 3f), third, lab.RequestExit);
            Label(bottom, "D-pad: caso  |  RB: lancar  |  Select: repor\nTeclado: setas, Enter, R  |  Start/F1: controles",
                new Vector2(pad, -pad - h * 4f), config.HudWidth - pad * 2f, h, Color.white, config.FontSize * 0.72f);
            BuildRebindPanel();
            eventSystem.firstSelectedGameObject = laboratoryFocus.gameObject;
            eventSystem.SetSelectedGameObject(laboratoryFocus.gameObject);
            UpdateLabels();
            IsInitialized = true;
            enabled = true;
        }

        public void ToggleBindings()
        {
            if (!IsInitialized) return;
            bool show = !RebindVisible;
            inputs.CancelRebind();
            rebindPanel.gameObject.SetActive(show);
            laboratoryGroup.interactable = !show;
            laboratoryGroup.blocksRaycasts = !show;
            if (show) RefreshBindings();
            eventSystem.SetSelectedGameObject(show ? menuFocus.gameObject : laboratoryFocus.gameObject);
        }
        private void BuildRebindPanel()
        {
            float pad = config.PanelPadding, h = config.ButtonHeight, width = config.RebindPanelWidth;
            rebindPanel = Panel("Rebind Panel", transform, Vector2.one, new Vector2(-pad, -pad),
                new Vector2(width, config.ReferenceResolution.y - pad * 2f));
            rebindPanel.pivot = Vector2.one;
            rebindTitle = Label(rebindPanel, "CONTROLES", new Vector2(pad, -pad), width - pad * 2f, h * 2f,
                config.Accent, config.FontSize * 0.8f);
            menuFocus = ButtonAt(rebindPanel, "Jogador 1", new Vector2(pad, -pad - h * 2f), (width - pad * 2f) / 4f,
                () => SelectPlayer(0));
            ButtonAt(rebindPanel, "Jogador 2", new Vector2(pad + (width - pad * 2f) / 4f, -pad - h * 2f), (width - pad * 2f) / 4f,
                () => SelectPlayer(1));
            ButtonAt(rebindPanel, "Padrao", new Vector2(pad + (width - pad * 2f) / 2f, -pad - h * 2f), (width - pad * 2f) / 4f,
                () => { inputs.ResetBindings(selectedPlayer); RefreshBindings(); });
            ButtonAt(rebindPanel, "Fechar", new Vector2(pad + (width - pad * 2f) * 0.75f, -pad - h * 2f),
                (width - pad * 2f) / 4f, ToggleBindings);
            RectTransform viewport = Rect("Viewport", rebindPanel, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            viewport.anchorMin = Vector2.zero; viewport.anchorMax = Vector2.one;
            viewport.offsetMin = new Vector2(pad, pad); viewport.offsetMax = new Vector2(-pad, -pad - h * 3f);
            viewport.gameObject.AddComponent<Image>().color = new Color(0.025f, 0.04f, 0.05f, 0.98f);
            viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            rows = Rect("Rows", viewport, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(width - pad * 2f, 0f));
            var layout = rows.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childControlWidth = true; layout.childControlHeight = false;
            layout.childForceExpandHeight = false; layout.childForceExpandWidth = true;
            layout.spacing = config.FontSize * 0.15f;
            var fitter = rows.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.content = rows; scroll.viewport = viewport; scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            rebindPanel.gameObject.SetActive(false);
        }
        private void SelectPlayer(int player)
        {
            inputs.CancelRebind(); selectedPlayer = player; RefreshBindings();
        }
        private void RefreshBindings()
        {
            refreshBindings = false;
            eventSystem.SetSelectedGameObject(menuFocus.gameObject);
            for (int i = rows.childCount - 1; i >= 0; i--)
            {
                GameObject child = rows.GetChild(i).gameObject;
                child.SetActive(false); Destroy(child);
            }
            string group = inputs.UsesKeyboard(selectedPlayer) ? "Keyboard" : "Gamepad";
            previousDevice = inputs.DeviceLabel(selectedPlayer);
            rebindTitle.text = "JOGADOR " + (selectedPlayer + 1) + "  /  " + previousDevice + "\nClique no comando e pressione o novo controle. Esc cancela.";
            InputActionAsset asset = inputs.Actions(selectedPlayer);
            for (int m = 0; m < asset.actionMaps.Count; m++)
            {
                var map = asset.actionMaps[m];
                for (int a = 0; a < map.actions.Count; a++)
                {
                    InputAction action = map.actions[a];
                    for (int b = 0; b < action.bindings.Count; b++)
                    {
                        InputBinding binding = action.bindings[b];
                        if (binding.isComposite || !binding.groups.Contains(group)) continue;
                        int index = b;
                        string name = action.name + (binding.isPartOfComposite ? " / " + binding.name : "");
                        Button button = ButtonAt(rows, name + " : " + action.GetBindingDisplayString(b), Vector2.zero,
                            config.RebindPanelWidth - config.PanelPadding * 2f,
                            () => StartRebind(action, index));
                        button.interactable = inputs.DeviceLabel(selectedPlayer) != "Sem controle conectado";
                        var element = button.gameObject.AddComponent<LayoutElement>();
                        element.preferredHeight = config.ButtonHeight;
                    }
                }
            }
        }
        private void StartRebind(InputAction action, int binding)
        {
            rebindTitle.text = "AGUARDANDO NOVO CONTROLE...\nEsc cancela. O remapeamento e salvo neste PC.";
            inputs.Rebind(selectedPlayer, action, binding, OnBindingsChanged);
        }
        private void OnBindingsChanged() { refreshBindings = true; }
        private void Update()
        {
            if (!IsInitialized) return;
            uiModule.enabled = !inputs.IsRebinding;
            if (refreshBindings && RebindVisible) RefreshBindings();
            if (RebindVisible && previousDevice != inputs.DeviceLabel(selectedPlayer)) RefreshBindings();
            nextRefresh -= Time.unscaledDeltaTime;
            if (nextRefresh > 0f) return;
            nextRefresh = config.StatusInterval;
            UpdateLabels();
        }
        private void UpdateLabels()
        {
            telemetry.SetText("Tick {0:0}  |  Bola {1:1} m/s\nGols +Z {2:0}  -Z {3:0}  |  Fora {4:0}",
                lab.Tick, lab.BallSpeed, lab.PositiveGoals, lab.NegativeGoals, lab.BallsOut);
            if (previousStatus != lab.Status) { previousStatus = lab.Status; status.text = previousStatus; }
            if (previousScenario != lab.ScenarioName) { previousScenario = lab.ScenarioName; scenario.text = previousScenario; }
        }
        private TextMeshProUGUI Label(Transform parent, string value, Vector2 position, float width, float height,
            Color color, float size = 0f)
        {
            RectTransform rect = Rect("Label", parent, new Vector2(0f, 1f), new Vector2(0f, 1f), position, new Vector2(width, height));
            rect.gameObject.SetActive(false);
            TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = font; text.fontSize = size > 0f ? size : config.FontSize;
            text.color = color; text.raycastTarget = false;
            text.text = value;
            rect.gameObject.SetActive(true);
            return text;
        }
        private Button ButtonAt(Transform parent, string text, Vector2 position, float width, UnityEngine.Events.UnityAction action)
        {
            RectTransform rect = Rect("Button " + text, parent, new Vector2(0f, 1f), new Vector2(0f, 1f), position,
                new Vector2(width, config.ButtonHeight));
            Image image = rect.gameObject.AddComponent<Image>(); image.color = new Color(0.14f, 0.24f, 0.23f);
            Button button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            button.onClick.AddListener(action);
            TextMeshProUGUI label = Label(rect, text, Vector2.zero, width, config.ButtonHeight, Color.white, config.FontSize * 0.75f);
            label.alignment = TextAlignmentOptions.Center;
            return button;
        }
        private RectTransform Panel(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
        {
            RectTransform panel = Rect(name, parent, anchor, new Vector2(0f, 1f), position, size);
            panel.gameObject.AddComponent<Image>().color = new Color(0.025f, 0.045f, 0.055f, 0.94f);
            return panel;
        }
        private static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            var obj = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)obj.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = anchor; rect.anchorMax = anchor; rect.pivot = pivot;
            rect.anchoredPosition = position; rect.sizeDelta = size;
            return rect;
        }
    }
}
