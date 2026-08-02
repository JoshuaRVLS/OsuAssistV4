using Newtonsoft.Json;
using Osussist.src.config.objects;
using Osussist.src.utils;
using System.Text;
using WindowsInput.Native;

namespace Osussist.src.config
{
	public class Config
	{
		public const string LegitProfileFile = "legit.json";
		public const string RageProfileFile = "rage.json";
		private const string ActiveProfileFile = "active-profile.txt";

		private Logger logger = Logger.LoggingInstance;
		public static string currentConfigFile { get; set; }
		public static Config configInstance { get; set; }
		public static ConfigObject config { get; private set; }
		public static string configFolder { get; set; } = Path.Combine(Directory.GetCurrentDirectory(), "./config");

		public Config()
		{
			if (!Directory.Exists(configFolder))
				Directory.CreateDirectory(configFolder);

			config = new ConfigObject();
			configInstance = this;
		}

		public bool LoadActiveProfile()
		{
			EnsureProfiles();
			string profile = ReadActiveProfile();
			return LoadProfile(profile);
		}

		public bool LoadProfile(string fileName)
		{
			if (!IsManagedProfile(fileName))
				return false;

			EnsureProfiles();
			if (IsManagedProfile(currentConfigFile)
				&& !string.Equals(currentConfigFile, fileName, StringComparison.OrdinalIgnoreCase))
			{
				Save(currentConfigFile);
			}
			if (!Load(fileName))
				return false;

			config.aimbotsettings.algorithm = MouseAlgorithms.Linear;
			WriteActiveProfile(fileName);
			return true;
		}

		public bool SaveCurrent()
		{
			string fileName = IsManagedProfile(currentConfigFile) ? currentConfigFile : ReadActiveProfile();
			return Save(fileName);
		}

		public bool Load(string fileName)
		{
			string filePath = Path.Combine(configFolder, fileName);

			if (File.Exists(filePath) && Path.GetExtension(filePath).ToLower() == ".json")
			{
				try
				{
					string json = File.ReadAllText(filePath);
					config = JsonConvert.DeserializeObject<ConfigObject>(json) ?? new ConfigObject();
					logger.Info("Utils.Config", "Config file loaded successfully.");
					currentConfigFile = fileName;
					if (IsManagedProfile(fileName))
						WriteActiveProfile(fileName);
					return true;
				}
				catch (Exception e)
				{
					logger.Error("Utils.Config", $"Failed to load config file: {e.Message}");
					config = new ConfigObject();
					return false;
				}
			}
			else
			{
				logger.Error("Utils.Config", "Config file does not exist or is not a JSON file.");
				config = new ConfigObject();
				return false;
			}
		}

		public bool Reset(string fileName)
		{
			try
			{
				string filePath = Path.Combine(configFolder, fileName);
				File.Delete(filePath);

				ConfigObject tempConfig = new ConfigObject();
				char[] json = JsonConvert.SerializeObject(tempConfig, Formatting.Indented).ToCharArray();
				File.WriteAllBytes(filePath, Encoding.UTF8.GetBytes(json));
				logger.Info("Utils.Config", "Config file reset successfully.");
				return true;
			}
			catch (Exception e)
			{
				logger.Error("Utils.Config", $"Failed to reset config file: {e.Message}");
				return false;
			}
		}

		public bool Delete(string fileName)
		{
			try
			{
				string filePath = Path.Combine(configFolder, fileName);
				File.Delete(filePath);
				logger.Info("Utils.Config", "Config file deleted successfully.");
				return true;
			}
			catch (Exception e)
			{
				logger.Error("Utils.Config", $"Failed to delete config file: {e.Message}");
				return false;
			}
		}

		public bool Save(string fileName)
		{
			try
			{
				string filePath = Path.Combine(configFolder, fileName);

				char[] json = JsonConvert.SerializeObject(config, Formatting.Indented).ToCharArray();
				File.WriteAllBytes(filePath, Encoding.UTF8.GetBytes(json));
				currentConfigFile = fileName;
				logger.Info("Utils.Config", "Config file saved successfully.");
				return true;
			}
			catch (Exception e)
			{
				logger.Error("Utils.Config", $"Failed to save config file: {e.Message}");
				return false;
			}
		}

		public bool Create(string fileName)
		{
			try
			{
				string filePath = Path.Combine(configFolder, fileName);

				ConfigObject tempConfig = new ConfigObject();
				char[] json = JsonConvert.SerializeObject(tempConfig, Formatting.Indented).ToCharArray();
				File.WriteAllBytes(filePath, Encoding.UTF8.GetBytes(json));
				logger.Info("Utils.Config", "Config file created successfully.");
				return true;
			}
			catch (Exception e)
			{
				logger.Error("Utils.Config", $"Failed to create config file: {e.Message}");
				return false;
			}
		}

		public static List<string> GetConfigFiles()
		{
			string filePath = Path.Combine(configFolder);

			var allFiles = Directory.GetFiles(filePath);
			List<string> configFiles = new List<string>();
			foreach (string file in allFiles)
			{
				if (Path.GetExtension(file).ToLower() == ".json")
					configFiles.Add(Path.GetFileName(file));
			}

			return configFiles;
		}

