using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using MonoDetour;
using MonoDetour.HookGen;
using PEAKLib.Core;
using PEAKLib.ModConfig.Components;
using PEAKLib.UI;
using PEAKLib.UI.Elements;
using pworld.Scripts.Extensions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zorro.UI;
using Language = LocalizedText.Language;

namespace PEAKLib.ModConfig;

/// <summary>
/// BepInEx plugin of PEAKLib.ModPlugin.
/// Depend on this with <c>[BepInDependency(ModConfigPlugin.Id)]</c>.
/// </summary>
[BepInAutoPlugin]
[BepInDependency(CorePlugin.Id)]
[BepInDependency(UIPlugin.Id)]
public partial class ModConfigPlugin : BaseUnityPlugin
{
    internal static ManualLogSource Log { get; } = BepInEx.Logging.Logger.CreateLogSource(Name);
    private static List<ConfigEntryBase> EntriesProcessed { get; set; } = [];
    internal static ModConfigPlugin instance = null!;
    internal InputBindingCaptureService InputBindingCapture { get; private set; } = null!;

    // localizations
    private static TranslationKey? ModSettingsLoc;

    private static UIPage? settingsParent;
    private static PauseMenuControlsPage? controlsParent;

    private void Awake()
    {
        instance = this;
        InputBindingCapture =
            GetComponent<InputBindingCaptureService>()
            ?? gameObject.AddComponent<InputBindingCaptureService>();
        MonoDetourManager.InvokeHookInitializers(typeof(ModConfigPlugin).Assembly);
        Log.LogInfo($"Plugin {Name} is loaded!");
    }

    // AutoReload support
    private void OnDestroy()
    {
        // Dispose all hooks
        DefaultMonoDetourManager.Instance.Dispose();
        Log.LogInfo($"Plugin {Name} unloaded!");
    }

