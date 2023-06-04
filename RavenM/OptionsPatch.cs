using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace RavenM
{
    public class OptionsPatch
    {
        private static Dictionary<string, RavenMOption<Toggle, bool>> _toggles = new Dictionary<string, RavenMOption<Toggle, bool>>();
        private static Dictionary<string, RavenMOption<Slider, float>> _sliders = new Dictionary<string, RavenMOption<Slider, float>>();
        private static ConfigFile keybindConfig;

        public static bool showHUD = false;
        public static event Action onSettingUpdate;
        public static Dictionary<OptionKeybind, ConfigEntry<KeyCode>> setKeybinds = new Dictionary<OptionKeybind, ConfigEntry<KeyCode>>();
        public static Dictionary<OptionText, ConfigEntry<string>> setOptionText = new Dictionary<OptionText, ConfigEntry<string>>();
        [HarmonyPatch(typeof(Options), "Awake")]
        public class OptionsAwakePatch
        {
            static void Postfix()
            {
                if (Options.instance == null)
                {
                    return;
                }
                keybindConfig = new ConfigFile(System.IO.Path.Combine(Paths.ConfigPath, "RavenMSettings.cfg"), false);
                CreateToggleOption("Show RavenM HUD", true, RavenMOptions.ShowRavenMHUD);
                CreateSliderOption("Voice Chat Volume", 1f, RavenMOptions.VoiceChatVolume,false,0f,1f);
                CreateSpacer();
                CreateOptionLabel("NAME TAGS",true);
                CreateSliderOption("Nametag Size", 32f, RavenMOptions.NameTagScaleMultiplier,true,12f,60f);
                CreateToggleOption("Custom Colors", false, RavenMOptions.CustomNameTagColor);
                AddTextToConfig("#1E90FF", OptionText.NameTagTeamColor);
                AddTextToConfig("#FFA500", OptionText.NameTagEnemyColor);
                CreateOptionLabel("<color=yellow>Click me to open the Settings File</color>", OnClickKeybindLabel);
                AddKeybindToConfig(KeyCode.CapsLock, OptionKeybind.VoiceChatButton);
                AddKeybindToConfig(KeyCode.BackQuote, OptionKeybind.PlaceMarkerButton);
                AddKeybindToConfig(KeyCode.Y, OptionKeybind.GlobalChatButton);
                AddKeybindToConfig(KeyCode.U, OptionKeybind.TeamChatButton);
                //CreateToggleOption("AAAA", KeyCode.O, OptionTypes.Keybind, RavenMOptions.RebindKeyA);
            }
        }
        static void OnClickKeybindLabel()
        {
            //Opens the BepInEx Config File in the Explorer
            if (Application.platform == RuntimePlatform.WindowsPlayer)
            {
                System.Diagnostics.Process.Start("explorer.exe", "/select," + keybindConfig.ConfigFilePath);
            }
            else if (Application.platform == RuntimePlatform.OSXPlayer)
            {
                System.Diagnostics.Process.Start("open", "-R " + keybindConfig.ConfigFilePath);
            }
        }
        [HarmonyPatch(typeof(Options), nameof(Options.Save))]
        public class OptionsSavePatch
        {
            static void Postfix()
            {
                SetConfigValues(true);
                Plugin.logger.LogInfo("Saved Settings");
            }
        }
        [HarmonyPatch(typeof(MainMenu), nameof(MainMenu.OpenPageIndex))]
        public class MainMenuOpenPageIndexPatch
        {
            static void Postfix(int index)
            {
                if (index == 1)
                {
                    SetConfigValues(false);
                    Plugin.logger.LogInfo("Applied Settings");
                }
            }
        }
        //[HarmonyPatch(typeof(SteelInput), nameof(SteelInput.BindPositiveKeyCode))]
        //public class SteelInputPatch
        //{
        //	static void Postfix()
        //	{

        //	}
        //}

        public static void SetConfigValues(bool saveValue)
        {
            foreach (RavenMOption<Toggle, bool> UIToggle in _toggles.Values)
            {
                if (saveValue)
                    UIToggle.Save();

            }
            foreach (RavenMOption<Slider, float> UISlider in _sliders.Values)
            {
                if (saveValue)
                    UISlider.Save();
            }
            showHUD = GetOptionWithName<bool>(RavenMOptions.ShowRavenMHUD, OptionTypes.Toggle);
            IngameNetManager.instance.VoiceChatVolume = GetOptionWithName<float>(RavenMOptions.VoiceChatVolume, OptionTypes.Slider);
            IngameNetManager.instance.VoiceChatKeybind = setKeybinds[OptionKeybind.VoiceChatButton].Value;
            IngameNetManager.instance.PlaceMarkerKeybind = setKeybinds[OptionKeybind.PlaceMarkerButton].Value;
            ChatManager.instance.GlobalChatKeybind = setKeybinds[OptionKeybind.GlobalChatButton].Value;
            ChatManager.instance.TeamChatKeybind = setKeybinds[OptionKeybind.TeamChatButton].Value;
            onSettingUpdate?.Invoke();
        }
        public static T GetOptionWithName<T>(RavenMOptions option, OptionTypes type)
        {
            switch (type)
            {
                case OptionTypes.Toggle:
                    var toggleOption = _toggles.FirstOrDefault(x => x.Value.id == option).Value;
                    if (toggleOption == null)
                    {
                        Plugin.logger.LogError($"Could not get Option with name {option}!");
                        return default(T);
                    }
                    return (T)(object)toggleOption.value;
                case OptionTypes.Slider:
                    var sliderOption = _sliders.FirstOrDefault(x => x.Value.id == option).Value;
                    if (sliderOption == null)
                    {
                        Plugin.logger.LogError($"Could not get Option with name {option}!");
                        return default(T);
                    }
                    return (T)(object)sliderOption.value;
            }
            return default(T);
        }
        public static ConfigEntry<string> GetOptionWithName<T>(OptionText option)
        {
            return setOptionText.FirstOrDefault(x => x.Key == option).Value;
        }
        public enum RavenMOptions
        {
            ShowRavenMHUD,
            VoiceChatVolume,
            LazyKeybindLabel,
            RebindKeyA,
            NameTagScaleMultiplier,
            CustomNameTagColor

        }
        public enum OptionTypes
        {
            Toggle,
            Slider,
            Dropdown,
            Label,
            Keybind,
            SliderWithLabel
        }
        public enum OptionKeybind
        {
            VoiceChatButton,
            PlaceMarkerButton,
            GlobalChatButton,
            TeamChatButton
        }
        public enum OptionText
        {
            NameTagTeamColor,
            NameTagEnemyColor
        }
        private static void CreateSliderOption(string name,float defaultValue,RavenMOptions option,bool label,float min,float max)
        {
            if (_sliders.ContainsKey(name))
            {
                Plugin.logger.LogError($"Slider with the name {name} already exists!");
                return;
            }
            RectTransform target = Options.instance.gameOptions.transform.Find("Game Panel/Scroll View/Viewport/Content").GetComponent<RectTransform>();
            ConfigEntry<float> ravenMConfig = ravenMConfig = keybindConfig.Bind("RavenMKeybinds", option.ToString(), defaultValue);
            RectTransform exampleSettingSlider = Options.instance.gameOptions.transform.Find("Game Panel/Scroll View/Viewport/Content/SFX Volume").GetComponent<RectTransform>();
            if(label)
                exampleSettingSlider = Options.instance.gameOptions.transform.Find("Game Panel/Scroll View/Viewport/Content/Field Of View").GetComponent<RectTransform>();
            GameObject newTarget = GameObject.Instantiate(exampleSettingSlider.gameObject, target);
            newTarget.GetComponent<Text>().text = name;
            Slider slider = newTarget.GetComponentInChildren<Slider>();
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = defaultValue;
            MonoBehaviour.Destroy(newTarget.GetComponent<OptionSlider>());
            RavenMOptionSlider ravenMSlider = newTarget.AddComponent<RavenMOptionSlider>();
            if (label)
            {
                newTarget.transform.Find("Field Of View Label").GetComponent<SliderLabel>().slider = slider;
            }
            ravenMSlider.name = name;
            ravenMSlider.defaultValue = defaultValue;
            ravenMSlider.id = option;
            ravenMSlider.configEntry = ravenMConfig as ConfigEntry<float>;
            _sliders[name] = ravenMSlider;
        }
        private static void CreateToggleOption(string name, bool defaultValue, RavenMOptions option)
        {
            if (_toggles.ContainsKey(name))
            {
                Plugin.logger.LogError($"Toggle with the name {name} already exists!");
                return;
            }
            RectTransform target = Options.instance.gameOptions.transform.Find("Game Panel/Scroll View/Viewport/Content").GetComponent<RectTransform>();
            ConfigEntry<bool> ravenMConfig = ravenMConfig = keybindConfig.Bind("RavenMKeybinds", option.ToString(), defaultValue);
            RectTransform exampleSettingToggle = Options.instance.gameOptions.transform.Find("Game Panel/Scroll View/Viewport/Content/Hitmarkers").GetComponent<RectTransform>();
            GameObject newTarget = GameObject.Instantiate(exampleSettingToggle.gameObject, target);
            newTarget.GetComponent<Text>().text = name;
            MonoBehaviour.Destroy(newTarget.GetComponent<OptionToggle>());
            RavenMOptionToggle ravenMToggle = newTarget.AddComponent<RavenMOptionToggle>();
            ravenMToggle.name = name;
            ravenMToggle.defaultValue = defaultValue;
            ravenMToggle.id = option;
            ravenMToggle.configEntry = ravenMConfig;
            _toggles[name] = ravenMToggle;
        }
        private static void CreateOptionLabel(string name,bool header)
        {
            RectTransform exampleText;
            if (header)
                exampleText = Options.instance.videoOptions.transform.Find("Video Panel/Scroll View/Viewport/Content/Header").GetComponent<RectTransform>();
            else
                exampleText = Options.instance.gameOptions.transform.Find("Game Panel/Scroll View/Viewport/Content/Hitmarkers").GetComponent<RectTransform>();
            RectTransform target = Options.instance.gameOptions.transform.Find("Game Panel/Scroll View/Viewport/Content").GetComponent<RectTransform>();
            GameObject newTarget3 = GameObject.Instantiate(exampleText.gameObject, target);
            Text text = newTarget3.GetComponent<Text>();
            text.supportRichText = true;
            text.text = name;
            if (header)
            {
                text.resizeTextForBestFit = true;
                text.fontSize = 25;
                text.resizeTextMaxSize = text.resizeTextMaxSize + 10;
            }
            else
            {
                // Because we get the Text Component from the Toggle
                GameObject.Destroy(newTarget3.transform.GetChild(0).gameObject);
            }
        }
        private static void CreateOptionLabel(string name,Action onClick)
        {
            RectTransform exampleText = Options.instance.gameOptions.transform.Find("Game Panel/Scroll View/Viewport/Content/Hitmarkers").GetComponent<RectTransform>();
            RectTransform target = Options.instance.gameOptions.transform.Find("Game Panel/Scroll View/Viewport/Content").GetComponent<RectTransform>();
            GameObject newTarget3 = GameObject.Instantiate(exampleText.gameObject, target);
            Text text = newTarget3.GetComponent<Text>();
            text.supportRichText = true;
            text.text = name;
            GameObject.Destroy(newTarget3.transform.GetChild(0).gameObject);
            Button button = newTarget3.AddComponent<Button>();
            button.onClick.AddListener(delegate
            {
                onClick();
            });
        }
        private static void CreateSpacer()
        {
            RectTransform exampleSpacer = Options.instance.gameOptions.transform.Find("Game Panel/Scroll View/Viewport/Content/Spacer").GetComponent<RectTransform>();
            RectTransform target = Options.instance.gameOptions.transform.Find("Game Panel/Scroll View/Viewport/Content").GetComponent<RectTransform>();
            GameObject.Instantiate(exampleSpacer.gameObject, target);
        }
        private static void AddKeybindToConfig(KeyCode defaultKeyCode, OptionKeybind keybind)
        {
            // Probably not a good idea
            setKeybinds.Add(keybind, keybindConfig.Bind("RavenMKeybinds", keybind.ToString(), defaultKeyCode));
        }
        private static void AddTextToConfig(string defaultText, OptionText text)
        {
            // Probably not a good idea
            setOptionText.Add(text, keybindConfig.Bind("RavenMColors", text.ToString(), defaultText));
        }
        public abstract class RavenMOption<T, U> : MonoBehaviour
        {
            public RavenMOptions id;

            public U defaultValue;

            public U value;

            public bool valueChanged;

            public T uiElement;

            private string sectionName = "RavenMKeybinds";

            public ConfigEntry<U> configEntry;
            public virtual void Start()
            {
                this.uiElement = base.GetComponentInChildren<T>();
                this.Load();
            }
            public virtual void Save()
            {
                this.valueChanged = false;
                // Doing Config.Bind again would not work here, that's why it is necessary to pass this in here as well.
                configEntry.Value = this.value;
                Plugin.logger.LogInfo("Saved value " + this.value);
            }
            protected virtual void OnValueChange(U newValue)
            {
                this.value = newValue;
                this.valueChanged = true;
            }
            public virtual void Load()
            {
                ConfigDefinition configDef = new ConfigDefinition(sectionName, this.id.ToString());
                bool hasValue = keybindConfig.TryGetEntry(configDef, out ConfigEntry<U> savedValue);
                if (hasValue)
                {
                    this.value = savedValue.Value;
                }
                else
                {
                    this.value = this.defaultValue;
                    Plugin.logger.LogError($"Couldn't load config for {sectionName} {this.id.ToString()} and set to default value {this.defaultValue}");
                }

            }

        }
        public class RavenMOptionToggle : RavenMOption<Toggle, bool>
        {
            public override void Start()
            {
                base.Start();
                this.uiElement.onValueChanged.AddListener(new UnityAction<bool>(this.OnValueChange));
            }

            public override void Save()
            {
                base.Save();
            }

            public override void Load()
            {
                base.Load();
                this.uiElement.isOn = this.value;
            }
        }
        public class RavenMOptionSlider : RavenMOption<Slider, float>
        {
            public override void Start()
            {
                base.Start();
                this.uiElement.onValueChanged.AddListener(new UnityAction<float>(this.OnValueChange));
            }

            public override void Save()
            {
                base.Save();
            }
            public override void Load()
            {
                base.Load();
                this.uiElement.value = this.value;
            }
        }
    }

}