		private void EnsureProfiles()
		{
			EnsureProfile(LegitProfileFile, CreateLegitProfile());
			EnsureProfile(RageProfileFile, CreateRageProfile());
		}

		private void EnsureProfile(string fileName, ConfigObject profile)
		{
			string filePath = Path.Combine(configFolder, fileName);
			if (File.Exists(filePath))
				return;

			char[] json = JsonConvert.SerializeObject(profile, Formatting.Indented).ToCharArray();
			File.WriteAllBytes(filePath, Encoding.UTF8.GetBytes(json));
		}

		private string ReadActiveProfile()
		{
			try
			{
				string activeProfilePath = Path.Combine(configFolder, ActiveProfileFile);
				if (File.Exists(activeProfilePath))
				{
					string profile = File.ReadAllText(activeProfilePath).Trim();
					if (IsManagedProfile(profile))
						return profile;
				}
			}
			catch (Exception e)
			{
				logger.Error("Utils.Config", $"Failed to read active profile: {e.Message}");
			}

			return LegitProfileFile;
		}

		private void WriteActiveProfile(string fileName)
		{
			try
			{
				File.WriteAllText(Path.Combine(configFolder, ActiveProfileFile), fileName, Encoding.UTF8);
			}
			catch (Exception e)
			{
				logger.Error("Utils.Config", $"Failed to save active profile: {e.Message}");
			}
		}

		private static bool IsManagedProfile(string fileName)
		{
			return string.Equals(fileName, LegitProfileFile, StringComparison.OrdinalIgnoreCase)
				|| string.Equals(fileName, RageProfileFile, StringComparison.OrdinalIgnoreCase);
		}

		private static ConfigObject CreateLegitProfile()
		{
			var profile = new ConfigObject();
			profile.aimbotenabled = true;
			profile.aimbotsettings.fovsize = 300;
			profile.aimbotsettings.strength = 0.57f;
			profile.aimbotsettings.algorithm = MouseAlgorithms.Linear;
			profile.relaxenabled = true;
			profile.relaxsettings.hardrockenabled = false;
			return profile;
		}

		private static ConfigObject CreateRageProfile()
		{
			var profile = new ConfigObject();
			profile.aimbotenabled = true;
			profile.aimbotsettings.fovsize = 1000;
			profile.aimbotsettings.strength = 0.8042f;
			profile.aimbotsettings.algorithm = MouseAlgorithms.Linear;
			profile.relaxenabled = true;
			profile.relaxsettings.hardrockenabled = false;
			return profile;
		}
	}
	public class ConfigObject
	{
		// Osu settings
		[JsonProperty("osusettings")]
		public OsuSettings osusettings { get; set; } = new OsuSettings();

		// Aimbot settings
		[JsonProperty("aimbotenabled")]
		public bool aimbotenabled { get; set; } = false;
		[JsonProperty("aimbotsettings")]
		public AimbotConfig aimbotsettings { get; set; } = new AimbotConfig();

		// Relax settings
		[JsonProperty("relaxenabled")]
		public bool relaxenabled { get; set; } = false;
		[JsonProperty("relaxsettings")]
		public RelaxConfig relaxsettings { get; set; } = new RelaxConfig();

		// Keybindings
		[JsonProperty("keybindings")]
		public Keybindings keybindings { get; set; } = new Keybindings();

		// Overrides
		[JsonProperty("overrides")]
		public Overrides overrides { get; set; } = new Overrides();
	}

	public class Overrides
	{
		public bool overrideaimbot { get; set; } = false;
	}

	public class OsuSettings
	{
		public int audiooffset { get; set; } = 0;
		public float sensitivity { get; set; } = 1f;
	}

	public class Keybindings
	{
		public VirtualKeyCode aimbotkey { get; set; } = VirtualKeyCode.F1;
		public VirtualKeyCode relaxkey { get; set; } = VirtualKeyCode.F2;
		public VirtualKeyCode primarykey { get; set; } = VirtualKeyCode.VK_Z;
		public VirtualKeyCode secondarykey { get; set; } = VirtualKeyCode.VK_X;
	}

	public class AimbotConfig
	{
		public int fovsize { get; set; } = 400;
		public int minarea { get; set; } = 10;
		public int similarity { get; set; } = 0;
		public int smoothing { get; set; } = 100;
		public float strength { get; set; } = 0.07f;
		public int hitobjectradius { get; set; } = 50;
		public int pullawaydistance { get; set; } = 200;
		public bool hardrockenabled { get; set; } = false;
		public MouseAlgorithms algorithm { get; set; } = MouseAlgorithms.Linear;
		public RgbColor cursorcolor { get; set; } = new RgbColor(221, 50, 50);
		public RgbColor targetcolor { get; set; } = new RgbColor(255, 0, 220);
		public VarianceInt movementdelay { get; set; } = new VarianceInt(1, 5);
	}

	public class RelaxConfig
	{
		public bool hardrockenabled { get; set; } = false;
		public float hitscanmultiplier { get; set; } = 0.9f;
		public int hitscanmaxdistance { get; set; } = 30;
		public int hitscanradiusadd { get; set; } = 50;
		public int maxsingletapbpm { get; set; } = 250;
		public PlayStyles playstyle { get; set; } = PlayStyles.Alternate;
	}
}
 