    private void Start()
    {
        // refresh player control overrides for keybinds, vanilla does this later when the controls page becomes available
        Rebinding.LoadRebindingsFromFile();

        LoadModConfigLocalizations();
        LoadModSettings();

        // delegate for Mod Settings Page
        void builderDelegate(Transform parent)
        {
            Log.LogDebug("Mod Settings builderDelegate");
            var mainMenuHandler = parent.GetComponentInParent<MainMenuPageHandler>();
            var pauseMenuHandler = parent.GetComponentInParent<PauseMenuHandler>();

            if (mainMenuHandler == null && pauseMenuHandler == null)
                throw new Exception("Failed to get a UIPageHandler");

            settingsParent = (
                mainMenuHandler?.GetPage<MainMenuSettingsPage>()
                ?? pauseMenuHandler
                    ?.transform.Find("SettingsPage")
                    ?.GetComponent<PauseMenuSettingsMenuPage>()
            );

            ThrowHelper.ThrowIfArgumentNull(settingsParent);
            ThrowHelper.ThrowIfArgumentNull(ModSettingsLoc);

            var modSettingsPage = MenuAPI.CreateChildPage("ModSettings", settingsParent);

            if (mainMenuHandler != null) // we are on main menu, create a background
                modSettingsPage.CreateBackground(new Color(0, 0, 0, 0.8667f));

            modSettingsPage.SetOnOpen(() =>
            {
                //Double check if any config items have been created since initialization
                ProcessModEntries();
            });

            var headerContainer = new GameObject("Header")
                .ParentTo(modSettingsPage)
                .AddComponent<PeakElement>()
                .SetAnchorMinMax(new Vector2(0, 1))
                .SetPosition(new Vector2(40, -40))
                .SetPivot(new Vector2(0, 1))
                .SetSize(new Vector2(360, 100));

            var newText = MenuAPI
                .CreateText("Mod Settings", "HeaderText")
                .SetFontSize(48)
                .ParentTo(headerContainer)
                .ExpandToParent()
                .SetLocalizationIndex(ModSettingsLoc);

            newText.Text.fontSizeMax = 48;
            newText.Text.fontSizeMin = 24;
            newText.Text.enableAutoSizing = true;
            newText.Text.alignment = TextAlignmentOptions.Center;

            var backButton = MenuAPI
                .CreateMenuButton("Back")
                .SetLocalizationIndex("BACK") // Peak already have a "BACK" official translation, so let's just use it
                .SetColor(new Color(0.5189f, 0.1297f, 0.1718f)) //match vanilla back
                .ParentTo(modSettingsPage)
                .SetPosition(new Vector2(120f, -160f))
                .SetWidth(120f);

            // Vanilla SettingsPageShared/Content stretches with the page. Its offsets round to
            // left 428, right 131, top 70, bottom 31 UI units in both main and pause menus.
            var content = new GameObject("Content")
                .AddComponent<PeakElement>()
                .ParentTo(modSettingsPage)
                .ExpandToParent()
                .SetOffsetMin(new Vector2(428f, 31f))
                .SetOffsetMax(new Vector2(-131f, -70f));

            var settingsMenu = content.gameObject.AddComponent<ModSettingsMenu>();
            settingsMenu.MainPage = modSettingsPage;

            settingsMenu.FilterDropdown = MenuAPI
                .CreateDropdown("Settings Filter", settingsMenu.transform)
                .SetOptions(["Bools", "Strings", "Numbers", "Enums", "Controls"])
                .SetLabelColor(Color.wheat)
                .SetBackgroundColor(Color.slateBlue)
                .SetArrowColor(Color.wheat)
                .ParentTo(modSettingsPage)
                .SetMultiSelect(true)
                .SetInitialValue(31) // all bits selected
                .OnValueChanged(settingsMenu.SetFilter)
                .SetSize(new(200f, 70f))
                .SetPosition(new Vector2(285f, -160f)); // position needs to be set last

            MenuAPI
                .CreateText("Search")
                .ParentTo(modSettingsPage)
                .SetPosition(new Vector2(65f, -190f));

            var textInput = MenuAPI
                .CreateTextInput("SearchInput")
                .ParentTo(modSettingsPage)
                .SetSize(new Vector2(300, 70))
                .SetPosition(new Vector2(215, -275))
                .SetPlaceholder("Search here")
                .OnValueChanged(settingsMenu.SetSearch);

            modSettingsPage.SetBackButton(backButton.GetComponent<Button>()); // sadly backButton.Button doesn't work cause Awake have not being called yet

            var horizontalTabs = new GameObject("TABS")
                .ParentTo(content)
                .AddComponent<PeakHorizontalTabs>();
            // PeakHorizontalTabs already uses vanilla's top stretch anchors and 40-unit height.
            // Inset only the left edge for the label; keep the right edge inside Content.
            horizontalTabs.RectTransform.offsetMin = new Vector2(110f, -40f);
            horizontalTabs.RectTransform.offsetMax = Vector2.zero;

            var modTabsLabel = MenuAPI.CreateText("MODS").ParentTo(content).SetPosition(new(4, 10));

            var sectionTabsLabel = MenuAPI
                .CreateText("SECTIONS")
                .ParentTo(content)
                .SetPosition(new(4, -50));

            var sectionTabs = new GameObject("SectionTabs")
                .ParentTo(content)
                .AddComponent<PeakHorizontalTabs>();
            sectionTabs.RectTransform.offsetMin = new Vector2(175f, -95f);
            sectionTabs.RectTransform.offsetMax = new Vector2(0, -55f);

            var moddedSettingsTABS = horizontalTabs.gameObject.AddComponent<ModdedSettingsTABS>();
            moddedSettingsTABS.SettingsMenu = settingsMenu;

            var modSectionTABS = sectionTabs.gameObject.AddComponent<ModdedSettingsSectionTABS>();
            modSectionTABS.SettingsMenu = settingsMenu;
            settingsMenu.ModTabController = horizontalTabs;
            settingsMenu.SectionTabController = sectionTabs;

            var tabContent = MenuAPI
                .CreateScrollableContent("TabContent")
                .ParentTo(content)
                .ExpandToParent()
                // Vanilla's settings parent reserves 61.85 units above the list;
                // round to 62 and add 55 for our extra section tab row.
                .SetOffsetMax(new Vector2(0, -117f));

            settingsMenu.Content = tabContent.Content;
            settingsMenu.ModTabs = moddedSettingsTABS;
            settingsMenu.SectionTabs = modSectionTABS;

            foreach (var mod in ModSectionNames.SectionNames)
            {
                var modName = mod.ModName;
                var tabButton = horizontalTabs.AddTab(modName);
                var moddedButton = tabButton.AddComponent<ModdedTABSButton>();
                moddedButton.category = modName;
                moddedButton.text = tabButton.GetComponentInChildren<TextMeshProUGUI>();
                moddedButton.SelectedGraphic = tabButton.transform.Find("Selected").gameObject;
            }

            var modSettingsButton = MenuAPI
                .CreatePauseMenuButton("MOD SETTINGS")
                .SetLocalizationIndex(ModSettingsLoc)
                .SetColor(new Color(0.185f, 0.394f, 0.6226f)) //same blue as main menu settings button
                .ParentTo(parent)
                .OnClick(() =>
                {
                    // reset filter view to includ everything
                    ModSettingsMenu.Instance.FilterDropdown.Dropdown.value = 31;
                    ModSettingsMenu.Instance.FilterValue = 31;
                    // set parent to settings menu
                    ModSettingsMenu.Instance.MainPage.SetParentPage(settingsParent);

                    var handler = mainMenuHandler as UIPageHandler ?? pauseMenuHandler;

                    handler?.TransistionToPage(modSettingsPage, new SetActivePageTransistion());
                });

            modSettingsPage.gameObject.SetActive(false);

            modSettingsButton?.SetPosition(new Vector2(171, -230)).SetWidth(220);
        }

        void modControls(Transform parent)
        {
            ThrowHelper.ThrowIfArgumentNull(ModSettingsLoc);

            var pauseMenuHandler = parent.GetComponentInParent<PauseMenuHandler>();
            // reposition back button to better fit our new button
            var back = parent.Find("UI_MainMenuButton_LeaveGame (3)").GetComponent<RectTransform>();
            back.anchoredPosition = new(150f, -222.9999f);
            back.sizeDelta = new(120f, 67f);

            controlsParent = parent.GetComponentInChildren<PauseMenuControlsPage>();

            ThrowHelper.ThrowIfArgumentNull(pauseMenuHandler);

            var modSettingsButton = MenuAPI
                .CreatePauseMenuButton("MOD SETTINGS")
                .SetLocalizationIndex(ModSettingsLoc)
                .SetColor(new Color(0.185f, 0.394f, 0.6226f)) //same blue as main menu settings button
                .ParentTo(parent)
                .SetPosition(new(305f, -222.9999f))
                .SetWidth(190f)
                .OnClick(() =>
                {
                    // set filter view to controls only and open settings
                    ModSettingsMenu.Instance.FilterDropdown.Dropdown.value = 16;
                    ModSettingsMenu.Instance.FilterValue = 16;
                    ModSettingsMenu.Instance.MainPage.SetParentPage(controlsParent);
                    pauseMenuHandler?.TransistionToPage(
                        ModSettingsMenu.Instance.MainPage,
                        new SetActivePageTransistion()
                    );
                });
        }

        //settings menu builder
        MenuAPI.AddToSettingsMenus(builderDelegate);
        //controls menu builder
        MenuAPI.AddToControlsMenu(modControls);
    }

