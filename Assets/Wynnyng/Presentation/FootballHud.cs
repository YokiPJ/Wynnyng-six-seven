using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using Wynnyng.Configuration;
using Wynnyng.Core;
using Wynnyng.Input;

namespace Wynnyng.Presentation
{
    /// <summary>Match HUD and paused settings. Runtime sliders edit the match copy, never a project asset.</summary>
    public sealed class FootballHud : MonoBehaviour
    {
        private IFootballControls controls;
        private LocalInputHub inputs;
        private FootballConfig config;
        private PresentationConfig presentation;
        private TMP_FontAsset font;
        private EventSystem eventSystem;
        private InputSystemUIInputModule uiModule;
        private RectTransform menu, menuContent, bindingPanel, bindingRows;
        private RectTransform staminaFill, shotFill;
        private TextMeshProUGUI score, clock, status, playerLabel, shotLabel, modeLabel;
        private TextMeshProUGUI homeStats, awayStats, bindingTitle;
        private Slider jogSlider, sprintSlider, passSlider, shotSlider, pressureSlider;
        private TextMeshProUGUI jogValue, sprintValue, passValue, shotValue, pressureValue;
        private Button resumeButton, bindingsBackButton;
        private string previousStatus, previousDevice, previousMode;
        private float refreshCountdown;
        private int bindingPlayer;
        private bool refreshBindings, initialized;
        public bool MenuVisible => initialized && menu.gameObject.activeSelf;
        public bool IsInitialized => initialized;

        private void Awake() { enabled = false; }

        public void Initialize(IFootballControls matchControls, LocalInputHub inputHub, FootballConfig footballConfig,
            PresentationConfig presentationConfig, TMP_FontAsset defaultFont)
        {
            if (initialized) throw new InvalidOperationException("Match HUD is already initialized.");
            controls = matchControls ?? throw new ArgumentNullException(nameof(matchControls));
            inputs = inputHub ?? throw new ArgumentNullException(nameof(inputHub));
            config = footballConfig != null ? footballConfig : throw new ArgumentNullException(nameof(footballConfig));
            presentation = presentationConfig != null ? presentationConfig : throw new ArgumentNullException(nameof(presentationConfig));
            font = defaultFont != null ? defaultFont : throw new ArgumentNullException(nameof(defaultFont));
            if (controls.Match == null) throw new ArgumentException("Create the match before its HUD.", nameof(matchControls));

            Canvas canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = presentation.ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;
            gameObject.AddComponent<GraphicRaycaster>();
            var events = new GameObject("Match EventSystem");
            events.transform.SetParent(transform, false);
            eventSystem = events.AddComponent<EventSystem>();
            uiModule = events.AddComponent<InputSystemUIInputModule>();
            uiModule.AssignDefaultActions();
            eventSystem.sendNavigationEvents = false;

            BuildLiveHud();
            BuildMenu();
            initialized = true;
            menu.gameObject.SetActive(false);
            eventSystem.SetSelectedGameObject(null);
            WarmLabels();
            RefreshMatchLabels();
            enabled = true;
        }

        private void BuildLiveHud()
        {
            float pad = presentation.PanelPadding, h = presentation.ButtonHeight;
            float width = presentation.HudWidth + pad * 8f;
            RectTransform board = Panel("Scoreboard", transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -pad), new Vector2(width, h * 2f));
            float third = width / 3f;
            Label(board, config.HomeName, new Vector2(pad, -pad * 0.3f), third - pad, h, config.HomeColor,
                presentation.FontSize, TextAlignmentOptions.Center);
            Label(board, config.AwayName, new Vector2(third * 2f, -pad * 0.3f), third - pad, h, config.AwayColor,
                presentation.FontSize, TextAlignmentOptions.Center);
            score = Label(board, "", new Vector2(third, -pad * 0.3f), third, h, Color.white,
                presentation.FontSize * 1.2f, TextAlignmentOptions.Center);
            clock = Label(board, "", new Vector2(third, -h), third, h, config.SelectedColor,
                presentation.FontSize * 0.85f, TextAlignmentOptions.Center);
            modeLabel = Label(board, "", new Vector2(pad, -h), third - pad, h, Color.white,
                presentation.FontSize * 0.65f, TextAlignmentOptions.Center);
            ButtonAt(board, "PAUSAR / MENU", new Vector2(third * 2f + pad, -h), third - pad * 2f, ToggleMenu);
            status = Label(transform, "", new Vector2(0f, 0f), width, h, config.SelectedColor,
                presentation.FontSize, TextAlignmentOptions.Center);
            RectTransform statusRect = (RectTransform)status.transform;
            statusRect.anchorMin = new Vector2(0.5f, 1f); statusRect.anchorMax = statusRect.anchorMin;
            statusRect.pivot = new Vector2(0.5f, 1f);
            statusRect.anchoredPosition = new Vector2(0f, -pad * 2f - h * 2f);

