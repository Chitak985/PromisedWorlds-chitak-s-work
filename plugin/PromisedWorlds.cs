// Promised Worlds Settings GUI (PromisedWorlds.dll)
// Copyright © 2026 averageksp
// All rights reserved. See PluginLicense.txt.

using KSP.Localization;
using KSP.UI.Screens;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace PromisedWorlds
{
    [KSPAddon(KSPAddon.Startup.EveryScene, false)]
    public class PromisedWorldsMenu : MonoBehaviour
    {
        private static ApplicationLauncherButton toolbarButton;
        private bool showUI = false;
        private Rect windowRect = new Rect(Screen.width / 2 - 175, Screen.height / 2 - 250, 350, 460);
        private bool showCredits = false;
        private Rect creditsRect = new Rect(Screen.width / 2 - 175, Screen.height / 2 - 190, 350, 380);
        private Vector2 creditsScrollPos = Vector2.zero;
        private Vector2 settingsScrollPos = Vector2.zero;
        private Vector2 dependencyScrollPos = Vector2.zero;

        private bool isFirstRun = false;

        // Prefixed before each log message
        private string logPrefix = "[PromisedWorlds] ";

        private string settingsPath = "GameData/PromisedWorlds/PromisedWorldsSettings.cfg";
        private Dictionary<string, string> settings = new Dictionary<string, string>();
        private Dictionary<string, string> defaultSettings = new Dictionary<string, string>()
        {
            { "Wormholes", "False" },
            { "RealisticStarSize", "False" },
            { "RemoveStockScreens", "True" },
            { "Skybox", "True" },
            { "DebugReload", "False" },
            { "DistanceFactor", "1" },
            { "Rescale", "1" }
        };
        private bool settingsLoaded = false;
        private string logPath;

        private bool rssDetected, jnsqDetected, singularityDetected = false;

        private string eveVersion = null;
        private bool eveReduxInstalled = false;
        private bool showEVEWarning = false;
        // Loaded from plugin config - if the user has clicked don't show again for the EVE warning
        private bool suppressEVEWarning = false;
        private string suppressedEVEVersion = null;

        private Rect eveWarningRect = new Rect(Screen.width / 2 - 200, Screen.height / 2 - 100, 400, 250);

        private bool showSingularityWarning = false;
        private Rect singularityWarningRect = new Rect(Screen.width / 2 - 200, Screen.height / 2 - 100, 400, 150);

        private bool showDependencyWarning = false;
        private bool showDebdebSystemWarning = false;
        private bool showMisplacedSystemsWarning = false;
        private Rect missingDependenciesWarningRect = new Rect(Screen.width / 2 - 100, Screen.height / 2 - 50, 700, 500);
        private Rect debdebSystemWarningRect = new Rect(Screen.width / 2 - 250, Screen.height / 2 - 150, 500, 330);
        private Rect misplacedSystemsWarningRect = new Rect(Screen.width / 2 - 250, Screen.height / 2 - 150, 500, 330);
        private List<(string, string, string)> missingDependencies = new List<(string, string, string)>();
        private List<string> misplacedSystems = new List<string>();

        private bool showWelcome = false;
        private Rect welcomeRect = new Rect(Screen.width / 2 - 260, Screen.height / 2 - 210, 520, 420);
        private string pluginConfigPath;

        // welcome Screen Icons
        private Texture2D wikiIcon;
        private Texture2D discordIcon;
        private Texture2D githubIcon;
        private Texture2D forumIcon;

        // configurable Welcome screen data
        private Dictionary<string, string> pluginConfig = new Dictionary<string, string>();

        // links and changelog Windows
        private bool showLinks = false;
        private Rect linksRect = new Rect(Screen.width / 2 - 250, Screen.height / 2 - 150, 500, 280);
        private bool showChangelog = false;
        private Rect changelogRect = new Rect(Screen.width / 2 - 300, Screen.height / 2 - 300, 600, 600);
        private Vector2 changelogScrollPos = Vector2.zero;

        // Loadables
        private string changelogPath = "GameData/PromisedWorlds/Changelog.md";
        private string changelogContent = "";
        private bool changelogLoaded = false;

        private string creditsPath = "GameData/PromisedWorlds/Misc/Plugin/credits.txt";
        private string creditsContent = "";
        private bool creditsLoaded = false;

        private string defaultSettingsPath = "GameData/PromisedWorlds/Misc/Plugin/defaultSettings.txt";
        private string defaultSettingsContent = "";
        private bool defaultSettingsLoaded = false;

        // Version Info when Not in Main menu
        private string coreVersion = "Not Installed";
        private string debdebVersion = "Not Installed";
        private string tuunVersion = "Not Installed";

        private void Start()
        {
            Log("PromisedWorlds loaded in " + HighLogic.LoadedScene);

            logPath = KSPUtil.ApplicationRootPath + "GameData/PromisedWorlds/PromisedWorlds.log";
            pluginConfigPath = KSPUtil.ApplicationRootPath + "GameData/PromisedWorlds/Misc/Plugin/Plugin.cfg";

            // Load plugin settings like first run status and whether the EVE warning has been dismissed
            LoadPluginSettings();
            LoadWelcomeIcons();
            LoadChangelog();
            LoadCredits();
            LoadVersionInfo();
            CheckDependencies();
            CheckMisplacedSystems();

            // Only Do the heavy Stuff in main Menu
            if (HighLogic.LoadedScene == GameScenes.MAINMENU)
            {
                LoadDefaultSettings();
                LoadSettings();
                DetectModFolders();
                CheckEnvironmentalVisualEnhancements();
                GenerateLog();
            }
        }

        private void LoadSettings()
        {
            string fullPath = KSPUtil.ApplicationRootPath + settingsPath;
            if (File.Exists(fullPath))
            {
                foreach (string line in File.ReadAllLines(fullPath))
                {
                    string trimmed = line.Trim();
                    if (trimmed.Length == 0 || trimmed.StartsWith("//") || trimmed.StartsWith("{") || trimmed.StartsWith("}"))
                        continue;

                    string[] parts = trimmed.Split('=');
                    if (parts.Length < 2) continue;
                    string key = parts[0].Trim();
                    string value = parts[1].Split('/')[0].Trim();
                    if (!settings.ContainsKey(key))
                        settings[key] = value;
                }
                settingsLoaded = true;
            }
            else
            {
                ScreenMessages.PostScreenMessage("PromisedWorldsSettings.cfg missing!", 5f, ScreenMessageStyle.UPPER_CENTER);
                Log("PromisedWorldsSettings.cfg missing from " + fullPath);

                Log("Attempting to write default settings.");

                // Attempt to write default settings to file
                if(defaultSettingsLoaded) {
                    try
                    {
                        File.WriteAllText(fullPath, defaultSettingsContent);
                        Log("Wrote default settings to settings file at " + fullPath);
                        ScreenMessages.PostScreenMessage("Wrote defaults to PromisedWorldsSettings.cfg!", 5f, ScreenMessageStyle.UPPER_CENTER);
                        settingsLoaded = true;
                    } catch(Exception e) {
                        LogError("Caught exception trying to write default settings:\n" + e);
                    }
                    settingsLoaded = false;
                } else {
                    LogError("Default settings not loaded! Cannot write default settings.");
                    settingsLoaded = false;
                }
            }
        }

        private void DetectModFolders()
        {
            string gameDataPath = Path.Combine(KSPUtil.ApplicationRootPath, "GameData");
            rssDetected = Directory.Exists(Path.Combine(gameDataPath, "RealSolarSystem"));
            jnsqDetected = Directory.Exists(Path.Combine(gameDataPath, "JNSQ"));
            singularityDetected = Directory.Exists(Path.Combine(gameDataPath, "Singularity"));

            bool needsSave = false;
            if (settings.ContainsKey("Rescale"))
            {
                float rescaleVal = float.Parse(settings["Rescale"], CultureInfo.InvariantCulture);
                if (rssDetected && rescaleVal != 10f)
                {
                    settings["Rescale"] = "10";
                    needsSave = true;
                    Log("RSS detected, setting Rescale to 10x");
                }
                if (jnsqDetected && Math.Abs(rescaleVal - 2.5f) > 0.001f)
                {
                    settings["Rescale"] = "2.5";
                    needsSave = true;
                    Log("JNSQ detected, setting Rescale to 2.5x");
                }
            }
            if (needsSave)
            {
                SaveSettings();
                Log("Saved settings.");
            }
        }

        private void CheckDependencies()
        {
            // ModuleManager check - MM filename is different between versions, so need to search GameData.
            string gameDataPath = Path.Combine(KSPUtil.ApplicationRootPath, "GameData");
            System.Collections.Generic.IEnumerable<string> moduleManagerFiles = Directory.EnumerateFiles(gameDataPath, searchPattern: "ModuleManager.*.*.*.dll");
            int files = 0;
            foreach (string file in moduleManagerFiles)
            {
                Log("ModuleManager present at " + file);
                files++;
            }

            if(files > 1)
            {
                LogWarning("Multiple ModuleManager files found.");
            } 
            else if (files < 1)
            {
                LogError("ModuleManager not found!");
                (string, string, string) mmDep = ("ModuleManager", "ModuleManager", "https://ksp.sarbian.com/jenkins/job/ModuleManager/");
                missingDependencies.Add(mmDep);
            }
            
            // List of dependencies as tuples of string
            // (Display name, PathInGameData, URL)
            List<(string, string, string)> dependencies = new List<(string, string, string)>();
            // Kopernicus
            dependencies.Add(("Kopernicus", "Kopernicus", "https://github.com/Kopernicus/Kopernicus/releases"));
            // Harmony
            dependencies.Add(("HarmonyKSP", "000_Harmony", "https://github.com/KSPModdingLibs/HarmonyKSP/releases"));
            // ModularFlightIntegrator
            dependencies.Add(("ModularFlightIntegrator", "ModularFlightIntegrator", "https://ksp.sarbian.com/jenkins/job/ModularFlightIntegrator/"));
            // KSPTextureLoader
            dependencies.Add(("KSP Texture Loader", "KSPTextureLoader", "https://github.com/Phantomical/KSPTextureLoader/releases"));
            // Kopernicus Expansion
            dependencies.Add(("Kopernicus Expansion", "KopernicusExpansion", "https://github.com/VabienArt/KopernicusExpansion-Continueder/releases"));
            // KSP Community Fixes
            dependencies.Add(("KSP Commnunity Fixes", "KSPCommunityFixes", "https://github.com/KSPModdingLibs/KSPCommunityFixes/releases"));
            // Mitchell-Netravali Filtered Heightmap
            dependencies.Add(("Mitchell-Netravali Filtered Heightmap", "000_NiakoUtils/MitchellNetravali", "https://github.com/pkmniako/Kopernicus_VertexMitchellNetravaliHeightMap/releases"));
            // Vertex Height Oblate Advanced
            dependencies.Add(("Vertex Height Oblate Advanced", "001_DuckweedUtils/VertexHeightOblateAdvanced", "https://github.com/jamespglaze/VertexHeightOblateAdvanced/releases"));
            // Vertex Color Map Emissive
            dependencies.Add(("Vertex Color Map Emissive", "001_DuckweedUtils/VertexColorMapEmissive", "https://github.com/jamespglaze/VertexColorMapEmissive/releases"));
            // ScaledDecorator and Singularity are not checked here, as they are optional

            foreach ((string, string, string) dependency in dependencies)
            {
                string dependencyPath = Path.Combine(KSPUtil.ApplicationRootPath, "GameData", dependency.Item2);
                if(!Directory.Exists(dependencyPath))
                {
                    LogError("Missing dependency directory - " + dependency.Item2);
                    missingDependencies.Add(dependency);
                }
            }

            if(missingDependencies.Count > 0)
            {
                LogError("One or more dependencies is missing!");
                showDependencyWarning = true;
            }

        }

        private void CheckEnvironmentalVisualEnhancements()
        {
            string evePath = Path.Combine(KSPUtil.ApplicationRootPath, "GameData/EnvironmentalVisualEnhancements/EnvironmentalVisualEnhancements.version");
            if (!File.Exists(evePath))
            {
                Log("EVE not detected.");
                return;
            }

            string json = File.ReadAllText(evePath);
            string major = ExtractJsonValue(json, "\"MAJOR\"");
            string minor = ExtractJsonValue(json, "\"MINOR\"");
            string patch = ExtractJsonValue(json, "\"PATCH\"");
            string build = ExtractJsonValue(json, "\"BUILD\"");

            eveVersion = $"{major}.{minor}.{patch}.{build}";
            Log($"Detected EVE version: {eveVersion}");
            Log($"Suppress version: {suppressedEVEVersion}");
            if(suppressEVEWarning)
                Log("suppressEVEWarning");

            if (!string.IsNullOrEmpty(eveVersion))
            {
                string minVersion = pluginConfig.ContainsKey("MinEVEVersion") ? pluginConfig["MinEVEVersion"] : "2.2.1.0";
                string[] minParts = minVersion.Split('.');
                string[] parts = eveVersion.Split('.');

                if (parts.Length >= 4 && minParts.Length >= 4 &&
                    int.TryParse(parts[0], out int eveMajor) &&
                    int.TryParse(parts[1], out int eveMinor) &&
                    int.TryParse(minParts[0], out int minMajor) &&
                    int.TryParse(minParts[1], out int minMinor) &&
                    (eveMajor < minMajor || (eveMajor == minMajor && eveMinor < minMinor)))
                {
                    if(suppressEVEWarning)
                    {
                        if(string.Equals(eveVersion, suppressedEVEVersion))
                        {
                            Log("EVE warning suppressed");
                            showEVEWarning = false;
                        }
                        else   
                        {
                            Log($"Installed EVE version {eveVersion} is different from suppressed EVE version ${suppressedEVEVersion}. EVE warning will not be suppressed.");
                            suppressEVEWarning = false;
                            showEVEWarning = true;
                        }
                    } else {
                        Log("EVE Warning will be shown");
                        showEVEWarning = true;
                    }

                    if (eveMajor == 1)
                    {
                        eveReduxInstalled = true;
                    }
                    LogWarning($"Unsupported EVE version. Need {minVersion}+");
                }
            }
        }

        private void Update()
        {
            if (ApplicationLauncher.Ready && toolbarButton == null)
                AddToolbarButton();
        }

        private void AddToolbarButton()
        {
            if (!ApplicationLauncher.Ready || toolbarButton != null)
                return;

            Texture2D icon = LoadIconDirect("icon");
            if (icon == null)
            {
                LogError("Failed to load toolbar icon.");
                return;
            }

            toolbarButton = ApplicationLauncher.Instance.AddModApplication(
                OnToolbarButtonOn,
                OnToolbarButtonOff,
                null, null, null, null,
                ApplicationLauncher.AppScenes.ALWAYS,
                icon
            );

            Log("Toolbar button added.");
        }

        private void OnToolbarButtonOn()
        {
            showUI = true;
        }

        private void OnToolbarButtonOff()
        {
            showUI = false;
        }

        private Texture2D LoadIconDirect(string fileName)
        {
            string path = Path.Combine(KSPUtil.ApplicationRootPath, "GameData/PromisedWorlds/Misc/Icon/", fileName + ".png");

            if (!File.Exists(path))
            {
                LogError("Icon not found: " + path);
                return null;
            }

            try
            {
                byte[] fileData = File.ReadAllBytes(path);
                Texture2D tex = new Texture2D(2, 2, TextureFormat.ARGB32, false);
                if (tex.LoadImage(fileData))
                    return tex;
                else
                {
                    LogError("Failed to load texture.");
                    return null;
                }
            }
            catch (Exception e)
            {
                LogError("Error loading icon: " + e);
                return null;
            }
        }

        private void OnDestroy()
        {
            if (toolbarButton != null && ApplicationLauncher.Instance != null)
                ApplicationLauncher.Instance.RemoveModApplication(toolbarButton);
        }

        private void OnGUI()
        {
            GUI.skin = HighLogic.Skin;
            if (showUI)
            {
                string windowTitle = (HighLogic.LoadedScene == GameScenes.MAINMENU) ? "Promised Worlds Settings" : "Promised Worlds";
                windowRect = GUI.Window(GetInstanceID(), windowRect, DrawWindow, windowTitle);
            }

            if (showCredits)
                creditsRect = GUI.Window(999999, creditsRect, DrawCreditsWindow, "Promised Worlds Credits");

            if (showEVEWarning)
                eveWarningRect = GUI.Window(888888, eveWarningRect, DrawEVEWarning, "Promised Worlds Warning");

            if (showSingularityWarning)
                singularityWarningRect = GUI.Window(777777, singularityWarningRect, DrawSingularityWarning, "Promised Worlds Warning");

            if (showDebdebSystemWarning)
                debdebSystemWarningRect = GUI.Window(121212, debdebSystemWarningRect, DrawDebdebSystemWarning, "Critical Promised Worlds Installation Error");
                
            if (showMisplacedSystemsWarning)
            {
                // Dynamic Height based On Number of systems
                float baseHeight = 250;
                float perSystemHeight = 50;
                float dynamicHeight = baseHeight + (misplacedSystems.Count * perSystemHeight);
                misplacedSystemsWarningRect.height = dynamicHeight;
                misplacedSystemsWarningRect = GUI.Window(333333, misplacedSystemsWarningRect, DrawMisplacedSystemsWarning, "Critical Promised Worlds Installation Error");
            }

            if (showDependencyWarning)
            {
                missingDependenciesWarningRect = GUI.Window(333333, missingDependenciesWarningRect, DrawMissingDependenciesWarning, "Critical Promised Worlds Installation Error");
            }

            if (showWelcome)
                welcomeRect = GUI.Window(666666, welcomeRect, DrawWelcomeWindow, "Welcome to Promised Worlds!");

            if (showLinks)
                linksRect = GUI.Window(555555, linksRect, DrawLinksWindow, "Promised Worlds Links");

            if (showChangelog)
                changelogRect = GUI.Window(444444, changelogRect, DrawChangelogWindow, "Promised Worlds Changelog");
        }

        private void DrawEVEWarning(int id)
        {
            GUILayout.BeginVertical();
            
            // If EVE Redux is installed 
            if (eveReduxInstalled)
            {
                GUILayout.Label("EVE Redux is not supported by Promised Worlds!");
                GUILayout.Space(5);
                GUILayout.Label($"Only True Volumetric Clouds release 3 and above are supported.");
                GUILayout.Space(5);
                GUILayout.Label("Release 3 is available at no cost from Blackrack's Patreon page.");
            } else {
                GUILayout.Label("Promised Worlds: Unsupported EVE version detected!");
                string minVersion = pluginConfig.ContainsKey("MinEVEVersion") ? pluginConfig["MinEVEVersion"] : "2.2.1.0";
                GUILayout.Label($"Only True Volumetric Clouds release 3 (EVE version {minVersion}) and above are supported.");
            }

            GUILayout.Space(10);
            if (GUILayout.Button("Understood"))
            {
                showEVEWarning = false;
            }
            GUILayout.Space(5);
            if (GUILayout.Button("Don't show again"))
            {
                showEVEWarning = false;
                CreatePluginConfig(isFirstRun, true, eveVersion);
                Log("EVE warning - user clicked don't show again.");
                Log($"Wrote plugin config suppressing EVE version {eveVersion}");
            }
            GUILayout.Space(10);
            GUILayout.EndVertical();
            GUI.DragWindow();
        }

        private void DrawSingularityWarning(int id)
        {
            GUILayout.BeginVertical();
            GUILayout.Label("Wormholes require the mod Singularity to be installed.");
            GUILayout.Label("Please install it to use this feature.");
            GUILayout.Space(10);
            if (GUILayout.Button("Understood"))
            {
                showSingularityWarning = false;
                settings["Wormholes"] = "False";
                SaveSettings();
            }
            GUILayout.EndVertical();
            GUI.DragWindow();
        }

        // Check if there's a folder called DebdebSystem (ancient PW) - if so it will cause issues.
        private void CheckDebdebSystem()
        {
            string gameDataPath = Path.Combine(KSPUtil.ApplicationRootPath, "GameData");
            if (Directory.Exists(Path.Combine(gameDataPath, "DebdebSystem")))
            {
                LogError("DebdebSystem folder is present! Old PW will cause issues.");
                showDebdebSystemWarning = true;
            }

        }

        private void CheckMisplacedSystems()
        {
            string gameDataPath = Path.Combine(KSPUtil.ApplicationRootPath, "GameData");
            misplacedSystems.Clear();

            if (Directory.Exists(Path.Combine(gameDataPath, "_Systems")))
            {
                misplacedSystems.Add("_Systems");
                LogError("_Systems folder in wrong location!");
            }

            if (Directory.Exists(Path.Combine(gameDataPath, "Debdeb")))
            {
                misplacedSystems.Add("Debdeb");
                LogError("Debdeb folder in wrong location!");
            }

            if (Directory.Exists(Path.Combine(gameDataPath, "Tuun")))
            {
                misplacedSystems.Add("Tuun");
                LogError("Tuun folder in wrong location!");
            }

            if (misplacedSystems.Count > 0)
                showMisplacedSystemsWarning = true;
        }

        private void DrawDebdebSystemWarning(int id)
        {
            GUILayout.BeginVertical();

            GUIStyle headerStyle = new GUIStyle(GUI.skin.label);
            headerStyle.fontStyle = FontStyle.Bold;
            headerStyle.fontSize = 14;

            GUIStyle pathStyle = new GUIStyle(GUI.skin.label);
            pathStyle.fontStyle = FontStyle.Italic;

            GUILayout.Label("Promised Worlds installation error detected!", headerStyle);
            GUILayout.Space(10);

            GUILayout.Label("DebdebSystem folder (Old Promised Worlds version) is present.");

            GUILayout.Space(10);
            GUILayout.Label("Remove this folder.", headerStyle);
            GUILayout.Space(10);
            GUILayout.Label("Promised Worlds will NOT work until this is fixed!", GUI.skin.label);
            GUILayout.Space(10);

            if (GUILayout.Button("Understood"))
            {
                showDebdebSystemWarning = false;
            }

            GUILayout.EndVertical();
            GUI.DragWindow();
        }

        private void DrawMisplacedSystemsWarning(int id)
        {
            GUILayout.BeginVertical();

            GUIStyle headerStyle = new GUIStyle(GUI.skin.label);
            headerStyle.fontStyle = FontStyle.Bold;
            headerStyle.fontSize = 14;

            GUIStyle pathStyle = new GUIStyle(GUI.skin.label);
            pathStyle.fontStyle = FontStyle.Italic;

            GUILayout.Label("Promised Worlds installation error detected!", headerStyle);
            GUILayout.Space(10);

            GUILayout.Label("You have the following Promised Worlds systems installed incorrectly:");
            GUILayout.Space(5);

            foreach (string system in misplacedSystems)
            {
                GUILayout.Label($"GameData/{system}/", pathStyle);
            }

            GUILayout.Space(10);
            GUILayout.Label("Move it to:", headerStyle);
            GUILayout.Space(5);

            foreach (string system in misplacedSystems)
            {
                // _systems Goes Directly in promisedWorlds, Not nested
                if (system == "_Systems")
                    GUILayout.Label("GameData/PromisedWorlds/_Systems/", pathStyle);
                else
                    GUILayout.Label($"GameData/PromisedWorlds/_Systems/{system}/", pathStyle);
            }

            GUILayout.Space(10);
            GUILayout.Label("Promised Worlds will NOT work until this is fixed!", GUI.skin.label);
            GUILayout.Space(10);

            if (GUILayout.Button("Understood"))
            {
                showMisplacedSystemsWarning = false;
            }

            GUILayout.EndVertical();
            GUI.DragWindow();
        }

        private void DrawMissingDependenciesWarning(int id)
        {
            GUILayout.BeginVertical();

            GUIStyle headerStyle = new GUIStyle(GUI.skin.label);
            headerStyle.fontStyle = FontStyle.Bold;
            headerStyle.fontSize = 14;

            GUIStyle pathStyle = new GUIStyle(GUI.skin.label);
            pathStyle.fontStyle = FontStyle.Italic;

            GUILayout.Label("Promised Worlds installation error detected!", headerStyle);
            GUILayout.Space(10);

            GUILayout.Label("The following dependencies of Promised Worlds were not found.");
            GUILayout.Space(5);
            GUILayout.Label("Install them via CKAN or click to open their download pages in a browser.");
            GUILayout.Space(10);

            // scrollable Changelog content
            dependencyScrollPos = GUILayout.BeginScrollView(dependencyScrollPos, GUILayout.Width(missingDependenciesWarningRect.width - 20), GUILayout.Height(missingDependenciesWarningRect.height / 2));
            foreach ((string, string, string) dependency in missingDependencies)
            {
                GUILayout.Label(dependency.Item1); // Name
                GUILayout.Space(5);

                // Link button to the dependency
                string url = dependency.Item3;
                if (GUILayout.Button(url, GUILayout.Height(36)))
                {
                    if (!string.IsNullOrEmpty(url))
                    {
                        Application.OpenURL(url);
                    }
                }
                GUILayout.Space(10);
            }

            GUILayout.EndScrollView();

            GUILayout.Space(10);
            GUILayout.Label("Promised Worlds will NOT work until this is fixed!", GUI.skin.label);
            GUILayout.Space(10);

            if (GUILayout.Button("Understood"))
            {
                showDependencyWarning = false;
            }

            GUILayout.EndVertical();
            GUI.DragWindow();
        }

        private void LoadPluginSettings()
        {
            if (File.Exists(pluginConfigPath))
            {
                try
                {
                    Log("Loading plugin settings");
                    string[] lines = File.ReadAllLines(pluginConfigPath);

                    foreach (string line in lines)
                    {
                        string trimmed = line.Trim();
                        if (trimmed.Length == 0 || trimmed.StartsWith("//") || trimmed.StartsWith("{") || trimmed.StartsWith("}"))
                            continue;

                        if (trimmed.Contains("="))
                        {
                            string[] parts = trimmed.Split(new char[] { '=' }, 2);
                            if (parts.Length >= 2)
                            {
                                string key = parts[0].Trim();
                                string value = parts[1].Trim();

                                // Strip Inline Comments
                                int commentIndex = value.IndexOf(" //");
                                if (commentIndex >= 0)
                                    value = value.Substring(0, commentIndex).Trim();

                                pluginConfig[key] = value;

                                if (key == "FirstRun")
                                    isFirstRun = value.Equals("True", StringComparison.OrdinalIgnoreCase);

                                if (key == "SuppressEVEWarning")
                                    suppressEVEWarning = value.Equals("True", StringComparison.OrdinalIgnoreCase);

                                if (key == "SuppressedEVEVersion")
                                {
                                    if (!string.IsNullOrEmpty(value))
                                    {
                                        suppressedEVEVersion = value;
                                    }
                                    else
                                    {
                                        suppressedEVEVersion = null;
                                        suppressEVEWarning = false;
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    LogError("Failed to read Plugin.cfg: " + e);
                }
            }
            else
            {
                LogWarning("Plugin config does not exist at " + pluginConfigPath);
                CreatePluginConfig(true, false, "0.0.0.0");
                Log("Wrote new plugin config with defaults.");
                LoadDefaultPluginConfig();
            }

            if (isFirstRun && HighLogic.LoadedScene == GameScenes.MAINMENU)
            {
                showWelcome = true;
            }
        }

        private void LoadDefaultPluginConfig()
        {
            pluginConfig["FirstRun"] = "True";
            pluginConfig["WikiURL"] = "https://promisedworlds.github.io/PWiki/";
            pluginConfig["DiscordURL"] = "https://discord.gg/d5tjWuWan7";
            pluginConfig["GitHubURL"] = "https://github.com/PromisedWorlds/PromisedWorlds";
            pluginConfig["ForumURL"] = "https://forum.kerbalspaceprogram.com/topic/228751-112x-v103-promised-worlds-a-faithful-recreation-of-ksp-2s-solar-systems-in-ksp-1/";
            pluginConfig["MinEVEVersion"] = "2.2.1.0";
            pluginConfig["SuppressEVEWarning"] = "False";
            pluginConfig["SuppressedEVEVersion"] = "0.0.0.0";
            pluginConfig["RequireSingularityForWormholes"] = "True";
        }

        private void CreatePluginConfig(bool firstRun, bool suppressEVEWarning, string suppressedEVEVersion)
        {
            try
            {
                string directory = Path.GetDirectoryName(pluginConfigPath);
                if (!Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                List<string> lines = new List<string>();
                lines.Add("PromisedWorldsPlugin");
                lines.Add("{");
                lines.Add("    FirstRun = " + (firstRun ? "True" : "False"));
                lines.Add("");
                lines.Add("    // Welcome Screen Links");
                lines.Add("    WikiURL = https://promisedworlds.github.io/PWiki/");
                lines.Add("    DiscordURL = https://discord.gg/d5tjWuWan7");
                lines.Add("    GitHubURL = https://github.com/PromisedWorlds/PromisedWorlds");
                lines.Add("    ForumURL = https://forum.kerbalspaceprogram.com/topic/228751-112x-v103-promised-worlds-a-faithful-recreation-of-ksp-2s-solar-systems-in-ksp-1/");
                lines.Add("");
                lines.Add("    // Mod Compatibility Settings");
                lines.Add("    MinEVEVersion = 2.2.1.0 // Minimum supported EVE version");
                lines.Add("    // If true, EVE warning has been suppressed and won't be shown again.");
                lines.Add("    SuppressEVEWarning = " + (suppressEVEWarning ? "True" : "False"));
                lines.Add("    // Store version of EVE that has been suppressed - if a new version is installed, check again to display warning.");
                lines.Add("    SuppressedEVEVersion = " + suppressedEVEVersion);         
                lines.Add("    RequireSingularityForWormholes = True // Set to False to disable Singularity requirement warning");
                lines.Add("}");

                File.WriteAllLines(pluginConfigPath, lines);
                Log("Plugin.cfg created at " + pluginConfigPath);
            }
            catch (Exception e)
            {
                LogError("Failed to create Plugin.cfg: " + e);
            }
        }

        private void LoadWelcomeIcons()
        {
            wikiIcon = LoadIconDirect("wiki_icon");
            discordIcon = LoadIconDirect("discord_icon");
            githubIcon = LoadIconDirect("github_icon");
            forumIcon = LoadIconDirect("forum_icon");
        }

        private void DrawIconButton(string text, Texture2D icon, string url)
        {
            GUILayout.BeginHorizontal(GUILayout.Height(40));

            if (icon != null)
            {
                GUILayout.Box(icon, GUILayout.Width(24), GUILayout.Height(24));
                GUILayout.Space(5);
            }
            else
            {
                GUILayout.Space(29);
            }

            if (GUILayout.Button(text, GUILayout.ExpandWidth(true), GUILayout.Height(36)))
            {
                if (!string.IsNullOrEmpty(url))
                {
                    Application.OpenURL(url);
                }
            }

            GUILayout.EndHorizontal();
            GUILayout.Space(8);
        }

        private void DrawWelcomeWindow(int id)
        {
            GUILayout.BeginVertical();

            GUILayout.Label("Thank you for downloading Promised Worlds!", GUI.skin.GetStyle("Label"));
            GUILayout.Space(10);

            GUILayout.Label("We're excited to have you explore the new star systems.");
            GUILayout.Label("Here are some useful links to get started:");
            GUILayout.Space(10);

            // links section With Icons, urls From Config
            string wikiURL = pluginConfig.ContainsKey("WikiURL") ? pluginConfig["WikiURL"] : "https://promisedworlds.github.io/PWiki/";
            string discordURL = pluginConfig.ContainsKey("DiscordURL") ? pluginConfig["DiscordURL"] : "https://discord.gg/d5tjWuWan7";
            string githubURL = pluginConfig.ContainsKey("GitHubURL") ? pluginConfig["GitHubURL"] : "https://github.com/PromisedWorlds/PromisedWorlds";
            string forumURL = pluginConfig.ContainsKey("ForumURL") ? pluginConfig["ForumURL"] : "https://forum.kerbalspaceprogram.com/topic/228751-112x-v103-promised-worlds-a-faithful-recreation-of-ksp-2s-solar-systems-in-ksp-1/";

            DrawIconButton("Wiki", wikiIcon, wikiURL);
            DrawIconButton("Discord Community", discordIcon, discordURL);
            DrawIconButton("GitHub Repository", githubIcon, githubURL);
            DrawIconButton("Forum Thread", forumIcon, forumURL);

            GUILayout.Space(15);
            GUILayout.Label("You can access settings anytime from the toolbar button.");
            GUILayout.Space(10);

            if (GUILayout.Button("Get Started!"))
            {
                showWelcome = false;
                CreatePluginConfig(false, suppressEVEWarning, suppressedEVEVersion);
            }

            GUILayout.EndVertical();
            GUI.DragWindow();
        }

        private void DrawWindow(int id)
        {
            if (HighLogic.LoadedScene != GameScenes.MAINMENU)
            {
                windowRect.width = 320;
                windowRect.height = 320;

                GUILayout.BeginVertical();

                GUILayout.Label("Installed Systems:", GUI.skin.GetStyle("Label"));
                GUILayout.Space(5);

                GUILayout.Label("Core: " + coreVersion);
                GUILayout.Label("Debdeb: " + debdebVersion);
                GUILayout.Label("Tuun: " + tuunVersion);

                GUILayout.Space(10);
                GUILayout.Label("To change settings, return to the Main Menu.", GUI.skin.GetStyle("Label"));
                GUILayout.Space(10);

                if (GUILayout.Button("Open PromisedWorlds.log"))
                {
                    string logFilePath = KSPUtil.ApplicationRootPath + "GameData/PromisedWorlds/PromisedWorlds.log";
                    if (File.Exists(logFilePath))
                        System.Diagnostics.Process.Start(logFilePath);
                    else
                        ScreenMessages.PostScreenMessage("PromisedWorlds.log not found!", 3f, ScreenMessageStyle.UPPER_CENTER);
                }

                if (GUILayout.Button("Open KSP.log"))
                {
                    string kspLogPath = KSPUtil.ApplicationRootPath + "KSP.log";
                    if (File.Exists(kspLogPath))
                        System.Diagnostics.Process.Start(kspLogPath);
                    else
                        ScreenMessages.PostScreenMessage("KSP.log not found!", 3f, ScreenMessageStyle.UPPER_CENTER);
                }

                GUILayout.Space(10);

                if (GUILayout.Button("Close"))
                {
                    showUI = false;
                    if (toolbarButton != null)
                        toolbarButton.SetFalse(false);
                }

                GUILayout.EndVertical();
                GUI.DragWindow();
                return;
            }

            if (!settingsLoaded)
            {
                GUILayout.Label("Settings file missing!");
                if (GUILayout.Button("Close")) showUI = false;
                GUI.DragWindow();
                return;
            }

            GUILayout.BeginVertical();

            float scrollHeight = windowRect.height - 120;
            settingsScrollPos = GUILayout.BeginScrollView(settingsScrollPos, GUILayout.Width(windowRect.width - 20), GUILayout.Height(scrollHeight));

            DrawToggleWithDescription("Wormholes", "Enables wormholes that link the star systems and the Kerbol system together. Set to False for intended KSP2 experience.");
            DrawToggleWithDescription("RealisticStarSize", "Options are True and False. Adjusts star sizes to be realistic or in line with Kerbol's stats. Only applies in default scale.");
            DrawToggleWithDescription("RemoveStockScreens", "Whether or not to remove the stock KSP loading screens. If false, Promised Worlds loading screens will be shown alongside the stock ones.");
            DrawToggleWithDescription("Skybox", "Requires KSP DiRT, TextureReplacer, or Sigma Replacements Skybox. Set to True to enable the skybox. Set to False if using a skybox replacer.");
            DrawToggleWithDescription("DebugReload", "Bypass cache files for all planets, so changes will be reflected immediately when the game reloads.");

            GUILayout.Space(10);

            string[] distanceOptions = new string[] { "0.1", "1", "10", "100" };
            DrawOptionGrid("DistanceFactor", "Options are as followed: 1 - Default kerbal LYs (0.01 real LY) 0.1 - 10% default distance, 10 - 10x default distance, or 100 - 100x default distance. Multiplier for stellar distances.", distanceOptions);

            GUILayout.Space(10);

            string[] rescaleOptions = new string[] { "1", "2.5", "10" };
            DrawOptionGrid("Rescale", "Options are 1, 2.5 and 10. Requires Sigma Dimensions. Rescales all bodies and orbits. 2.5x rescale is forced when JNSQ is installed. 10x rescale is forced when RSS is installed.", rescaleOptions);

            GUILayout.EndScrollView();

            GUILayout.Space(5);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Apply Changes"))
            {
                SaveSettings();
                ScreenMessages.PostScreenMessage("Changes applied. Restart your game!", 5f, ScreenMessageStyle.UPPER_CENTER);
            }

            if (GUILayout.Button("Reset to Defaults"))
                ResetToDefaults();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Links"))
                showLinks = !showLinks;

            if (GUILayout.Button("Changelog"))
                showChangelog = !showChangelog;

            if (GUILayout.Button("Credits"))
                showCredits = !showCredits;

            if (GUILayout.Button("Close"))
            {
                showUI = false;
                if (toolbarButton != null)
                    toolbarButton.SetFalse(false);
            }
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
            GUI.DragWindow();
        }

        private void DrawToggleWithDescription(string key, string description)
        {
            if (!settings.ContainsKey(key)) return;

            bool oldVal = settings[key].Equals("True", StringComparison.OrdinalIgnoreCase);
            bool newVal = GUILayout.Toggle(oldVal, key);

            if (oldVal != newVal)
            {
                bool requireSingularity = pluginConfig.ContainsKey("RequireSingularityForWormholes") ?
                    pluginConfig["RequireSingularityForWormholes"].Equals("True", StringComparison.OrdinalIgnoreCase) : true;

                if (key == "Wormholes" && newVal && requireSingularity && !singularityDetected)
                {
                    showSingularityWarning = true;
                    return;
                }

                settings[key] = newVal ? "True" : "False";
                SaveSettings();
            }

            GUILayout.Label(description, GUILayout.ExpandWidth(true));
        }

        private void DrawOptionGrid(string key, string description, string[] options)
        {
            if (!settings.ContainsKey(key))
                settings[key] = options[0];

            float currentVal = float.Parse(settings[key], CultureInfo.InvariantCulture);
            int selectedIndex = 0;
            for (int i = 0; i < options.Length; i++)
            {
                if (Math.Abs(float.Parse(options[i], CultureInfo.InvariantCulture) - currentVal) < 0.001f)
                    selectedIndex = i;
            }

            int oldIndex = selectedIndex;

            GUILayout.Label(key, GUILayout.ExpandWidth(true));

            if (key == "Rescale")
            {
                for (int i = 0; i < options.Length; i++)
                {
                    float val = float.Parse(options[i], CultureInfo.InvariantCulture);
                    bool enabled = true;
                    if (rssDetected && val != 10f) enabled = false;
                    if (jnsqDetected && Math.Abs(val - 2.5f) > 0.001f) enabled = false;

                    GUI.enabled = enabled;

                    GUI.backgroundColor = (i == selectedIndex) ? Color.gray : Color.white;

                    if (GUILayout.Button(options[i]))
                        selectedIndex = i;

                    GUI.backgroundColor = Color.white;
                    GUI.enabled = true;
                }
            }
            else
            {
                selectedIndex = GUILayout.SelectionGrid(selectedIndex, options, 4);
            }

            if (oldIndex != selectedIndex)
            {
                settings[key] = options[selectedIndex];
                SaveSettings();
            }

            GUILayout.Label(description, GUILayout.ExpandWidth(true));
        }

        private void DrawCreditsWindow(int id)
        {
            GUILayout.BeginVertical();
            creditsScrollPos = GUILayout.BeginScrollView(creditsScrollPos, GUILayout.Width(creditsRect.width - 20), GUILayout.Height(creditsRect.height - 60));

            if(creditsLoaded)
            {
                GUILayout.Label(creditsContent);
            }
            else
            {
                GUILayout.Label("Credits failed to load!");
            }

            GUILayout.EndScrollView();

            GUILayout.Space(5);
            if (GUILayout.Button("Close"))
                showCredits = false;

            GUILayout.EndVertical();
            GUI.DragWindow();
        }

        private void GenerateLog()
        {
            try
            {
                StringBuilder sb = new StringBuilder();

                string basePath = KSPUtil.ApplicationRootPath + "GameData/PromisedWorlds/";
                string coreVer = GetVersionInfo(basePath + "Version/PromisedWorldsCore.version");
                string debdebVer = GetVersionInfo(basePath + "_Systems/Debdeb/Version/PromisedWorldsDebdeb.version");
                string tuunVer = GetVersionInfo(basePath + "_Systems/Tuun/Version/PromisedWorldsTuun.version");

                sb.AppendLine("Promised Worlds Installed Components:");
                sb.AppendLine($" - Core:   {(coreVer != null ? coreVer : "Not Installed")}");
                sb.AppendLine($" - Debdeb: {(debdebVer != null ? debdebVer : "Not Installed")}");
                sb.AppendLine($" - Tuun:   {(tuunVer != null ? tuunVer : "Not Installed")}");
                sb.AppendLine("===================================================");
                sb.AppendLine();

                sb.AppendLine("Environmental Visual Enhancements (EVE):");
                sb.AppendLine($" - Detected Version: {(eveVersion ?? "Not Installed")}");
                sb.AppendLine("===================================================");

                DetectInstallationInfo(sb);

                sb.AppendLine("Additional Mod Detection:");
                sb.AppendLine($" - RealSolarSystem: {(rssDetected ? "Installed (Rescale locked to 10x)" : "Not Installed")}");
                sb.AppendLine($" - JNSQ: {(jnsqDetected ? "Installed (Rescale locked to 2.5x)" : "Not Installed")}");
                sb.AppendLine("===================================================");

                Log(sb.ToString());
            }
            catch (Exception e)
            {
                LogError("Failed generating log: " + e);
            }
        }

        private void DetectInstallationInfo(StringBuilder sb)
        {
            try
            {
                string basePath = KSPUtil.ApplicationRootPath + "GameData/PromisedWorlds/";
                string ckanPath = Path.Combine(KSPUtil.ApplicationRootPath, "CKAN", "registry.json");

                bool coreCkan = false, debdebCkan = false, tuunCkan = false;

                if (File.Exists(ckanPath))
                {
                    string json = File.ReadAllText(ckanPath);
                    coreCkan = Regex.IsMatch(json, "\"PromisedWorldsCore\"");
                    debdebCkan = Regex.IsMatch(json, "\"PromisedWorldsDebdeb\"");
                    tuunCkan = Regex.IsMatch(json, "\"PromisedWorldsTuun\"");
                }

                string gameData = Path.Combine(KSPUtil.ApplicationRootPath, "GameData");
                string parallaxStatus = "None";
                if (Directory.Exists(Path.Combine(gameData, "ParallaxContinued")))
                    parallaxStatus = "ParallaxContinued installed";
                else if (Directory.Exists(Path.Combine(gameData, "Parallax")))
                    parallaxStatus = "Parallax installed";

                sb.AppendLine("Installation Methods (CKAN / Manual):");
                sb.AppendLine($" - Core:   {(coreCkan ? "CKAN" : File.Exists(Path.Combine(basePath, "Version/PromisedWorldsCore.version")) ? "Manual" : "Not Installed")}");
                sb.AppendLine($" - Debdeb: {(debdebCkan ? "CKAN" : File.Exists(Path.Combine(basePath, "_Systems/Debdeb/Version/PromisedWorldsDebdeb.version")) ? "Manual" : "Not Installed")}");
                sb.AppendLine($" - Tuun:   {(tuunCkan ? "CKAN" : File.Exists(Path.Combine(basePath, "_Systems/Tuun/Version/PromisedWorldsTuun.version")) ? "Manual" : "Not Installed")}");
                sb.AppendLine($" - Parallax: {parallaxStatus}");
                sb.AppendLine("===================================================");
            }
            catch (Exception ex)
            {
                sb.AppendLine("[Error] Failed to detect installation info: " + ex.Message);
            }
        }

        private string GetVersionInfo(string path)
        {
            if (!File.Exists(path))
                return null;

            try
            {
                string json = File.ReadAllText(path);
                string name = ExtractJsonValue(json, "\"NAME\"");
                string major = ExtractJsonValue(json, "\"MAJOR\"");
                string minor = ExtractJsonValue(json, "\"MINOR\"");
                string patch = ExtractJsonValue(json, "\"PATCH\"");
                if (string.IsNullOrEmpty(name)) 
                    name = Path.GetFileNameWithoutExtension(path);
                return $"{name} v{major}.{minor}.{patch}";
            }
            catch
            {
                return "Error reading version";
            }
        }

        private string ExtractJsonValue(string json, string key)
        {
            Match match = Regex.Match(json, key + @"\s*:\s*(?:""(?<str>[^""]+)""|(?<num>\d+))");
            if (match.Success)
                return match.Groups["str"].Success ? match.Groups["str"].Value : match.Groups["num"].Value;
            return "";
        }


        private void ResetToDefaults()
        {
            foreach (var kvp in defaultSettings)
            {
                if (settings.ContainsKey(kvp.Key))
                    settings[kvp.Key] = kvp.Value;
            }

            SaveSettings();
            ScreenMessages.PostScreenMessage("Settings reset to defaults. Restart your game!", 5f, ScreenMessageStyle.UPPER_CENTER);
        }

        private void SaveSettings()
        {
            string fullPath = KSPUtil.ApplicationRootPath + settingsPath;

            if (!File.Exists(fullPath))
            {
                LogError("Cannot save settings - file not found: " + fullPath);
                return;
            }

            try
            {
                string[] lines = File.ReadAllLines(fullPath);

                for (int i = 0; i < lines.Length; i++)
                {
                    string trimmed = lines[i].Trim();
                    if (trimmed.Length == 0 || trimmed.StartsWith("//") || trimmed.StartsWith("{") || trimmed.StartsWith("}"))
                        continue;

                    string[] parts = trimmed.Split('=');
                    if (parts.Length < 2)
                        continue;

                    string key = parts[0].Trim();
                    if (settings.ContainsKey(key))
                    {
                        string valueToWrite = settings[key];

                        // format Floats Properly for rescale and DistanceFactor
                        if ((key.Equals("Rescale", StringComparison.OrdinalIgnoreCase) ||
                            key.Equals("DistanceFactor", StringComparison.OrdinalIgnoreCase)) &&
                            float.TryParse(settings[key], NumberStyles.Float, CultureInfo.InvariantCulture, out float f))
                        {
                            valueToWrite = f.ToString(CultureInfo.InvariantCulture);
                        }

                        // preserve Line Structure with Comments
                        int equalsIndex = lines[i].IndexOf('=');
                        string beforeEquals = lines[i].Substring(0, equalsIndex + 1);
                        string afterEquals = lines[i].Substring(equalsIndex + 1);

                        // Check if there's a comment
                        int commentIndex = afterEquals.IndexOf("//");
                        string comment = (commentIndex >= 0) ? afterEquals.Substring(commentIndex) : "";

                        // Preserve Indentation
                        int indentCount = lines[i].Length - lines[i].TrimStart().Length;
                        string indent = new string(' ', indentCount);

                        // reconstruct Line
                        if (comment.Length > 0)
                            lines[i] = indent + key + " = " + valueToWrite + " " + comment;
                        else
                            lines[i] = indent + key + " = " + valueToWrite;
                    }
                }

                File.WriteAllLines(fullPath, lines);
                Log("Wrote settings to file " + fullPath);
            }
            catch (Exception e)
            {
                LogError("Failed to save settings: " + e);
            }
        }

        private void LoadVersionInfo()
        {
            string basePath = KSPUtil.ApplicationRootPath + "GameData/PromisedWorlds/";
            string coreVer = GetVersionInfo(basePath + "Version/PromisedWorldsCore.version");
            string debdebVer = GetVersionInfo(basePath + "_Systems/Debdeb/Version/PromisedWorldsDebdeb.version");
            string tuunVer = GetVersionInfo(basePath + "_Systems/Tuun/Version/PromisedWorldsTuun.version");

            coreVersion = coreVer ?? "Not Installed";
            debdebVersion = debdebVer ?? "Not Installed";
            tuunVersion = tuunVer ?? "Not Installed";
        }

        // Load the changelog text from file
        private void LoadChangelog()
        {
            string fullPath = KSPUtil.ApplicationRootPath + changelogPath;
            if (File.Exists(fullPath))
            {
                try
                {
                    changelogContent = File.ReadAllText(fullPath);
                    changelogLoaded = true;
                }
                catch (Exception e)
                {
                    LogError("Failed to load changelog: " + e);
                    changelogContent = "Failed to load changelog. Check " + changelogPath;
                    changelogLoaded = false;
                }
            }
            else
            {
                changelogContent = "Changelog.md not found at " + changelogPath;
                changelogLoaded = false;
            }
        }

        // Load the credits text from file
        private void LoadCredits()
        {
            string fullPath = KSPUtil.ApplicationRootPath + creditsPath;
            if (File.Exists(fullPath))
            {
                try
                {
                    creditsContent = File.ReadAllText(fullPath);
                    creditsLoaded = true;
                }
                catch (Exception e)
                {
                    LogError("Failed to load credits: " + e);
                    creditsContent = "Failed to load credits. Check " + creditsPath;
                    creditsLoaded = false;
                }
            }
            else
            {
                creditsContent = "credits.txt not found at " + creditsPath;
                creditsLoaded = false;
            }
        }

        private void LoadDefaultSettings()
        {
            string fullPath = KSPUtil.ApplicationRootPath + defaultSettingsPath;
            if (File.Exists(fullPath))
            {
                try
                {
                    defaultSettingsContent = File.ReadAllText(fullPath);
                    defaultSettingsLoaded = true;
                }
                catch (Exception e)
                {
                    LogError("Failed to load default settings: " + e);
                    defaultSettingsContent = @"PromisedWorldsSettings
    {
        // Error - default settings failed to load. Check:
        // " + defaultSettingsPath + @"
    }";
                    defaultSettingsLoaded = false;
                }
            }
            else
            {
                // 
                defaultSettingsContent = @"PromisedWorldsSettings
    {
        // Error - default settings could not be found at:
        // " + defaultSettingsPath + @"
    }";
                defaultSettingsLoaded = false;
            }
        }

        private void DrawLinksWindow(int id)
        {
            GUILayout.BeginVertical();

            string wikiURL = pluginConfig.ContainsKey("WikiURL") ? pluginConfig["WikiURL"] : "https://promisedworlds.github.io/PWiki/";
            string discordURL = pluginConfig.ContainsKey("DiscordURL") ? pluginConfig["DiscordURL"] : "https://discord.gg/d5tjWuWan7";
            string githubURL = pluginConfig.ContainsKey("GitHubURL") ? pluginConfig["GitHubURL"] : "https://github.com/PromisedWorlds/PromisedWorlds";
            string forumURL = pluginConfig.ContainsKey("ForumURL") ? pluginConfig["ForumURL"] : "https://forum.kerbalspaceprogram.com/topic/228751-112x-v103-promised-worlds-a-faithful-recreation-of-ksp-2s-solar-systems-in-ksp-1/";

            DrawIconButton("Wiki", wikiIcon, wikiURL);
            DrawIconButton("Discord", discordIcon, discordURL);
            DrawIconButton("GitHub", githubIcon, githubURL);
            DrawIconButton("Forum", forumIcon, forumURL);

            GUILayout.Space(10);

            if (GUILayout.Button("Close"))
            {
                showLinks = false;
            }

            GUILayout.EndVertical();
            GUI.DragWindow();
        }

        private void DrawChangelogWindow(int id)
        {
            GUILayout.BeginVertical();

            // scrollable Changelog content
            changelogScrollPos = GUILayout.BeginScrollView(changelogScrollPos, GUILayout.Width(changelogRect.width - 20), GUILayout.Height(changelogRect.height - 80));

            if (!changelogLoaded || string.IsNullOrEmpty(changelogContent))
            {
                GUILayout.Label("Changelog not available. Please check if Changelog.md exists in GameData/PromisedWorlds/");
            }
            else
            {
                RenderMarkdown(changelogContent);
            }

            GUILayout.EndScrollView();

            GUILayout.Space(10);

            if (GUILayout.Button("Close"))
            {
                showChangelog = false;
            }

            GUILayout.EndVertical();
            GUI.DragWindow();
        }

        private void RenderMarkdown(string markdown)
        {
            GUIStyle h1Style = new GUIStyle(GUI.skin.label);
            h1Style.fontSize = 16;
            h1Style.fontStyle = FontStyle.Bold;
            h1Style.normal.textColor = Color.white;
            h1Style.wordWrap = true;

            GUIStyle h2Style = new GUIStyle(GUI.skin.label);
            h2Style.fontSize = 14;
            h2Style.fontStyle = FontStyle.Bold;
            h2Style.normal.textColor = Color.yellow;
            h2Style.wordWrap = true;

            GUIStyle boldStyle = new GUIStyle(GUI.skin.label);
            boldStyle.fontStyle = FontStyle.Bold;
            boldStyle.normal.textColor = Color.green;
            boldStyle.wordWrap = true;

            GUIStyle normalStyle = new GUIStyle(GUI.skin.label);
            normalStyle.wordWrap = true;

            GUIStyle linkStyle = new GUIStyle(GUI.skin.label);
            linkStyle.normal.textColor = Color.cyan;
            linkStyle.wordWrap = true;

            string[] lines = markdown.Split('\n');

            foreach (string line in lines)
            {
                string trimmedLine = line.Trim();

                if (string.IsNullOrEmpty(trimmedLine))
                {
                    GUILayout.Space(3);
                    continue;
                }

                // Skip Images
                if (trimmedLine.StartsWith("!["))
                    continue;

                // Handle <url> Style links
                if (trimmedLine.Contains("<") && trimmedLine.Contains(">") && trimmedLine.Contains("http"))
                {
                    int startIdx = trimmedLine.IndexOf('<');
                    int endIdx = trimmedLine.IndexOf('>');
                    if (endIdx > startIdx)
                    {
                        string url = trimmedLine.Substring(startIdx + 1, endIdx - startIdx - 1);
                        string beforeLink = trimmedLine.Substring(0, startIdx).Trim();
                        string afterLink = trimmedLine.Length > endIdx + 1 ? trimmedLine.Substring(endIdx + 1).Trim() : "";

                        if (!string.IsNullOrEmpty(beforeLink))
                            GUILayout.Label(beforeLink, normalStyle);

                        GUIStyle buttonLinkStyle = new GUIStyle(GUI.skin.button);
                        buttonLinkStyle.normal.textColor = Color.cyan;
                        buttonLinkStyle.hover.textColor = Color.white;
                        buttonLinkStyle.alignment = TextAnchor.MiddleCenter;
                        buttonLinkStyle.wordWrap = true;

                        if (GUILayout.Button(url, buttonLinkStyle))
                            Application.OpenURL(url);

                        if (!string.IsNullOrEmpty(afterLink))
                            GUILayout.Label(afterLink, normalStyle);
                    }
                    continue;
                }

                // h1 Headers
                if (trimmedLine.StartsWith("# "))
                {
                    GUILayout.Label(trimmedLine.Substring(2), h1Style);
                    GUILayout.Space(5);
                    continue;
                }

                // H2 Headers
                if (trimmedLine.StartsWith("## "))
                {
                    GUILayout.Space(5);
                    GUILayout.Label(trimmedLine.Substring(3), h2Style);
                    continue;
                }

                // Bold section Headers
                if (trimmedLine.EndsWith(":") && trimmedLine == trimmedLine.ToUpper())
                {
                    GUILayout.Label(trimmedLine, boldStyle);
                    continue;
                }

                // Bullet Points
                if (trimmedLine.StartsWith("- ") || trimmedLine.StartsWith("• "))
                {
                    string bulletText = trimmedLine.Substring(2);
                    GUILayout.Label("• " + bulletText, normalStyle);
                    continue;
                }

                // Regular Text
                GUILayout.Label(trimmedLine, normalStyle);
            }
        }

        // Logging helpers
        private void Log(string message)
        {
            Debug.Log(logPrefix + message);
        }

        private void LogWarning (string message)
        {
            Debug.LogWarning(logPrefix + message);
        }

        private void LogError (string message)
        {
            Debug.LogError(logPrefix + message);
        }
    }
}
