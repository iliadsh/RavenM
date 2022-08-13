using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace RavenM
{
    public class OptionsPatch
    {
		private static Dictionary<string, RavenMOption<Toggle,bool>> _toggles = new Dictionary<string, RavenMOption<Toggle, bool>>();
		private static Dictionary<string, RavenMOption<Slider, float>> _sliders = new Dictionary<string, RavenMOption<Slider, float>>();
		private static ConfigFile keybindConfig;

		private static bool showHUD = false;

		public static Dictionary<OptionKeybind,ConfigEntry<KeyCode>> setKeybinds = new Dictionary<OptionKeybind, ConfigEntry<KeyCode>>();
		[HarmonyPatch(typeof(Options), "Awake")]
        public class OptionsAwakePatch
        {
            static void Postfix()
            {
                if (Options.instance == null)
                {
                    return;
                }
				keybindConfig = new ConfigFile(System.IO.Path.Combine(Paths.ConfigPath, "RavenMKeybinds.cfg"), false);
				CreateToggleOption("Show RavenM HUD", true, OptionTypes.Toggle, RavenMOptions.ShowRavenMHUD);
				CreateToggleOption("Voice Chat Volume", 0.9f, OptionTypes.Slider, RavenMOptions.VoiceChatVolume);
				CreateToggleOption<Action>("<color=yellow>Click on me to open the Keybinds File</color>", OnClickKeybindLabel, OptionTypes.Label, RavenMOptions.LazyKeybindLabel);
				AddKeybindToConfig(KeyCode.CapsLock, OptionKeybind.VoiceChatButton);
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
				if(index == 1)
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
				if(saveValue)
					UIToggle.Save();
				showHUD = GetOptionWithName<bool>(UIToggle.id, OptionTypes.Toggle);
			}
			foreach (RavenMOption<Slider, float> UISlider in _sliders.Values)
			{
				if (saveValue)
					UISlider.Save();
				IngameNetManager.instance.VoiceChatVolume = GetOptionWithName<float>(UISlider.id, OptionTypes.Slider);
			}
			IngameNetManager.instance.VoiceChatKeybind = setKeybinds[OptionKeybind.VoiceChatButton].Value;
		}
		//public static void SetOptionWithName<T>(string name,T targetValue,OptionTypes type)
  //      {
  //          if (!_toggles.ContainsKey(name) || !_sliders.ContainsKey(name))
		//	{
		//		return;
  //          }
  //          switch (type)
  //          {
		//		case OptionTypes.Toggle:
		//			var toggleOption = _toggles.FirstOrDefault(x => x.Key == name).Value;
		//			if(targetValue.GetType() != typeof(bool))
  //                  {
		//				Plugin.logger.LogError($"Failed to set option with name {name} because {targetValue.GetType()} should be bool!");
		//				return;
  //                  }
		//			toggleOption.value = (bool)(object)targetValue;
		//			break;
		//		case OptionTypes.Slider:
		//			var toggleSlider = _sliders.FirstOrDefault(x => x.Key == name).Value;
		//			if (targetValue.GetType() != typeof(float))
		//			{
		//				Plugin.logger.LogError($"Failed to set option with name {name} because {targetValue.GetType()} should be float!");
		//				return;
		//			}
		//			toggleSlider.value = (float)(object)targetValue;
		//			break;

		//	}
  //      }
		public static T GetOptionWithName<T>(RavenMOptions option, OptionTypes type)
        {
			switch (type)
			{
				case OptionTypes.Toggle:
					var toggleOption = _toggles.FirstOrDefault(x => x.Value.id == option).Value;
					if(toggleOption == null)
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
		public enum RavenMOptions
		{
			ShowRavenMHUD,
			VoiceChatVolume,
			LazyKeybindLabel,
			RebindKeyA
		}
		public enum OptionTypes
        {
			Toggle,
			Slider,
			Dropdown,
			Label,
			Keybind
        }
		public enum OptionKeybind
		{
			VoiceChatButton
		}
		private static void CreateToggleOption<T>(string name,T defaultValue,OptionTypes type,RavenMOptions option) {
			if (_toggles.ContainsKey(name) || _sliders.ContainsKey(name))
			{
				Plugin.logger.LogError($"Toggle with the name {name} already exists!");
				return;
            }
			RectTransform target = Options.instance.gameOptions.transform.Find("Game Panel/Scroll View/Viewport/Content").GetComponent<RectTransform>();
			ConfigEntry<T> ravenMConfig = null;
			if (type != OptionTypes.Label)
            {
				ravenMConfig = keybindConfig.Bind("RavenMKeybinds", option.ToString(), defaultValue);
            }
			switch (type)
            {
				case OptionTypes.Toggle:
					RectTransform exampleSettingToggle = Options.instance.gameOptions.transform.Find("Game Panel/Scroll View/Viewport/Content/Hitmarkers").GetComponent<RectTransform>();
					GameObject newTarget = GameObject.Instantiate(exampleSettingToggle.gameObject, target);
					newTarget.GetComponent<Text>().text = name;
					MonoBehaviour.Destroy(newTarget.GetComponent<OptionToggle>());
					RavenMOptionToggle ravenMToggle = newTarget.AddComponent<RavenMOptionToggle>();
					ravenMToggle.name = name;
					ravenMToggle.defaultValue = (bool)(object)defaultValue;
					ravenMToggle.id = option;
					ravenMToggle.configEntry = ravenMConfig as ConfigEntry<bool>;
					_toggles[name] = ravenMToggle;
					break;
				case OptionTypes.Slider:
					RectTransform exampleSettingSlider = Options.instance.gameOptions.transform.Find("Game Panel/Scroll View/Viewport/Content/SFX Volume").GetComponent<RectTransform>();
					GameObject newTarget2 = GameObject.Instantiate(exampleSettingSlider.gameObject, target);
					newTarget2.GetComponent<Text>().text = name;
					MonoBehaviour.Destroy(newTarget2.GetComponent<OptionSlider>());
					RavenMOptionSlider ravenMSlider = newTarget2.AddComponent<RavenMOptionSlider>();
					ravenMSlider.name = name;
					ravenMSlider.defaultValue = (float)(object)defaultValue;
					ravenMSlider.id = option;
					ravenMSlider.configEntry = ravenMConfig as ConfigEntry<float>;
					_sliders[name] = ravenMSlider;
					break;
				case OptionTypes.Label:
					RectTransform exampleSettingToggle2 = Options.instance.gameOptions.transform.Find("Game Panel/Scroll View/Viewport/Content/Hitmarkers").GetComponent<RectTransform>();
					GameObject newTarget3 = GameObject.Instantiate(exampleSettingToggle2.gameObject, target);
					newTarget3.GetComponent<Text>().supportRichText = true;
					newTarget3.GetComponent<Text>().text = name;
					GameObject.Destroy(newTarget3.transform.GetChild(0).gameObject);
					// Delete the Toggle to only have the Text
                    MonoBehaviour.Destroy(newTarget3.GetComponent<OptionToggle>());
					if (defaultValue != null)
					{
						Button button = newTarget3.AddComponent<Button>();
						button.onClick.AddListener(delegate {
							((Action)(object)defaultValue)();
						});
					}
					break;
			}
		}
		private static void AddKeybindToConfig(KeyCode defaultKeyCode,OptionKeybind keybind)
        {
			// Probably not a good idea
            setKeybinds.Add(keybind,keybindConfig.Bind("RavenMKeybinds",keybind.ToString(), defaultKeyCode));
        }
		public enum SlideId
		{
			E,
		}
		public abstract class RavenMOption<T,U> : MonoBehaviour
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
                if (hasValue) {
					this.value = savedValue.Value;
                }
                else
                {
					this.value = this.defaultValue;
					Plugin.logger.LogError($"Couldn't load config for {sectionName} {this.id.ToString()} and set to default value {this.defaultValue}");
				}

			}

		}
		public class RavenMOptionToggle : RavenMOption<Toggle,bool>
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