            float panelWidth = presentation.HudWidth * 0.75f;
            RectTransform player = Panel("Controlled Player", transform, Vector2.zero, Vector2.zero,
                new Vector2(pad, pad + h * 1.5f), new Vector2(panelWidth, h * 2f));
            playerLabel = Label(player, "", new Vector2(pad, -pad * 0.35f), panelWidth - pad * 2f, h,
                Color.white, presentation.FontSize * 0.85f);
            staminaFill = Bar(player, "Stamina", new Vector2(pad, -h * 1.35f), panelWidth - pad * 2f,
                h * 0.2f, config.SelectedColor);
            RectTransform power = Panel("Shot Power", transform, new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-pad, pad + h * 1.5f), new Vector2(panelWidth, h * 2f));
            shotLabel = Label(power, "", new Vector2(pad, -pad * 0.35f), panelWidth - pad * 2f, h,
                Color.white, presentation.FontSize * 0.85f);
            shotFill = Bar(power, "Power", new Vector2(pad, -h * 1.35f), panelWidth - pad * 2f,
                h * 0.2f, config.AwayColor);
            TextMeshProUGUI help = Label(transform,
                "WASD / setas: mover   J: chute (segure)   K: passe / pressao   I: profundidade   L: lancamento\n" +
                "Shift: correr   Tab: trocar   Esc: menu   |   Controle: Sul chute, Oeste passe, Norte profundidade, Leste alto, RT correr, LB trocar, Start menu",
                Vector2.zero, presentation.ReferenceResolution.x - pad * 2f, h * 1.5f, Color.white,
                presentation.FontSize * 0.68f, TextAlignmentOptions.Center);
            RectTransform helpRect = (RectTransform)help.transform;
            helpRect.anchorMin = Vector2.zero; helpRect.anchorMax = Vector2.zero; helpRect.pivot = Vector2.zero;
            helpRect.anchoredPosition = new Vector2(pad, pad * 0.25f);
        }

        private void BuildMenu()
        {
            float pad = presentation.PanelPadding, h = presentation.ButtonHeight;
            float width = presentation.RebindPanelWidth + pad * 8f;
            float height = presentation.ReferenceResolution.y - pad * 6f;
            menu = Panel("Paused Match Menu", transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(width, height));
            menu.GetComponent<Image>().color = new Color(0.025f, 0.045f, 0.055f, 0.99f);
            Label(menu, "WYNNYNG  /  MENU DA PARTIDA", new Vector2(pad, -pad), width - pad * 2f, h,
                config.SelectedColor, presentation.FontSize * 1.1f);
            Label(menu, "Dois clubes ficticios para teste. Os ajustes abaixo valem somente nesta sessao.",
                new Vector2(pad, -pad - h), width - pad * 2f, h, Color.white, presentation.FontSize * 0.72f);
            menuContent = Rect("Menu Content", menu, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(pad, -pad - h * 2f), new Vector2(width - pad * 2f, height - h * 2f - pad * 2f));
            float column = (width - pad * 3f) * 0.5f;
            resumeButton = ButtonAt(menuContent, "CONTINUAR", Vector2.zero, column, ToggleMenu);
            ButtonAt(menuContent, "NOVA PARTIDA", new Vector2(0f, -h * 1.3f), column, RestartFromMenu);
            ButtonAt(menuContent, "JOGAR CONTRA A CPU", new Vector2(0f, -h * 2.6f), column, () => SelectMode(false, false));
            ButtonAt(menuContent, "2 JOGADORES LOCAIS", new Vector2(0f, -h * 3.9f), column, () => SelectMode(true, false));
            ButtonAt(menuContent, "ASSISTIR: CPU x CPU", new Vector2(0f, -h * 5.2f), column, () => SelectMode(false, true));
            ButtonAt(menuContent, "REMAPEAR CONTROLES", new Vector2(0f, -h * 6.5f), column, OpenBindings);
            ButtonAt(menuContent, "SAIR DO JOGO", new Vector2(0f, -h * 7.8f), column, controls.Exit);
            Label(menuContent, "2P: conecte dois controles. Sem controle,\no jogador 1 usa o teclado.\nStart / Esc fecha o menu.",
                new Vector2(0f, -h * 9.1f), column, h * 2f, Color.white, presentation.FontSize * 0.68f);
            float x = column + pad;
            Label(menuContent, "AJUSTAR A SENSACAO", new Vector2(x, 0f), column, h, config.SelectedColor);
            jogSlider = TuningSlider(menuContent, "Corrida", new Vector2(x, -h * 1.2f), column,
                config.JogRange, controls.Match.Parameters.JogSpeed, SetJog, out jogValue);
            sprintSlider = TuningSlider(menuContent, "Sprint", new Vector2(x, -h * 2.6f), column,
                config.SprintRange, controls.Match.Parameters.SprintSpeed, SetSprint, out sprintValue);
            passSlider = TuningSlider(menuContent, "Forca do passe", new Vector2(x, -h * 4f), column,
                config.PassRange, controls.Match.Parameters.PassMaxSpeed, SetPass, out passValue);
            shotSlider = TuningSlider(menuContent, "Forca do chute", new Vector2(x, -h * 5.4f), column,
                config.ShotRange, controls.Match.Parameters.ShotMaxSpeed, SetShot, out shotValue);
            pressureSlider = TuningSlider(menuContent, "Raio de pressao da CPU", new Vector2(x, -h * 6.8f), column,
                config.PressureRange, controls.Match.Parameters.AiPressureRadius, SetPressure, out pressureValue);
            homeStats = Label(menuContent, "", new Vector2(x, -h * 8.4f), column, h * 1.5f,
                config.HomeColor, presentation.FontSize * 0.7f);
            awayStats = Label(menuContent, "", new Vector2(x, -h * 10f), column, h * 1.5f,
                config.AwayColor, presentation.FontSize * 0.7f);
            BuildBindings(width, height);
        }

        private void BuildBindings(float width, float height)
        {
            float pad = presentation.PanelPadding, h = presentation.ButtonHeight;
            bindingPanel = Rect("Bindings", menu, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(pad, -pad - h * 2f), new Vector2(width - pad * 2f, height - h * 2f - pad * 2f));
            bindingTitle = Label(bindingPanel, "", Vector2.zero, width - pad * 2f, h * 2f, Color.white,
                presentation.FontSize * 0.75f);
            float quarter = (width - pad * 2f) / 4f;
            ButtonAt(bindingPanel, "JOGADOR 1", new Vector2(0f, -h * 2f), quarter, () => SelectBindingPlayer(0));
            ButtonAt(bindingPanel, "JOGADOR 2", new Vector2(quarter, -h * 2f), quarter, () => SelectBindingPlayer(1));
            ButtonAt(bindingPanel, "PADRAO", new Vector2(quarter * 2f, -h * 2f), quarter,
                () => { inputs.ResetBindings(bindingPlayer); RefreshBindings(); });
            bindingsBackButton = ButtonAt(bindingPanel, "VOLTAR", new Vector2(quarter * 3f, -h * 2f), quarter, CloseBindings);
            RectTransform viewport = Rect("Bindings Viewport", bindingPanel, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, -h * 3.3f), new Vector2(width - pad * 2f, height - pad * 2f - h * 5.4f));
            viewport.gameObject.AddComponent<Image>().color = new Color(0.02f, 0.03f, 0.035f, 1f);
            viewport.gameObject.AddComponent<RectMask2D>();
            bindingRows = Rect("Bindings Rows", viewport, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero,
                new Vector2(width - pad * 2f, 0f));
            VerticalLayoutGroup layout = bindingRows.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childControlWidth = true; layout.childControlHeight = false;
            layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
            layout.spacing = h * 0.1f;
            ContentSizeFitter fitter = bindingRows.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport; scroll.content = bindingRows;
            scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped;
            bindingPanel.gameObject.SetActive(false);
        }

        public void ToggleMenu()
        {
            if (!initialized) return;
            if (MenuVisible) { CloseMenu(); return; }
            inputs.CancelRebind();
            menuContent.gameObject.SetActive(true);
            bindingPanel.gameObject.SetActive(false);
            menu.gameObject.SetActive(true);
            if (!controls.IsPaused) controls.TogglePause();
            eventSystem.sendNavigationEvents = true;
            eventSystem.SetSelectedGameObject(resumeButton.gameObject);
            RefreshTuneLabels();
            RefreshMatchLabels();
        }

        public void CloseMenu(bool resume = true)
        {
            if (!initialized) return;
            inputs.CancelRebind();
            menu.gameObject.SetActive(false);
            bindingPanel.gameObject.SetActive(false);
            eventSystem.sendNavigationEvents = false;
            eventSystem.SetSelectedGameObject(null);
            if (resume && controls.IsPaused) controls.TogglePause();
        }

        private void RestartFromMenu() { CloseMenu(); controls.Restart(); }
        private void SelectMode(bool local, bool cpu)
        {
            CloseMenu(); controls.SetTwoPlayers(local); controls.SetCpuHome(cpu); controls.Restart();
        }
        private void OpenBindings()
        {
            menuContent.gameObject.SetActive(false);
            bindingPanel.gameObject.SetActive(true);
            RefreshBindings();
        }
        private void CloseBindings()
        {
            inputs.CancelRebind();
            bindingPanel.gameObject.SetActive(false);
            menuContent.gameObject.SetActive(true);
            eventSystem.SetSelectedGameObject(resumeButton.gameObject);
        }
        private void SelectBindingPlayer(int player) { inputs.CancelRebind(); bindingPlayer = player; RefreshBindings(); }
        private void RefreshBindings()
        {
            refreshBindings = false;
            eventSystem.SetSelectedGameObject(bindingsBackButton.gameObject);
            for (int i = bindingRows.childCount - 1; i >= 0; i--)
            {
                GameObject child = bindingRows.GetChild(i).gameObject;
                child.SetActive(false); Destroy(child);
            }
            previousDevice = inputs.DeviceLabel(bindingPlayer);
            bindingTitle.text = "JOGADOR " + (bindingPlayer + 1) + " / " + previousDevice +
                "\nSelecione um comando e pressione o novo controle. Esc cancela. Salvo neste PC.";
            string group = inputs.UsesKeyboard(bindingPlayer) ? "Keyboard" : "Gamepad";
            InputActionMap map = inputs.Actions(bindingPlayer).FindActionMap("Gameplay", true);
            for (int a = 0; a < map.actions.Count; a++)
            {
                InputAction action = map.actions[a];
                for (int b = 0; b < action.bindings.Count; b++)
                {
                    InputBinding binding = action.bindings[b];
                    if (binding.isComposite || !binding.groups.Contains(group)) continue;
                    int index = b;
                    string name = action.name + (binding.isPartOfComposite ? " / " + binding.name : "");
                    Button button = ButtonAt(bindingRows, name + " : " + action.GetBindingDisplayString(b), Vector2.zero,
                        bindingRows.rect.width, () => StartRebind(action, index));
                    button.interactable = previousDevice != "Sem controle conectado";
                    button.gameObject.AddComponent<LayoutElement>().preferredHeight = presentation.ButtonHeight;
                }
            }
        }
        private void StartRebind(InputAction action, int binding)
        {
            bindingTitle.text = "AGUARDANDO NOVO CONTROLE...\nEsc cancela. O remapeamento e salvo neste PC.";
            inputs.Rebind(bindingPlayer, action, binding, OnBindingsChanged);
        }
        private void OnBindingsChanged() { refreshBindings = true; }

        private void SetJog(float value)
        {
            if (!float.IsFinite(value)) return;
            FootballParameters p = controls.Match.Parameters;
            p.JogSpeed = Mathf.Clamp(value, config.JogRange.x, config.JogRange.y);
            p.SprintSpeed = Mathf.Max(p.SprintSpeed, p.JogSpeed);
            sprintSlider.SetValueWithoutNotify(p.SprintSpeed); RefreshTuneLabels();
        }
        private void SetSprint(float value)
        {
            if (!float.IsFinite(value)) return;
            FootballParameters p = controls.Match.Parameters;
            p.SprintSpeed = Mathf.Clamp(value, config.SprintRange.x, config.SprintRange.y);
            p.JogSpeed = Mathf.Min(p.JogSpeed, p.SprintSpeed);
            jogSlider.SetValueWithoutNotify(p.JogSpeed); RefreshTuneLabels();
        }
        private void SetPass(float value)
        {
            if (!float.IsFinite(value)) return;
            controls.Match.Parameters.PassMaxSpeed = Mathf.Max(controls.Match.Parameters.PassMinSpeed,
                Mathf.Clamp(value, config.PassRange.x, config.PassRange.y));
            RefreshTuneLabels();
        }
        private void SetShot(float value)
        {
            if (!float.IsFinite(value)) return;
            controls.Match.Parameters.ShotMaxSpeed = Mathf.Max(controls.Match.Parameters.ShotMinSpeed,
                Mathf.Clamp(value, config.ShotRange.x, config.ShotRange.y));
            RefreshTuneLabels();
        }
        private void SetPressure(float value)
        {
            if (!float.IsFinite(value)) return;
            controls.Match.Parameters.AiPressureRadius = Mathf.Clamp(value, config.PressureRange.x, config.PressureRange.y);
            RefreshTuneLabels();
        }
        private void RefreshTuneLabels()
        {
            FootballParameters p = controls.Match.Parameters;
            jogValue.SetText("Corrida: {0:1} m/s", p.JogSpeed);
            sprintValue.SetText("Sprint: {0:1} m/s", p.SprintSpeed);
            passValue.SetText("Passe: {0:1} m/s", p.PassMaxSpeed);
            shotValue.SetText("Chute: {0:1} m/s", p.ShotMaxSpeed);
            pressureValue.SetText("Pressao CPU: {0:1} m", p.AiPressureRadius);
        }

        private void Update()
        {
            if (!initialized) return;
            uiModule.enabled = !inputs.IsRebinding;
            if (MenuVisible && bindingPanel.gameObject.activeSelf &&
                (refreshBindings || previousDevice != inputs.DeviceLabel(bindingPlayer))) RefreshBindings();
            refreshCountdown -= Time.unscaledDeltaTime;
            if (refreshCountdown > 0f) return;
            refreshCountdown = presentation.StatusInterval;
            RefreshMatchLabels();
        }

        private void RefreshMatchLabels()
        {
            FootballMatch match = controls.Match;
            float simulatedSeconds = match.ElapsedSeconds / match.Parameters.MatchSeconds * 90f * 60f;
            float minute = Mathf.Floor(simulatedSeconds / 60f);
            float second = Mathf.Floor(simulatedSeconds - minute * 60f);
            score.SetText("{0:0}  :  {1:0}", match.HomeGoals, match.AwayGoals);
            clock.SetText("{0:00}:{1:00}   /   {2:0} TEMPO", minute, second, match.Half);
            string mode = match.CpuHome ? "CPU x CPU" : controls.TwoPlayers ? "2 JOGADORES LOCAIS" : "VOCE x CPU";
            if (previousMode != mode) { previousMode = mode; modeLabel.text = mode; }
            string nextStatus = controls.IsPaused ? "PARTIDA PAUSADA" : match.Status;
            if (previousStatus != nextStatus) { previousStatus = nextStatus; status.text = nextStatus; }
            int selected = Mathf.Clamp(match.ControlledPlayer, 0, match.Players.Length - 1);
            FootballPlayer player = match.Players[selected];
            playerLabel.SetText("SEU JOGADOR  #{0:0}    ENERGIA {1:0}%", player.Number, player.Stamina * 100f);
            shotLabel.SetText("CHUTE  {0:0}%   /   segure J ou Sul", match.Charge01 * 100f);
            SetBar(staminaFill, player.Stamina); SetBar(shotFill, match.Charge01);
            if (MenuVisible)
            {
                FootballStatistics home = match.HomeStats, away = match.AwayStats;
                homeStats.SetText("MANDANTE   Chutes {0:0}   Passes {1:0}\nCompletos {2:0}   Defesas {3:0}",
                    home.Shots, home.Passes, home.CompletedPasses, home.Saves);
                awayStats.SetText("VISITANTE   Chutes {0:0}   Passes {1:0}\nCompletos {2:0}   Defesas {3:0}",
                    away.Shots, away.Passes, away.CompletedPasses, away.Saves);
            }
        }

        private void WarmLabels()
        {
            score.SetText("{0:0}  :  {1:0}", 999f, 999f);
            clock.SetText("{0:00}:{1:00}   /   {2:0} TEMPO", 999f, 59f, 2f);
            playerLabel.SetText("SEU JOGADOR  #{0:0}    ENERGIA {1:0}%", 99f, 100f);
            shotLabel.SetText("CHUTE  {0:0}%   /   segure J ou Sul", 100f);
            score.ForceMeshUpdate(); clock.ForceMeshUpdate(); playerLabel.ForceMeshUpdate(); shotLabel.ForceMeshUpdate();
        }

        private Slider TuningSlider(Transform parent, string title, Vector2 position, float width, Vector2 range,
            float value, UnityAction<float> changed, out TextMeshProUGUI valueLabel)
        {
            float h = presentation.ButtonHeight;
            valueLabel = Label(parent, title, position, width, h * 0.6f, Color.white, presentation.FontSize * 0.75f);
            RectTransform track = Rect(title + " Slider", parent, new Vector2(0f, 1f), new Vector2(0f, 1f),
                position - Vector2.up * h * 0.65f, new Vector2(width, h * 0.4f));
            Image background = track.gameObject.AddComponent<Image>(); background.color = new Color(0.11f, 0.17f, 0.2f);
            RectTransform fill = Rect("Fill", track, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            fill.anchorMin = Vector2.zero; fill.anchorMax = Vector2.one; fill.offsetMin = Vector2.zero; fill.offsetMax = Vector2.zero;
            fill.gameObject.AddComponent<Image>().color = config.SelectedColor;
            RectTransform handle = Rect("Handle", track, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(h * 0.5f, h * 0.65f));
            Image handleImage = handle.gameObject.AddComponent<Image>(); handleImage.color = Color.white;
            Slider slider = track.gameObject.AddComponent<Slider>();
            slider.direction = Slider.Direction.LeftToRight; slider.minValue = range.x; slider.maxValue = range.y;
            slider.fillRect = fill; slider.handleRect = handle; slider.targetGraphic = handleImage;
            slider.SetValueWithoutNotify(value);
            slider.onValueChanged.AddListener(changed);
            return slider;
        }
        private RectTransform Bar(Transform parent, string name, Vector2 position, float width, float height, Color color)
        {
            RectTransform track = Rect(name + " Track", parent, new Vector2(0f, 1f), new Vector2(0f, 1f),
                position, new Vector2(width, height));
            track.gameObject.AddComponent<Image>().color = new Color(0.12f, 0.19f, 0.2f);
            RectTransform fill = Rect(name + " Fill", track, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            fill.anchorMin = Vector2.zero; fill.anchorMax = Vector2.one; fill.offsetMin = Vector2.zero; fill.offsetMax = Vector2.zero;
            Image graphic = fill.gameObject.AddComponent<Image>(); graphic.color = color; graphic.raycastTarget = false;
            return fill;
        }
        private static void SetBar(RectTransform bar, float value)
        { Vector2 anchor = bar.anchorMax; anchor.x = Mathf.Clamp01(value); bar.anchorMax = anchor; }
        private TextMeshProUGUI Label(Transform parent, string value, Vector2 position, float width, float height,
            Color color, float size = 0f, TextAlignmentOptions alignment = TextAlignmentOptions.TopLeft)
        {
            RectTransform rect = Rect("Label", parent, new Vector2(0f, 1f), new Vector2(0f, 1f), position, new Vector2(width, height));
            rect.gameObject.SetActive(false);
            TextMeshProUGUI label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font; label.fontSize = size > 0f ? size : presentation.FontSize;
            label.color = color; label.raycastTarget = false;
            label.alignment = alignment; label.text = value;
            rect.gameObject.SetActive(true);
            return label;
        }
        private Button ButtonAt(Transform parent, string text, Vector2 position, float width, UnityAction clicked)
        {
            RectTransform rect = Rect("Button " + text, parent, new Vector2(0f, 1f), new Vector2(0f, 1f), position,
                new Vector2(width, presentation.ButtonHeight));
            Image image = rect.gameObject.AddComponent<Image>(); image.color = new Color(0.1f, 0.2f, 0.23f);
            Button button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.highlightedColor = config.SelectedColor; colors.selectedColor = config.SelectedColor;
            colors.pressedColor = config.HomeColor; button.colors = colors;
            button.onClick.AddListener(clicked);
            Label(rect, text, Vector2.zero, width, presentation.ButtonHeight, Color.white,
                presentation.FontSize * 0.72f, TextAlignmentOptions.Center);
            return button;
        }
        private static RectTransform Panel(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            RectTransform rect = Rect(name, parent, anchor, pivot, position, size);
            rect.gameObject.AddComponent<Image>().color = new Color(0.025f, 0.045f, 0.055f, 0.92f);
            return rect;
        }
        private static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            var obj = new GameObject(name, typeof(RectTransform));
            RectTransform rect = (RectTransform)obj.transform;
            rect.SetParent(parent, false); rect.anchorMin = anchor; rect.anchorMax = anchor;
            rect.pivot = pivot; rect.anchoredPosition = position; rect.sizeDelta = size;
            return rect;
        }
        private void OnDestroy()
        {
            initialized = false;
            if (inputs != null) inputs.CancelRebind();
            if (eventSystem != null) eventSystem.SetSelectedGameObject(null);
        }
    }
}
