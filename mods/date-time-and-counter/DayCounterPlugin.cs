using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using GK2.Framework;
using LazyBearTechnology;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace GK2.DayCounter;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
[BepInDependency(FrameworkPlugin.PluginGuid, BepInDependency.DependencyFlags.HardDependency)]
public sealed class DayCounterPlugin : BaseUnityPlugin
{
    public const string PluginGuid = "local.gk2.daycounter";
    public const string PluginName = "GK2 Date Time and Counter";
    public const string PluginVersion = "0.1.0";

    private void Awake()
    {
        FrameworkApi.RegisterMod(new DayCounterModule(Logger, Config), Config);
        Logger.LogInfo("GK2 Date Time and Counter registered with GK2 Mod Framework.");
    }
}

internal sealed class DayCounterModule : Gk2ModBase
{
    private static readonly IReadOnlyList<Gk2ModDependency> ModDependencies = new[]
    {
        new Gk2ModDependency(FrameworkPlugin.PluginGuid, "0.1.12", "0.2.0")
    };

    private readonly BepInEx.Logging.ManualLogSource logger;
    private readonly ConfigFile config;
    private readonly Gk2ModMetadata metadata = new(
        DayCounterPlugin.PluginGuid,
        DayCounterPlugin.PluginName,
        "Bulgarian Localization Team",
        DayCounterPlugin.PluginVersion,
        "Shows the current in-game day and time on the HUD.",
        supportsRuntimeToggle: true,
        requiresKnownBuild: false);

    private ConfigEntry<bool>? showDayCounter;
    private ConfigEntry<bool>? showClock;
    private ConfigEntry<string>? timeFormat;
    private ConfigEntry<int>? minuteStep;
    private ConfigEntry<bool>? attachToLocationLabel;
    private ConfigEntry<bool>? numberOnly;
    private ConfigEntry<int>? fontSize;
    private ConfigEntry<int>? detachedX;
    private ConfigEntry<int>? detachedY;
    private ConfigEntry<int>? detachedFontSize;
    private ConfigEntry<bool>? detachedBackground;
    private ConfigEntry<bool>? repositionWidget;
    private DayCounterBehaviour? behaviour;

    public DayCounterModule(BepInEx.Logging.ManualLogSource source, ConfigFile config)
    {
        logger = source;
        this.config = config;
    }

    public override Gk2ModMetadata Metadata => metadata;
    public override IReadOnlyList<Gk2ModDependency> Dependencies => ModDependencies;

    public override void OnRegister(Gk2ModContext context)
    {
        showDayCounter = context.Settings.AddToggle(
            "General", "ShowDayCounter", true,
            "Show day counter", "Display the current in-game day.", order: 0);
        showClock = context.Settings.AddToggle(
            "General", "ShowClock", false,
            "Show in-game time", "Display the current in-game time with the day counter.", order: 1);
        timeFormat = context.Settings.AddDropdown(
            "Clock", "TimeFormat", "24h", new[] { "24h", "12h" },
            "Time format", "Choose between 24-hour and 12-hour time.", order: 0);
        minuteStep = context.Settings.AddDropdown<int>(
            "Clock", "MinuteStep", 10, new[] { 1, 5, 10, 15, 30, 60 },
            "Minute steps", "Round displayed minutes down to this interval.", order: 1);
        attachToLocationLabel = context.Settings.AddToggle(
            "General", "AttachToLocationLabel", true,
            "Attach to location label", "When disabled, show the date and time at a fixed screen position.", order: 2);
        // Keep the previous config key as a migration source while presenting the control
        // beside its related attachment toggle in the Framework UI.
        bool oldRepositionValue = config.Bind("Detached display", "RepositionWidget", false).Value;
        repositionWidget = context.Settings.AddToggle(
            "General", "RepositionWidget", oldRepositionValue,
            "Reposition widget", "Works only while Attach to location label is off. Turns off when attached. Drag with left mouse; right-click or Escape to finish.", order: 3);
        numberOnly = context.Settings.AddToggle(
            "General", "NumberOnly", false,
            "Show number only", "Hide the localized word for day and show only the number.", order: 4);
        fontSize = context.Settings.AddIntSlider(
            "Appearance", "FontSize", 18, 10, 36,
            "Attached font size", "Text size used while the counter is attached to the location label.", step: 1, order: 0);
        detachedX = context.Settings.AddIntSlider(
            "Detached display", "PositionX", 560, 0, 1920,
            "Horizontal position", "Distance in reference pixels from the left edge. Used only while detached.", step: 1, order: 0);
        detachedY = context.Settings.AddIntSlider(
            "Detached display", "PositionY", 35, 0, 1080,
            "Vertical position", "Distance in reference pixels from the top edge. Used only while detached.", step: 1, order: 1);
        detachedFontSize = context.Settings.AddIntSlider(
            "Detached display", "FontSize", 18, 10, 48,
            "Detached font size", "Text size used only while detached.", step: 1, order: 2);
        detachedBackground = context.Settings.AddToggle(
            "Detached display", "ShowBackground", true,
            "Show background", "Show the location-style HUD frame behind the detached day counter.", order: 3);
        logger.LogInfo("Date Time and Counter settings registered.");
    }