    private static void LoadModConfigLocalizations()
    {
        // Vanilla Localization Keys:       CURRENT_LANGUAGE,English,Français,Italiano,Deutsch,Español (España),Español (LatAm),Português (BR),Русский,Українська,简体中文,繁体中文,日本語,한국어,Polski,Türkçe,ENDLINE
        // Vanilla Controls Localization:   CONTROLS,CONTROLS,COMMANDES,COMANDI,STEUERUNG,CONTROLES,CONTROLES,CONTROLES,УПРАВЛЕНИЕ,КЕРУВАННЯ,操作方式,控制,コントロール,조작 방법,STEROWANIE,KONTROLLER,ENDLINE
        // Vanilla Settings Localization:   SETTINGS,SETTINGS,PARAMÈTRES,IMPOSTAZIONI,EINSTELLUNGEN,AJUSTES,AJUSTES,CONFIGURAÇÕES,НАСТРОЙКИ,НАЛАШТУВАННЯ,设置,設定,設定,설정,USTAWIENIA,AYARLAR,ENDLINE

        // Below translations are best effort, if a better translation exists please feel free to offer corrections

        ModSettingsLoc = MenuAPI
            .CreateLocalization("MOD SETTINGS")
            .AddLocalization("MOD SETTINGS", Language.English)
            .AddLocalization("PARAMÈTRES DU MOD", Language.French)
            .AddLocalization("IMPOSTAZIONI MOD", Language.Italian)
            .AddLocalization("MOD-EINSTELLUNGEN", Language.German)
            .AddLocalization("AJUSTES DEL MOD", Language.SpanishSpain)
            .AddLocalization("CONFIGURACIONES DEL MOD", Language.SpanishLatam)
            .AddLocalization("CONFIGURAÇÕES DE MOD", Language.BRPortuguese)
            .AddLocalization("НАСТРОЙКИ МОДА", Language.Russian)
            .AddLocalization("НАЛАШТУВАННЯ МОДА", Language.Ukrainian)
            .AddLocalization("模组设置", Language.SimplifiedChinese)
            .AddLocalization("模組設定", Language.TraditionalChinese)
            .AddLocalization("MOD設定", Language.Japanese)
            .AddLocalization("모드 설정", Language.Korean)
            .AddLocalization("USTAWIENIA MODÓW", Language.Polish)
            .AddLocalization("MOD AYARLAR", Language.Turkish);

        // Not used, perhaps will in the future so leaving these comments
        /*
        ModControlsLoc = MenuAPI
                .CreateLocalization("MOD CONTROLS")
                .AddLocalization("MOD CONTROLS", Language.English)
                .AddLocalization("COMMANDES DU MOD", Language.French)
                .AddLocalization("COMANDI MOD", Language.Italian)
                .AddLocalization("MOD-STEUERUNG", Language.German)
                .AddLocalization("CONTROLES DEL MOD", Language.SpanishSpain)
                .AddLocalization("CONTROLES DEL MOD", Language.SpanishLatam)
                .AddLocalization("CONTROLES DE MOD", Language.BRPortuguese)
                .AddLocalization("УПРАВЛЕНИЕ MOD", Language.Russian)
                .AddLocalization("КЕРУВАННЯ MOD", Language.Ukrainian)
                .AddLocalization("MOD 操作方式", Language.SimplifiedChinese)
                .AddLocalization("MOD 控制", Language.TraditionalChinese)
                .AddLocalization("MODコントロール", Language.Japanese)
                .AddLocalization("MOD 조작 방법", Language.Korean)
                .AddLocalization("STEROWANIE MODÓW", Language.Polish)
                .AddLocalization("MOD KONTROLLER", Language.Turkish); */
    }