    public override void OnEnable()
    {
        if (behaviour != null) return;
        GameObject root = new("GK2.DayCounter");
        UnityEngine.Object.DontDestroyOnLoad(root);
        behaviour = root.AddComponent<DayCounterBehaviour>();
        behaviour.SetLogger(logger);
        ApplySettings();
        if (showDayCounter != null) showDayCounter.SettingChanged += OnSettingChanged;
        if (showClock != null) showClock.SettingChanged += OnSettingChanged;
        if (timeFormat != null) timeFormat.SettingChanged += OnSettingChanged;
        if (minuteStep != null) minuteStep.SettingChanged += OnSettingChanged;
        if (attachToLocationLabel != null) attachToLocationLabel.SettingChanged += OnSettingChanged;
        if (numberOnly != null) numberOnly.SettingChanged += OnSettingChanged;
        if (fontSize != null) fontSize.SettingChanged += OnSettingChanged;
        if (detachedX != null) detachedX.SettingChanged += OnSettingChanged;
        if (detachedY != null) detachedY.SettingChanged += OnSettingChanged;
        if (detachedFontSize != null) detachedFontSize.SettingChanged += OnSettingChanged;
        if (detachedBackground != null) detachedBackground.SettingChanged += OnSettingChanged;
        if (repositionWidget != null) repositionWidget.SettingChanged += OnSettingChanged;
        logger.LogInfo("Date Time and Counter enabled.");
    }

    public override void OnDisable()
    {
        if (showDayCounter != null) showDayCounter.SettingChanged -= OnSettingChanged;
        if (showClock != null) showClock.SettingChanged -= OnSettingChanged;
        if (timeFormat != null) timeFormat.SettingChanged -= OnSettingChanged;
        if (minuteStep != null) minuteStep.SettingChanged -= OnSettingChanged;
        if (attachToLocationLabel != null) attachToLocationLabel.SettingChanged -= OnSettingChanged;
        if (numberOnly != null) numberOnly.SettingChanged -= OnSettingChanged;
        if (fontSize != null) fontSize.SettingChanged -= OnSettingChanged;
        if (detachedX != null) detachedX.SettingChanged -= OnSettingChanged;
        if (detachedY != null) detachedY.SettingChanged -= OnSettingChanged;
        if (detachedFontSize != null) detachedFontSize.SettingChanged -= OnSettingChanged;
        if (detachedBackground != null) detachedBackground.SettingChanged -= OnSettingChanged;
        if (repositionWidget != null) repositionWidget.SettingChanged -= OnSettingChanged;
        if (behaviour != null)
        {
            UnityEngine.Object.Destroy(behaviour.gameObject);
            behaviour = null;
        }
        logger.LogInfo("Date Time and Counter disabled.");
    }

    private void OnSettingChanged(object sender, EventArgs args)
    {
        if (attachToLocationLabel?.Value == true && repositionWidget?.Value == true)
            repositionWidget.Value = false;
        ApplySettings();
    }

    private void ApplySettings()
    {
        if (behaviour != null)
            behaviour.Configure(showDayCounter?.Value ?? true,
                showClock?.Value ?? false,
                timeFormat?.Value ?? "24h",
                minuteStep?.Value ?? 10,
                attachToLocationLabel?.Value ?? true,
                numberOnly?.Value ?? false,
                fontSize?.Value ?? 18,
                detachedX?.Value ?? 560,
                detachedY?.Value ?? 35,
                detachedFontSize?.Value ?? 18,
                detachedBackground?.Value ?? true,
                repositionWidget?.Value ?? false,
                value =>
                {
                    if (repositionWidget != null && repositionWidget.Value != value)
                        repositionWidget.Value = value;
                },
                (x, y) =>
                {
                    if (detachedX != null && detachedX.Value != x) detachedX.Value = x;
                    if (detachedY != null && detachedY.Value != y) detachedY.Value = y;
                });
    }
}

internal sealed class DayCounterBehaviour : MonoBehaviour
{
    private static readonly FieldInfo? WorldZoneLabelField =
        typeof(WorldZoneWidget).GetField("worldZoneLabel", BindingFlags.Instance | BindingFlags.NonPublic);