    private static bool modSettingsLoaded = false;

    private static void LoadModSettings()
    {
        if (modSettingsLoaded)
            return;

        EntriesProcessed = [];
        modSettingsLoaded = true;

        ProcessModEntries();
    }

    //Processes Bepinex config items to Settings entries
    //Called during 1) mod initialization 2) mod settings page is opened 3) null plugin instances are found
    internal static void ProcessModEntries()
    {
        foreach (var (plugin, configEntryBases) in GetModConfigEntries())
        {
            var modName = FixNaming(plugin.Metadata.Name);
            ModSectionNames sectionTracker = ModSectionNames.SetMod(modName);

            foreach (var configEntry in configEntryBases)
            {
                try
                {
                    //track mod entries we have processed to not duplicate setting entries
                    if (EntriesProcessed.Contains(configEntry))
                        continue;
                    else
                        EntriesProcessed.Add(configEntry);

                    sectionTracker.CheckSectionName(configEntry.Definition.Section);

                    if (configEntry.SettingType == typeof(bool))
                        SettingsHandlerUtility.AddBoolToTab(
                            configEntry,
                            plugin,
                            modName,
                            newVal => configEntry.BoxedValue = newVal
                        );
                    else if (configEntry.SettingType == typeof(float))
                    {
                        SettingsHandlerUtility.AddFloatToTab(
                            configEntry,
                            plugin,
                            modName,
                            newVal => configEntry.BoxedValue = newVal
                        );
                    }
                    else if (configEntry.SettingType == typeof(double))
                    {
                        SettingsHandlerUtility.AddDoubleToTab(
                            configEntry,
                            plugin,
                            modName,
                            newVal => configEntry.BoxedValue = newVal
                        );
                    }
                    else if (configEntry.SettingType == typeof(int))
                    {
                        SettingsHandlerUtility.AddIntToTab(
                            configEntry,
                            plugin,
                            modName,
                            newVal => configEntry.BoxedValue = newVal
                        );
                    }
                    else if (configEntry.SettingType == typeof(string))
                    {
                        var defaultValue = configEntry.DefaultValue is string cValue ? cValue : "";
                        bool isInputPath = InputBindingPath.IsValid(defaultValue);

                        //checking if default value matches key path pattern
                        if (isInputPath)
                        {
                            SettingsHandlerUtility.AddKeyPathToTab(
                                configEntry,
                                plugin,
                                modName,
                                newVal => configEntry.BoxedValue = newVal
                            );
                            continue;
                        }

                        //dropdown box for acceptablevalue list
                        if (
                            configEntry.Description.AcceptableValues
                            is AcceptableValueList<string> stringList
                        )
                        {
                            SettingsHandlerUtility.AddEnumToTab(
                                configEntry,
                                plugin,
                                modName,
                                false,
                                newVal =>
                                {
                                    configEntry.BoxedValue = newVal;
                                }
                            );
                        }
                        else
                        {
                            SettingsHandlerUtility.AddStringToTab(
                                configEntry,
                                plugin,
                                modName,
                                newVal => configEntry.BoxedValue = newVal
                            );
                        }
                    }
                    else if (configEntry.SettingType == typeof(KeyCode))
                    {
                        SettingsHandlerUtility.AddKeybindToTab(
                            configEntry,
                            plugin,
                            modName,
                            newVal => configEntry.BoxedValue = newVal
                        );
                    }
                    else if (configEntry.SettingType.IsEnum)
                    {
                        SettingsHandlerUtility.AddEnumToTab(
                            configEntry,
                            plugin,
                            modName,
                            true,
                            newVal =>
                            {
                                if (Enum.TryParse(configEntry.SettingType, newVal, out var value))
                                    configEntry.BoxedValue = value;
                            }
                        );
                    }
                    else // Warn about missing SettingTypes
                        Log.LogWarning(
                            $"Missing SettingType: [Mod: {modName}] {configEntry.Definition.Key} (Type: {configEntry.SettingType})"
                        );
                }
                catch (Exception e)
                {
                    Log.LogError(e);
                }
            }
        }
    }