    private bool showDay = true;
    private bool showClock;
    private bool use12Hour;
    private int minuteInterval = 10;
    private bool attachToLocation = true;
    private bool showNumberOnly;
    private int fontSize = 18;
    private int currentDay = -1;
    private float currentTimeOfDay;
    private TextMeshProUGUI? locationDayLabel;
    private TextMeshProUGUI? locationSource;
    private WorldZoneWidget? locationWidget;
    private GameObject? detachedRoot;
    private Image? detachedBackground;
    private Image? dragSurface;
    private TextMeshProUGUI? detachedLabel;
    private RectTransform? detachedParentRect;
    private TextMeshProUGUI? detachedSource;
    private int detachedX = 560;
    private int detachedY = 35;
    private int detachedFontSize = 18;
    private bool showDetachedBackground = true;
    private bool repositionRequested;
    private bool repositioning;
    private bool dragging;
    private Vector2 dragGrabOffset;
    private Outline? repositionOutline;
    private TextMeshProUGUI? coordinateLabel;
    private Action<bool>? setRepositionSetting;
    private static readonly Type? ModsMenuWindowType = Type.GetType("GK2.Framework.ModsMenuWindow, GK2.Framework");
    private static readonly FieldInfo? ModsMenuInstanceField = ModsMenuWindowType?.GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);
    private static readonly FieldInfo? ModsMenuSettingsPageField = ModsMenuWindowType?.GetField("settingsPage", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly PropertyInfo? ModsMenuSettingsIsOpen = ModsMenuSettingsPageField?.FieldType.GetProperty("IsOpen", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
    private static readonly MethodInfo? ModsMenuBuildControls = ModsMenuSettingsPageField?.FieldType.GetMethod("BuildControls", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
    private GameObject? hiddenModsWindow;
    private Action<int, int>? persistPosition;
    private int loggedWidgetInstanceId;
    private BepInEx.Logging.ManualLogSource? logger;

    public void SetLogger(BepInEx.Logging.ManualLogSource source)
    {
        logger = source;
        DayCounterLocalization.SetLogger(source);
    }

    private void OnEnable() => InvokeRepeating(nameof(PollState), 0f, 0.25f);

    private void OnDisable() => CancelInvoke(nameof(PollState));

    private void OnDestroy()
    {
        if (repositioning || hiddenModsWindow != null)
            FinishRepositioning("mod unloaded", reopenMenu: true);
        if (detachedRoot != null) Destroy(detachedRoot);
    }

    public void Configure(bool show, bool clock, string format, int minuteStep, bool attach, bool numberOnly, int size, int x, int y, int detachedSize, bool background,
        bool reposition, Action<bool> setReposition, Action<int, int> persist)
    {
        showDay = show;
        showClock = clock;
        use12Hour = string.Equals(format, "12h", StringComparison.OrdinalIgnoreCase);
        minuteInterval = Mathf.Clamp(minuteStep, 1, 60);
        attachToLocation = attach;
        showNumberOnly = numberOnly;
        fontSize = Mathf.Clamp(size, 10, 36);
        detachedX = Mathf.Clamp(x, 0, 1920);
        detachedY = Mathf.Clamp(y, 0, 1080);
        detachedFontSize = Mathf.Clamp(detachedSize, 10, 48);
        showDetachedBackground = background;
        setRepositionSetting = setReposition;
        persistPosition = persist;
        if (reposition != repositionRequested)
        {
            repositionRequested = reposition;
            if (reposition) BeginRepositioning();
            else if (repositioning) FinishRepositioning("setting disabled", reopenMenu: true);
        }
        ApplyDetachedLayout();
        // Settings can fire continuously while a slider is dragged. Reconcile the
        // current mode immediately instead of hiding both labels until the next poll.
        PollState();
    }

    private void PollState()
    {
        bool inGame = IsInGame();
        currentDay = inGame ? ReadCurrentDay() : -1;
        currentTimeOfDay = inGame ? ReadCurrentTimeOfDay() : 0f;
        if (attachToLocation) UpdateLocationLabel(inGame);
        else UpdateDetachedLabel(inGame);
    }

    private void UpdateLocationLabel(bool inGame)
    {
        if (detachedRoot != null && detachedRoot.activeSelf)
            detachedRoot.SetActive(false);
        if (!EnsureLocationLabel()) return;

        SyncLanguageStyle(locationSource!, locationDayLabel!, copyAlignment: true);
        bool visible = inGame && ((showDay && currentDay > 0) || showClock);
        UpdateText(locationDayLabel!, visible);
    }

    private void UpdateDetachedLabel(bool inGame)
    {
        if (locationDayLabel != null && locationDayLabel.gameObject.activeSelf)
            locationDayLabel.gameObject.SetActive(false);
        if (!inGame || !EnsureDetachedLabel())
        {
            if (detachedRoot != null && detachedRoot.activeSelf)
                detachedRoot.SetActive(false);
            return;
        }

        bool visible = inGame && ((showDay && currentDay > 0) || showClock) && IsHudHierarchyVisible(detachedSource?.transform);
        if (detachedBackground != null && detachedBackground.enabled != showDetachedBackground)
            detachedBackground.enabled = showDetachedBackground;
        string displayText = FormatDisplay();
        SyncLanguageStyle(detachedSource!, detachedLabel!, copyAlignment: false);
        if (detachedLabel!.text != displayText) detachedLabel.text = displayText;
        detachedLabel.isRightToLeftText = DayCounterLocalization.IsRightToLeft;
        if (detachedLabel!.fontSize != detachedFontSize) detachedLabel.fontSize = detachedFontSize;
        ApplyDetachedLayout();
        UpdateDetachedPanelSize(displayText);
        UpdateCoordinateLabel();
        if (detachedRoot!.activeSelf != visible) detachedRoot.SetActive(visible);
    }

    private bool EnsureLocationLabel()
    {
        if (locationDayLabel != null) return true;
        if (!TryGetLocationSource(out TextMeshProUGUI? source)) return false;
        locationSource = source;

        GameObject child = new("GK2.DayCounter.LocationLabel");
        child.transform.SetParent(source!.transform.parent, false);
        locationDayLabel = child.AddComponent<TextMeshProUGUI>();
        CopyTextStyle(source, locationDayLabel);

        RectTransform sourceRect = source.rectTransform;
        RectTransform targetRect = locationDayLabel.rectTransform;
        targetRect.anchorMin = sourceRect.anchorMin;
        targetRect.anchorMax = sourceRect.anchorMax;
        targetRect.pivot = sourceRect.pivot;
        targetRect.sizeDelta = sourceRect.sizeDelta;
        targetRect.anchoredPosition = sourceRect.anchoredPosition + new Vector2(0f, -sourceRect.rect.height - 2f);
        return true;
    }

    private bool EnsureDetachedLabel()
    {
        if (!TryGetLocationSource(out TextMeshProUGUI? source)) return false;
        Canvas? sourceCanvas = source!.canvas;
        Canvas? rootCanvas = sourceCanvas != null ? sourceCanvas.rootCanvas : null;
        Transform? parent = rootCanvas != null ? rootCanvas.transform : null;
        if (parent == null)
        {
            logger?.LogWarning("Could not find the root HUD Canvas; detached display cannot attach to the game HUD.");
            return false;
        }
        if (detachedLabel != null && detachedRoot != null && detachedRoot.transform.parent == parent && detachedSource == source)
            return true;

        if (detachedRoot != null) Destroy(detachedRoot);
        detachedRoot = null;
        detachedBackground = null;
        dragSurface = null;
        detachedLabel = null;
        detachedSource = source;
        detachedParentRect = parent as RectTransform;
        if (detachedParentRect == null)
        {
            logger?.LogWarning("Root HUD Canvas has no RectTransform; detached display cannot attach to the game HUD.");
            return false;
        }
        detachedRoot = new GameObject("GK2.DayCounter.DetachedPanel", typeof(RectTransform), typeof(Image));
        detachedRoot.transform.SetParent(parent, false);
        detachedRoot.transform.SetAsLastSibling();
        detachedBackground = detachedRoot.GetComponent<Image>();
        detachedBackground.enabled = showDetachedBackground;
        Image? sourceBackground = FindLocationBackground(locationWidget!.transform, source.transform);
        if (sourceBackground != null)
        {
            CopyImageStyle(sourceBackground, detachedBackground!);
            detachedBackground!.raycastTarget = false;
            logger?.LogInfo($"Detached panel cloned HUD frame sprite '{sourceBackground.sprite?.name ?? "<no-sprite>"}' from '{sourceBackground.gameObject.name}'.");
        }
        else
        {
            detachedBackground!.enabled = false;
            logger?.LogWarning("No Image frame found in the location-label hierarchy; detached text will use no background.");
        }

        GameObject labelObject = new("GK2.DayCounter.DetachedLabel", typeof(RectTransform));
        labelObject.transform.SetParent(detachedRoot.transform, false);
        detachedLabel = labelObject.AddComponent<TextMeshProUGUI>();
        CopyTextStyle(source, detachedLabel);
        detachedLabel.fontSize = detachedFontSize;
        detachedLabel.alignment = TextAlignmentOptions.Center;

        RectTransform panelRect = (RectTransform)detachedRoot.transform;
        panelRect.anchorMin = new Vector2(0f, 1f);
        panelRect.anchorMax = new Vector2(0f, 1f);
        panelRect.pivot = new Vector2(0f, 1f);
        RectTransform labelRect = detachedLabel.rectTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.pivot = new Vector2(0.5f, 0.5f);
        labelRect.offsetMin = new Vector2(8f, 5f);
        labelRect.offsetMax = new Vector2(-8f, -5f);

        repositionOutline = detachedRoot.AddComponent<Outline>();
        repositionOutline.effectColor = new Color(0.2f, 0.9f, 1f, 1f);
        repositionOutline.effectDistance = new Vector2(1f, -1f);
        repositionOutline.enabled = false;
        GameObject dragSurfaceObject = new("GK2.DayCounter.DragSurface", typeof(RectTransform), typeof(Image));
        dragSurfaceObject.transform.SetParent(detachedRoot.transform, false);
        dragSurface = dragSurfaceObject.GetComponent<Image>();
        dragSurface.color = Color.clear;
        dragSurface.raycastTarget = false;
        RectTransform dragSurfaceRect = (RectTransform)dragSurfaceObject.transform;
        dragSurfaceRect.anchorMin = Vector2.zero;
        dragSurfaceRect.anchorMax = Vector2.one;
        dragSurfaceRect.offsetMin = Vector2.zero;
        dragSurfaceRect.offsetMax = Vector2.zero;
        DayCounterDragHandle dragHandle = dragSurfaceObject.AddComponent<DayCounterDragHandle>();
        dragHandle.Initialize(this);

        GameObject coordinates = new("GK2.DayCounter.Coordinates", typeof(RectTransform));
        coordinates.transform.SetParent(detachedRoot.transform, false);
        coordinateLabel = coordinates.AddComponent<TextMeshProUGUI>();
        CopyTextStyle(source, coordinateLabel);
        coordinateLabel.fontSize = Mathf.Max(12f, detachedFontSize * 0.75f);
        coordinateLabel.alignment = TextAlignmentOptions.Center;
        coordinateLabel.color = new Color(0.25f, 0.95f, 1f, 1f);
        coordinateLabel.raycastTarget = false;
        RectTransform coordinateRect = coordinateLabel.rectTransform;
        coordinateRect.anchorMin = new Vector2(0.5f, 0f);
        coordinateRect.anchorMax = new Vector2(0.5f, 0f);
        coordinateRect.pivot = new Vector2(0.5f, 1f);
        coordinateRect.anchoredPosition = new Vector2(0f, -1f);
        coordinateRect.sizeDelta = new Vector2(260f, 42f);
        coordinateLabel.gameObject.SetActive(false);
        detachedRoot.SetActive(false);
        ApplyDetachedLayout();
        return true;
    }

    private void ApplyDetachedLayout()
    {
        if (detachedRoot == null || detachedParentRect == null) return;
        RectTransform panelRect = (RectTransform)detachedRoot.transform;
        panelRect.anchorMin = new Vector2(0f, 1f);
        panelRect.anchorMax = new Vector2(0f, 1f);
        panelRect.pivot = new Vector2(0f, 1f);
        panelRect.anchoredPosition = new Vector2(detachedX, -detachedY);
        if (dragSurface != null)
        {
            dragSurface.enabled = repositioning;
            dragSurface.raycastTarget = repositioning;
        }
        if (detachedLabel != null && detachedLabel.fontSize != detachedFontSize)
            detachedLabel.fontSize = detachedFontSize;
        UpdateDetachedPanelSize(detachedLabel?.text ?? string.Empty);
    }

    private void UpdateDetachedPanelSize(string displayText)
    {
        if (detachedRoot == null || detachedLabel == null) return;
        Vector2 preferred = detachedLabel.GetPreferredValues(displayText);
        Vector2 desired = new(Mathf.Max(58f, Mathf.Ceil(preferred.x + 24f)), Mathf.Max(32f, Mathf.Ceil(preferred.y + 14f)));
        RectTransform rect = (RectTransform)detachedRoot.transform;
        if (rect.sizeDelta != desired) rect.sizeDelta = desired;
    }

    private void Update()
    {
        if (!repositioning) return;
        if (!IsInGame() || detachedRoot == null || !detachedRoot.activeInHierarchy)
        {
            FinishRepositioning("HUD widget is no longer available", reopenMenu: true);
            return;
        }
        if (Input.GetMouseButtonDown(1)) FinishRepositioning("right-click", reopenMenu: true);
        else if (Input.GetKeyDown(KeyCode.Escape)) FinishRepositioning("Escape", reopenMenu: true);
    }

    private void BeginRepositioning()
    {
        PollState();
        if (attachToLocation || !(showDay || showClock) || (showDay && currentDay <= 0 && !showClock) || detachedRoot == null || !detachedRoot.activeInHierarchy)
        {
            logger?.LogWarning("Repositioning needs a visible detached day counter in a loaded game; turn off 'Attach to location label' first.");
            repositionRequested = false;
            setRepositionSetting?.Invoke(false);
            return;
        }
        if (!TryHideModsMenu())
        {
            logger?.LogWarning("Could not hide the Mods settings window for repositioning; close it and try again.");
            repositionRequested = false;
            setRepositionSetting?.Invoke(false);
            return;
        }
        repositioning = true;
        dragging = false;
        if (repositionOutline != null) repositionOutline.enabled = true;
        if (coordinateLabel != null) coordinateLabel.gameObject.SetActive(true);
        if (dragSurface != null)
        {
            dragSurface.enabled = true;
            dragSurface.raycastTarget = true;
        }
        UpdateCoordinateLabel();
        logger?.LogInfo("Day counter reposition mode started; drag with left mouse, right-click or Escape to finish. The game remains paused.");
    }

    private void FinishRepositioning(string reason, bool reopenMenu)
    {
        if (!repositioning && hiddenModsWindow == null) return;
        dragging = false;
        repositioning = false;
        repositionRequested = false;
        if (repositionOutline != null) repositionOutline.enabled = false;
        if (coordinateLabel != null) coordinateLabel.gameObject.SetActive(false);
        if (dragSurface != null)
        {
            dragSurface.raycastTarget = false;
            dragSurface.enabled = false;
        }
        SaveDetachedPosition();
        setRepositionSetting?.Invoke(false);
        if (reopenMenu) ShowModsMenu();
        logger?.LogInfo($"Day counter reposition mode ended ({reason}); saved position X={detachedX}, Y={detachedY}.");
    }

    internal void BeginDrag(PointerEventData eventData)
    {
        if (!repositioning || eventData.button != PointerEventData.InputButton.Left || detachedParentRect == null || detachedRoot == null) return;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(detachedParentRect, eventData.position, eventData.pressEventCamera, out Vector2 local))
        {
            Vector2 pointer = new(local.x - detachedParentRect.rect.xMin, local.y - detachedParentRect.rect.yMax);
            dragGrabOffset = pointer - ((RectTransform)detachedRoot.transform).anchoredPosition;
            dragging = true;
        }
    }

    internal void Drag(PointerEventData eventData)
    {
        if (!repositioning || !dragging || detachedParentRect == null || detachedRoot == null) return;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(detachedParentRect, eventData.position, eventData.pressEventCamera, out Vector2 local)) return;
        Vector2 pointer = new(local.x - detachedParentRect.rect.xMin, local.y - detachedParentRect.rect.yMax);
        Vector2 target = pointer - dragGrabOffset;
        Rect rect = detachedParentRect.rect;
        RectTransform panel = (RectTransform)detachedRoot.transform;
        target.x = Mathf.Clamp(target.x, 0f, Mathf.Max(0f, rect.width - panel.rect.width));
        target.y = -Mathf.Clamp(-target.y, 0f, Mathf.Max(0f, rect.height - panel.rect.height));
        panel.anchoredPosition = target;
        detachedX = Mathf.RoundToInt(target.x);
        detachedY = Mathf.RoundToInt(-target.y);
        UpdateCoordinateLabel();
    }

    internal void EndDrag()
    {
        if (!dragging) return;
        dragging = false;
        SaveDetachedPosition();
    }

    private void SaveDetachedPosition()
    {
        if (detachedRoot != null)
        {
            RectTransform panel = (RectTransform)detachedRoot.transform;
            detachedX = Mathf.RoundToInt(panel.anchoredPosition.x);
            detachedY = Mathf.RoundToInt(-panel.anchoredPosition.y);
        }
        // Updating the exact sliders keeps both the visible settings and BepInEx config in sync.
        // The module writes these values via its setting-change callback.
        ApplyDetachedLayout();
        persistPosition?.Invoke(detachedX, detachedY);
    }

    private void UpdateCoordinateLabel()
    {
        if (coordinateLabel == null) return;
        string value = $"X {detachedX}   Y {detachedY}\n{DayCounterLocalization.MoveHint}";
        if (coordinateLabel.text != value) coordinateLabel.text = value;
        coordinateLabel.isRightToLeftText = DayCounterLocalization.IsRightToLeft;
        ArabicTextPreprocessor? preprocessor = coordinateLabel.GetComponent<ArabicTextPreprocessor>();
        bool arabic = DayCounterLocalization.IsRightToLeft;
        if (arabic && preprocessor == null) preprocessor = coordinateLabel.gameObject.AddComponent<ArabicTextPreprocessor>();
        if (preprocessor != null && preprocessor.enabled != arabic) preprocessor.enabled = arabic;
    }

    private bool TryHideModsMenu()
    {
        try
        {
            object? instance = ModsMenuInstanceField?.GetValue(null);
            Component? component = instance as Component;
            if (component == null || !component.gameObject.activeSelf) return false;
            component.gameObject.SetActive(false);
            hiddenModsWindow = component.gameObject;
            return true;
        }
        catch (Exception ex)
        {
            logger?.LogWarning("Mods menu hide failed: " + ex.Message);
            return false;
        }
    }

    private void ShowModsMenu()
    {
        GameObject? window = hiddenModsWindow;
        hiddenModsWindow = null;
        if (window == null) return;
        window.SetActive(true);
        try
        {
            object? settingsPage = ModsMenuSettingsPageField?.GetValue(window.GetComponent(ModsMenuWindowType!));
            bool isOpen = settingsPage != null && (bool)(ModsMenuSettingsIsOpen?.GetValue(settingsPage) ?? false);
            if (isOpen) ModsMenuBuildControls?.Invoke(settingsPage, null);
        }
        catch (Exception ex)
        {
            logger?.LogDebug("Could not refresh the Mods settings page after repositioning: " + ex.Message);
        }
    }

    private static Image? FindLocationBackground(Transform widget, Transform source)
    {
        // First search the widget itself. The location label's frame is often a
        // sibling Image rather than an ancestor of the TMP label.
        Image[] widgetImages = widget.GetComponentsInChildren<Image>(true);
        foreach (Image image in widgetImages)
        {
            if (image.sprite != null && image.gameObject != source.gameObject &&
                (string.Equals(image.gameObject.name, "Background", StringComparison.OrdinalIgnoreCase) ||
                 image.sprite.name.IndexOf("world_zone-location", StringComparison.OrdinalIgnoreCase) >= 0 &&
                 image.sprite.name.IndexOf("shadow", StringComparison.OrdinalIgnoreCase) < 0))
                return image;
        }
        // Prefer another non-shadow image if naming differs in a game update.
        foreach (Image image in widgetImages)
        {
            if (image.sprite != null && image.gameObject != source.gameObject &&
                image.sprite.name.IndexOf("shadow", StringComparison.OrdinalIgnoreCase) < 0)
                return image;
        }

        // Fall back to framed containers above the widget if the widget has no
        // frame of its own.
        for (Transform? current = widget.parent; current != null; current = current.parent)
        {
            Image? image = current.GetComponent<Image>();
            if (image != null && image.sprite != null) return image;
            if (current.GetComponent<Canvas>() != null) break;
        }
        return null;
    }

    private static void CopyImageStyle(Image source, Image target)
    {
        target.sprite = source.sprite;
        target.type = source.type;
        target.color = source.color;
        target.material = source.material;
        target.preserveAspect = source.preserveAspect;
        target.fillCenter = source.fillCenter;
        target.fillMethod = source.fillMethod;
        target.fillAmount = source.fillAmount;
        target.fillClockwise = source.fillClockwise;
        target.fillOrigin = source.fillOrigin;
        target.pixelsPerUnitMultiplier = source.pixelsPerUnitMultiplier;
    }

    private static bool IsHudHierarchyVisible(Transform? source)
    {
        if (source == null) return false;
        for (Transform? current = source; current != null; current = current.parent)
        {
            if (!current.gameObject.activeInHierarchy) return false;
            CanvasGroup? group = current.GetComponent<CanvasGroup>();
            if (group != null && group.enabled && group.alpha <= 0.001f)
                return false;
        }
        return source.GetComponent<Graphic>()?.enabled != false;
    }

    private bool TryGetLocationSource(out TextMeshProUGUI? source)
    {
        source = null;
        locationWidget = UnityEngine.Object.FindFirstObjectByType<WorldZoneWidget>();
        if (locationWidget == null || WorldZoneLabelField == null) return false;
        source = WorldZoneLabelField.GetValue(locationWidget) as TextMeshProUGUI;
        if (source != null && loggedWidgetInstanceId != locationWidget.GetInstanceID())
        {
            loggedWidgetInstanceId = locationWidget.GetInstanceID();
            LogLocationUiHierarchy(source);
        }
        return source != null;
    }

    private void LogLocationUiHierarchy(TextMeshProUGUI source)
    {
        try
        {
            logger?.LogInfo("Location HUD hierarchy snapshot (nearest first):");
            Transform? current = source.transform;
            for (int depth = 0; current != null && depth < 16; depth++, current = current.parent)
            {
                RectTransform? rect = current as RectTransform;
                Image? image = current.GetComponent<Image>();
                CanvasGroup? group = current.GetComponent<CanvasGroup>();
                Canvas? canvas = current.GetComponent<Canvas>();
                string rectInfo = rect == null
                    ? "no-rect"
                    : $"pos={rect.anchoredPosition}, size={rect.rect.size}, anchors={rect.anchorMin}..{rect.anchorMax}";
                string imageInfo = image == null
                    ? "no-image"
                    : $"image={image.sprite?.name ?? "<no-sprite>"}, type={image.type}, enabled={image.enabled}";
                string groupInfo = group == null
                    ? "no-canvas-group"
                    : $"canvas-group alpha={group.alpha}, interactable={group.interactable}, blocks={group.blocksRaycasts}";
                string canvasInfo = canvas == null
                    ? "no-canvas"
                    : $"canvas={canvas.renderMode}, enabled={canvas.enabled}, overrideSorting={canvas.overrideSorting}";
                logger?.LogInfo($"HUD[{depth}] '{current.name}' active={current.gameObject.activeSelf}/{current.gameObject.activeInHierarchy}; {rectInfo}; {imageInfo}; {groupInfo}; {canvasInfo}");
            }

            Transform? container = source.transform.parent;
            if (container != null)
            {
                for (int i = 0; i < container.childCount; i++)
                {
                    Transform child = container.GetChild(i);
                    Image? image = child.GetComponent<Image>();
                    logger?.LogInfo($"HUD sibling[{i}] '{child.name}' image={image?.sprite?.name ?? "<none>"}");
                }
            }
        }
        catch (Exception ex)
        {
            logger?.LogWarning("HUD hierarchy snapshot failed: " + ex.Message);
        }
    }

    private void UpdateText(TextMeshProUGUI label, bool visible)
    {
        string displayText = FormatDisplay();
        if (label.text != displayText) label.text = displayText;
        label.isRightToLeftText = DayCounterLocalization.IsRightToLeft;
        if (label.fontSize != fontSize) label.fontSize = fontSize;
        if (label.gameObject.activeSelf != visible) label.gameObject.SetActive(visible);
    }

    private static void SyncLanguageStyle(TextMeshProUGUI source, TextMeshProUGUI target, bool copyAlignment)
    {
        // The game swaps TMP font assets/materials when its language changes. Our cloned label
        // must follow those changes too, especially for CJK and Cyrillic glyph coverage.
        if (target.font != source.font) target.font = source.font;
        if (target.fontSharedMaterial != source.fontSharedMaterial)
            target.fontSharedMaterial = source.fontSharedMaterial;
        if (target.fontStyle != source.fontStyle) target.fontStyle = source.fontStyle;
        if (target.color != source.color) target.color = source.color;
        if (copyAlignment && target.alignment != source.alignment) target.alignment = source.alignment;

        bool arabic = DayCounterLocalization.IsRightToLeft;
        if (target.isRightToLeftText != arabic) target.isRightToLeftText = arabic;
        ArabicTextPreprocessor? preprocessor = target.GetComponent<ArabicTextPreprocessor>();
        if (arabic && preprocessor == null)
            preprocessor = target.gameObject.AddComponent<ArabicTextPreprocessor>();
        if (preprocessor != null && preprocessor.enabled != arabic)
            preprocessor.enabled = arabic;
    }

    private string FormatDisplay()
    {
        string dayText = showDay && currentDay > 0
            ? (showNumberOnly ? currentDay.ToString(CultureInfo.InvariantCulture) : DayCounterLocalization.Format(currentDay))
            : string.Empty;
        string timeText = showClock ? FormatTime(currentTimeOfDay, minuteInterval, use12Hour) : string.Empty;
        if (dayText.Length == 0) return timeText;
        return timeText.Length == 0 ? dayText : dayText + "\n" + timeText;
    }

    private static string FormatTime(float timeOfDay, int step, bool twelveHour)
    {
        if (float.IsNaN(timeOfDay) || float.IsInfinity(timeOfDay)) timeOfDay = 0f;
        int minute = (int)Math.Floor((timeOfDay - (float)Math.Floor(timeOfDay)) * 1440f + 0.0001f);
        minute = Mathf.Clamp(minute, 0, 1439);
        if (step > 1) minute -= minute % step;
        int hour = minute / 60;
        int minutes = minute % 60;
        if (!twelveHour) return hour.ToString("00", CultureInfo.InvariantCulture) + ":" + minutes.ToString("00", CultureInfo.InvariantCulture);
        return (hour % 12 == 0 ? 12 : hour % 12).ToString(CultureInfo.InvariantCulture) + ":" + minutes.ToString("00", CultureInfo.InvariantCulture) + (hour < 12 ? " AM" : " PM");
    }

    private static void CopyTextStyle(TextMeshProUGUI source, TextMeshProUGUI target)
    {
        target.font = source.font;
        target.fontSharedMaterial = source.fontSharedMaterial;
        target.fontSize = source.fontSize;
        target.fontStyle = source.fontStyle;
        target.alignment = source.alignment;
        target.color = source.color;
        target.enableWordWrapping = false;
        target.raycastTarget = false;
    }

    private static bool IsInGame()
    {
        try
        {
            return MainGame.Instance != null
                && (int)MainGame.Instance.gameState == 1
                && MainGame.Instance.GameSave != null
                && !IsCinematicActive();
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[GK2.DayCounter] game-state read failed: " + ex.Message);
            return false;
        }
    }

    private static bool IsCinematicActive()
    {
        UICinematic? cinematic = UnityEngine.Object.FindFirstObjectByType<UICinematic>();
        return cinematic != null && cinematic.gameObject.activeInHierarchy;
    }

    private static int ReadCurrentDay()
    {
        try { return MainGame.Instance?.GameSave?.environmentData?.Day ?? -1; }
        catch (Exception ex)
        {
            Debug.LogWarning("[GK2.DayCounter] day read failed: " + ex.Message);
            return -1;
        }
    }

    private static float ReadCurrentTimeOfDay()
    {
        try { return MainGame.Instance?.GameSave?.environmentData?.TimeOfDay ?? 0f; }
        catch (Exception ex)
        {
            Debug.LogWarning("[GK2.DayCounter] time read failed: " + ex.Message);
            return 0f;
        }
    }
}