    // From https://github.com/IsThatTheRealNick/REPOConfig/blob/main/REPOConfig/ConfigMenu.cs#L453
    // modified to capture plugin instances directly instead of just mod names
    private static Dictionary<PluginInfo, ConfigEntryBase[]> GetModConfigEntries()
    {
        var configs = new Dictionary<PluginInfo, ConfigEntryBase[]>();

        foreach (var plugin in Chainloader.PluginInfos.Values.OrderBy(p => p.Metadata.Name))
        {
            var configEntries = new List<ConfigEntryBase>();

            foreach (
                var configEntryBase in plugin.Instance.Config.Select(configEntry =>
                    configEntry.Value
                )
            )
            {
                var tags = configEntryBase.Description?.Tags;

                if (tags != null && tags.Contains("Hidden"))
                    continue;

                configEntries.Add(configEntryBase);
            }

            if (configEntries.Count > 0)
                configs.TryAdd(plugin, [.. configEntries]);
        }

        return configs;
    }

    private static string FixNaming(string input)
    {
        input = Regex.Replace(input, "([a-z])([A-Z])", "$1 $2");
        input = Regex.Replace(input, "([A-Z])([A-Z][a-z])", "$1 $2");
        input = Regex.Replace(input, @"\s+", " ");
        input = Regex.Replace(input, @"([A-Z]\.)\s([A-Z]\.)", "$1$2");

        return input.Trim();
    }
}