internal sealed class DayCounterDragHandle : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private DayCounterBehaviour? owner;

    public void Initialize(DayCounterBehaviour behaviour) => owner = behaviour;

    public void OnBeginDrag(PointerEventData eventData) => owner?.BeginDrag(eventData);

    public void OnDrag(PointerEventData eventData) => owner?.Drag(eventData);

    public void OnEndDrag(PointerEventData eventData) => owner?.EndDrag();

    private void OnDisable() => owner?.EndDrag();
}

internal static class DayCounterLocalization
{
    [Serializable]
    private sealed class LocaleFile { public string day = "Day {0}"; public string moveHint = "Esc: finish moving"; }
    [Serializable]
    private sealed class LocaleAliases { public LocaleAlias[] aliases = Array.Empty<LocaleAlias>(); }
    [Serializable]
    private sealed class LocaleAlias { public string language = string.Empty; public string locale = string.Empty; }

    private static BepInEx.Logging.ManualLogSource? logger;

    public static void SetLogger(BepInEx.Logging.ManualLogSource source) => logger = source;

    private static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["en-us"] = "en", ["english"] = "en",
        ["fr-fr"] = "fr", ["french"] = "fr",
        ["de-de"] = "de", ["german"] = "de",
        ["es-es"] = "es", ["es-mx"] = "es", ["spanish"] = "es",
        ["pt"] = "pt-br", ["pt-br"] = "pt-br", ["pt_br"] = "pt-br",
        ["ru-ru"] = "ru", ["russian"] = "ru",
        ["uk"] = "uk-ua", ["uk-ua"] = "uk-ua", ["uk_ua"] = "uk-ua",
        ["uk_vo"] = "uk-ua", ["ukrainian-voiceover"] = "uk-ua",
        ["it-it"] = "it", ["italian"] = "it", ["italiano"] = "it",
        ["pl-pl"] = "pl", ["polish"] = "pl",
        ["tr-tr"] = "tr", ["turkish"] = "tr",
        ["ja-jp"] = "ja", ["japanese"] = "ja",
        ["ko-kr"] = "ko", ["korean"] = "ko",
        ["zh-cn"] = "zh_cn", ["zh_cn"] = "zh_cn", ["simplified-chinese"] = "zh_cn",
        ["zh-tw"] = "zh_cht", ["zh-cht"] = "zh_cht", ["zh_cht"] = "zh_cht", ["traditional-chinese"] = "zh_cht",
        ["vi"] = "vn", ["vi-vn"] = "vn", ["vi_vn"] = "vn", ["vn"] = "vn",
        ["vietnamese"] = "vn", ["tiếng việt"] = "vn", ["tieng viet"] = "vn",
        ["th-th"] = "th", ["thai"] = "th",
        ["3806720282"] = "id", ["id"] = "id", ["id-id"] = "id", ["id_id"] = "id", ["indonesian"] = "id",
        ["bahasa indonesia"] = "id", ["bahasa-indonesia"] = "id", ["bahasa_indonesia"] = "id",
        ["ar-sa"] = "ar", ["arabic"] = "ar",
        ["3807120067"] = "cs", ["cs-cz"] = "cs", ["cs_cz"] = "cs", ["cz"] = "cs", ["czech"] = "cs",
        ["čeština"] = "cs", ["cestina"] = "cs"
    };

    private static string cachedLanguage = string.Empty;
    private static string cachedLocaleKey = "en";
    private static string template = "Day {0}";
    private static Dictionary<string, string>? externalAliases;
    public static bool IsRightToLeft => cachedLanguage.StartsWith("ar", StringComparison.OrdinalIgnoreCase);
    public static string MoveHint
    {
        get
        {
            string language = CurrentLanguage();
            if (!string.Equals(language, cachedLanguage, StringComparison.OrdinalIgnoreCase)) Load(language);
            return Read(cachedLocaleKey)?.moveHint ?? "Esc: finish moving";
        }
    }

    public static string Format(int day)
    {
        string language = CurrentLanguage();
        if (!string.Equals(language, cachedLanguage, StringComparison.OrdinalIgnoreCase)) Load(language);
        try { return string.Format(template, day); }
        catch { return "Day " + day; }
    }

    private static string CurrentLanguage()
    {
        try
        {
            string? language = LLBase.CurrentLang;
            if (!string.IsNullOrWhiteSpace(language)) return language.Trim().ToLowerInvariant();
        }
        catch { }
        return "en";
    }

    private static void Load(string language)
    {
        cachedLanguage = language;
        string trimmed = language.Trim();
        externalAliases ??= ReadAliases();
        string normalized = externalAliases.TryGetValue(trimmed, out string? customAlias)
            ? customAlias
            : Aliases.TryGetValue(trimmed, out string? alias) ? alias : trimmed.ToLowerInvariant();
        cachedLocaleKey = normalized;
        LocaleFile? locale = Read(normalized);
        bool localeFileFound = locale != null;
        string loadedLocale = normalized;
        if (locale == null)
        {
            locale = Read("en");
            loadedLocale = "en (fallback)";
        }
        template = locale?.day ?? "Day {0}";
        string testText;
        try { testText = string.Format(template, 7); }
        catch { testText = "<format-error>"; }
        logger?.LogInfo($"Locale selected: gameLanguage='{language}', localeKey='{normalized}', loadedLocale='{loadedLocale}', template='{template}', sample='{testText}', localeFileFound={localeFileFound}.");
    }

    private static Dictionary<string, string> ReadAliases()
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            string? pluginDirectory = Path.GetDirectoryName(typeof(DayCounterPlugin).Assembly.Location);
            if (string.IsNullOrEmpty(pluginDirectory)) return result;
            string file = Path.Combine(pluginDirectory, "Locales", "language-map.json");
            if (!File.Exists(file)) return result;
            LocaleAliases? map = JsonUtility.FromJson<LocaleAliases>(File.ReadAllText(file));
            if (map?.aliases != null)
                foreach (LocaleAlias entry in map.aliases)
                    if (!string.IsNullOrWhiteSpace(entry.language) && !string.IsNullOrWhiteSpace(entry.locale))
                        result[entry.language.Trim()] = entry.locale.Trim();
        }
        catch (Exception ex) { Debug.LogWarning("[GK2.DayCounter] language map read failed: " + ex.Message); }
        return result;
    }

    private static LocaleFile? Read(string language)
    {
        try
        {
            string? pluginDirectory = Path.GetDirectoryName(typeof(DayCounterPlugin).Assembly.Location);
            if (!string.IsNullOrEmpty(pluginDirectory))
            {
                string bundledLocale = Path.Combine(pluginDirectory, "Locales", language + ".json");
                if (File.Exists(bundledLocale))
                    return JsonUtility.FromJson<LocaleFile>(File.ReadAllText(bundledLocale));
            }

            string? gameRoot = Path.GetDirectoryName(Application.dataPath);
            if (string.IsNullOrEmpty(gameRoot)) return null;
            string file = Path.Combine(gameRoot, "Languages", "gk2daycounter_locales", language + ".json");
            return File.Exists(file) ? JsonUtility.FromJson<LocaleFile>(File.ReadAllText(file)) : null;
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[GK2.DayCounter] locale read failed: " + ex.Message);
            return null;
        }
    }
}
