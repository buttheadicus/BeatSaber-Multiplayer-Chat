using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using System.Security;
using System.Security.Permissions;
using System.Threading.Tasks;
using BeatSaber.AvatarCore;
using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components;
using BeatSaberMarkupLanguage.Components.Settings;
using BeatSaberMarkupLanguage.Util;
using BeatSaberMarkupLanguage.ViewControllers;
using HMUI;
using HarmonyLib;
using IPA;
using IPA.Config;
using IPA.Config.Data;
using IPA.Config.Stores;
using IPA.Config.Stores.Attributes;
using IPA.Loader;
using IPA.Logging;
using IPA.Utilities;
using LiteNetLib.Utils;
using Microsoft.CodeAnalysis;
using MultiplayerCore.Networking;
using MultiplayerCore.Objects;
using MultiplayerCore.Players;
using MultiplayerExtensions.Environment;
using MultiplayerExtensions.Environments;
using MultiplayerExtensions.Environments.Lobby;
using MultiplayerExtensions.Installers;
using MultiplayerExtensions.Patchers;
using MultiplayerExtensions.Players;
using MultiplayerExtensions.UI;
using MultiplayerExtensions.Utilities;
using SiraUtil.Affinity;
using SiraUtil.Extras;
using SiraUtil.Logging;
using SiraUtil.Objects.Multiplayer;
using SiraUtil.Web.SiraSync;
using SiraUtil.Zenject;
using TMPro;
using Tweening;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Zenject;
using Zenject.Internal;

[assembly: CompilationRelaxations(8)]
[assembly: RuntimeCompatibility(WrapNonExceptionThrows = true)]
[assembly: Debuggable(DebuggableAttribute.DebuggingModes.IgnoreSymbolStoreSequencePoints)]
[assembly: System.Runtime.CompilerServices.IgnoresAccessChecksTo("BeatSaber.AvatarCore")]
[assembly: System.Runtime.CompilerServices.IgnoresAccessChecksTo("BGLib.AppFlow")]
[assembly: System.Runtime.CompilerServices.IgnoresAccessChecksTo("Interactable")]
[assembly: System.Runtime.CompilerServices.IgnoresAccessChecksTo("Main")]
[assembly: System.Runtime.CompilerServices.IgnoresAccessChecksTo("MultiplayerCore")]
[assembly: TargetFramework(".NETFramework,Version=v4.7.2", FrameworkDisplayName = ".NET Framework 4.7.2")]
[assembly: AssemblyCompany("MultiplayerExtensions")]
[assembly: AssemblyConfiguration("Release")]
[assembly: AssemblyFileVersion("1.1.0")]
[assembly: AssemblyInformationalVersion("Official-dev/1.37-9b5959b+9b5959bff9ffaba121c3d6a20f379e953803e2e2")]
[assembly: AssemblyProduct("MultiplayerExtensions")]
[assembly: AssemblyTitle("MultiplayerExtensions")]
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
[assembly: AssemblyVersion("1.1.0.0")]
[module: UnverifiableCode]
[module: System.Runtime.CompilerServices.RefSafetyRules(11)]
namespace Microsoft.CodeAnalysis
{
	[CompilerGenerated]
	[Microsoft.CodeAnalysis.Embedded]
	internal sealed class EmbeddedAttribute : Attribute
	{
	}
}
namespace System.Runtime.CompilerServices
{
	[CompilerGenerated]
	[Microsoft.CodeAnalysis.Embedded]
	[AttributeUsage(AttributeTargets.Class | AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Event | AttributeTargets.Parameter | AttributeTargets.ReturnValue | AttributeTargets.GenericParameter, AllowMultiple = false, Inherited = false)]
	internal sealed class NullableAttribute : Attribute
	{
		public readonly byte[] NullableFlags;

		public NullableAttribute(byte P_0)
		{
			NullableFlags = new byte[1] { P_0 };
		}

		public NullableAttribute(byte[] P_0)
		{
			NullableFlags = P_0;
		}
	}
	[CompilerGenerated]
	[Microsoft.CodeAnalysis.Embedded]
	[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Method | AttributeTargets.Interface | AttributeTargets.Delegate, AllowMultiple = false, Inherited = false)]
	internal sealed class NullableContextAttribute : Attribute
	{
		public readonly byte Flag;

		public NullableContextAttribute(byte P_0)
		{
			Flag = P_0;
		}
	}
	[CompilerGenerated]
	[Microsoft.CodeAnalysis.Embedded]
	[AttributeUsage(AttributeTargets.Module, AllowMultiple = false, Inherited = false)]
	internal sealed class RefSafetyRulesAttribute : Attribute
	{
		public readonly int Version;

		public RefSafetyRulesAttribute(int P_0)
		{
			Version = P_0;
		}
	}
}
namespace MultiplayerExtensions
{
	public class Config
	{
		public static readonly Color DefaultPlayerColor = new Color(0.031f, 0.752f, 1f);

		public virtual bool SoloEnvironment { get; set; }

		public virtual bool SideBySide { get; set; }

		public virtual float SideBySideDistance { get; set; } = 4f;

		public virtual bool DisableAvatarConstraints { get; set; }

		public virtual bool DisableMultiplayerPlatforms { get; set; }

		public virtual bool DisableMultiplayerLights { get; set; }

		public virtual bool DisableMultiplayerObjects { get; set; }

		public virtual bool DisableMultiplayerColors { get; set; }

		public virtual bool DisablePlatformMovement { get; set; }

		public virtual bool MissLighting { get; set; }

		public virtual bool PersonalMissLightingOnly { get; set; }

		[UseConverter(typeof(ColorConverter))]
		public virtual Color PlayerColor { get; set; } = DefaultPlayerColor;

		[UseConverter(typeof(ColorConverter))]
		public virtual Color MissColor { get; set; } = new Color(1f, 0f, 0f);
	}
	[Plugin(/*Could not decode attribute arguments.*/)]
	public class Plugin
	{
		public const string ID = "com.goobwabber.multiplayerextensions";

		internal static Logger Logger;

		internal static Config Config;

		private readonly Harmony _harmony;

		private readonly PluginMetadata _metadata;

		[Init]
		public Plugin(Logger logger, Config conf, Zenjector zenjector, PluginMetadata pluginMetadata)
		{
			//IL_0014: Unknown result type (might be due to invalid IL or missing references)
			//IL_001e: Expected O, but got Unknown
			Config config = GeneratedStore.Generated<Config>(conf, true);
			_harmony = new Harmony("com.goobwabber.multiplayerextensions");
			_metadata = pluginMetadata;
			Logger = logger;
			Config = config;
			zenjector.UseMetadataBinder<Plugin>();
			zenjector.UseLogger(logger);
			zenjector.UseSiraSync(SiraSyncServiceType.GitHub, "Goobwabber", "MultiplayerExtensions");
			zenjector.Install<MpexAppInstaller>(Location.App, new object[1] { config });
			zenjector.Install<MpexMenuInstaller>(Location.Menu, Array.Empty<object>());
			zenjector.Install<MpexLobbyInstaller, MultiplayerLobbyInstaller>(Array.Empty<object>());
			zenjector.Install<MpexGameInstaller>(Location.MultiplayerCore, Array.Empty<object>());
			zenjector.Install<MpexLocalActivePlayerInstaller>(Location.MultiPlayer, Array.Empty<object>());
		}

		[OnEnable]
		public void OnEnable()
		{
			_harmony.PatchAll(_metadata.Assembly);
		}

		[OnDisable]
		public void OnDisable()
		{
			_harmony.UnpatchSelf();
		}
	}
}
namespace MultiplayerExtensions.Utilities
{
	public class ColorConverter : ValueConverter<Color>
	{
		public override Color FromValue(Value? value, object parent)
		{
			//IL_0039: Unknown result type (might be due to invalid IL or missing references)
			Text val = (Text)(object)((value is Text) ? value : null);
			if (val == null)
			{
				throw new ArgumentException("Argument not Text", "value");
			}
			Color result = default(Color);
			if (!ColorUtility.TryParseHtmlString(val.Value, ref result))
			{
				throw new ArgumentException("Could not parse HtmlString", "value");
			}
			return result;
		}

		public override Value? ToValue(Color obj, object parent)
		{
			//IL_0005: Unknown result type (might be due to invalid IL or missing references)
			return (Value?)(object)Value.Text("#" + ColorUtility.ToHtmlStringRGB(obj));
		}
	}
	public class SpriteManager : IInitializable, IDisposable
	{
		private readonly SiraLog _logger;

		public Sprite IconOculus64 { get; private set; }

		public Sprite IconSteam64 { get; private set; }

		public Sprite IconMeta64 { get; private set; }

		public Sprite IconToaster64 { get; private set; }

		internal SpriteManager(SiraLog logger)
		{
			_logger = logger;
		}

		public void Initialize()
		{
			IconOculus64 = GetSpriteFromResources("MultiplayerExtensions.Assets.IconOculus64.png");
			IconSteam64 = GetSpriteFromResources("MultiplayerExtensions.Assets.IconSteam64.png");
			IconMeta64 = GetSpriteFromResources("MultiplayerExtensions.Assets.IconMeta64.png");
			IconToaster64 = GetSpriteFromResources("MultiplayerExtensions.Assets.IconToaster64.png");
		}

		public void Dispose()
		{
			if ((Object)(object)IconOculus64 != (Object)null)
			{
				Object.Destroy((Object)(object)IconOculus64);
			}
			IconOculus64 = null;
			if ((Object)(object)IconSteam64 != (Object)null)
			{
				Object.Destroy((Object)(object)IconSteam64);
			}
			IconSteam64 = null;
			if ((Object)(object)IconMeta64 != (Object)null)
			{
				Object.Destroy((Object)(object)IconMeta64);
			}
			IconMeta64 = null;
			if ((Object)(object)IconToaster64 != (Object)null)
			{
				Object.Destroy((Object)(object)IconToaster64);
			}
			IconToaster64 = null;
		}

		private Sprite GetSpriteFromResources(string resourcePath, float pixelsPerUnit = 10f)
		{
			Sprite sprite = GetSprite(GetResource(Assembly.GetCallingAssembly(), resourcePath), pixelsPerUnit);
			if ((Object)(object)sprite == (Object)null)
			{
				return null;
			}
			((Object)sprite).name = resourcePath;
			return sprite;
		}

		private byte[] GetResource(Assembly asm, string resourceName)
		{
			Stream manifestResourceStream = asm.GetManifestResourceStream(resourceName);
			byte[] array = new byte[manifestResourceStream.Length];
			manifestResourceStream.Read(array, 0, (int)manifestResourceStream.Length);
			return array;
		}

		public Sprite? GetSprite(byte[]? data, float pixelsPerUnit = 100f, bool returnDefaultOnFail = true)
		{
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			//IL_0008: Expected O, but got Unknown
			//IL_0039: Unknown result type (might be due to invalid IL or missing references)
			//IL_0048: Unknown result type (might be due to invalid IL or missing references)
			try
			{
				Texture2D val = new Texture2D(2, 2);
				if (data == null || data.Length == 0)
				{
					return ReturnDefault(returnDefaultOnFail);
				}
				ImageConversion.LoadImage(val, data);
				return Sprite.Create(val, new Rect(0f, 0f, (float)((Texture)val).width, (float)((Texture)val).height), new Vector2(0f, 0f), pixelsPerUnit);
			}
			catch (Exception ex)
			{
				_logger.Warn("Caught unhandled exception " + ex.Message);
				return ReturnDefault(returnDefaultOnFail);
			}
			static Sprite? ReturnDefault(bool useDefault)
			{
				return null;
			}
		}
	}
}
namespace MultiplayerExtensions.UI
{
	[ViewDefinition("MultiplayerExtensions.UI.MpexEnvironmentViewController.bsml")]
	public class MpexEnvironmentViewController : BSMLAutomaticViewController
	{
		private Accessor<GameplaySetupViewController, bool> _showModifiers = FieldAccessor<GameplaySetupViewController, bool>.GetAccessor("_showModifiers");

		private Accessor<GameplaySetupViewController, bool> _showEnvironmentOverrideSettings = FieldAccessor<GameplaySetupViewController, bool>.GetAccessor("_showEnvironmentOverrideSettings");

		private Accessor<GameplaySetupViewController, bool> _showColorSchemesSettings = FieldAccessor<GameplaySetupViewController, bool>.GetAccessor("_showColorSchemesSettings");

		private Accessor<GameplaySetupViewController, bool> _showMultiplayer = FieldAccessor<GameplaySetupViewController, bool>.GetAccessor("_showMultiplayer");

		private GameplaySetupViewController _gameplaySetup;

		private Config _config;

		[UIComponent("side-by-side-distance-increment")]
		private GenericInteractableSetting _sideBySideDistanceIncrement;

		[UIValue("solo-environment")]
		private bool _soloEnvironment
		{
			get
			{
				return _config.SoloEnvironment;
			}
			set
			{
				_config.SoloEnvironment = value;
				_gameplaySetup.Setup(_showModifiers.Invoke(ref _gameplaySetup), _showEnvironmentOverrideSettings.Invoke(ref _gameplaySetup), _showColorSchemesSettings.Invoke(ref _gameplaySetup), _showMultiplayer.Invoke(ref _gameplaySetup), (PlayerSettingsPanelLayout)2);
				NotifyPropertyChanged("_soloEnvironment");
			}
		}

		[UIValue("side-by-side")]
		private bool _sideBySide
		{
			get
			{
				return _config.SideBySide;
			}
			set
			{
				_config.SideBySide = value;
				if ((Object)(object)_sideBySideDistanceIncrement != (Object)null)
				{
					_sideBySideDistanceIncrement.Interactable = value;
				}
				NotifyPropertyChanged("_sideBySide");
			}
		}

		[UIValue("side-by-side-distance")]
		private float _sideBySideDistance
		{
			get
			{
				return _config.SideBySideDistance;
			}
			set
			{
				_config.SideBySideDistance = value;
				NotifyPropertyChanged("_sideBySideDistance");
			}
		}

		[Inject]
		private void Construct(GameplaySetupViewController gameplaySetup, Config config)
		{
			_gameplaySetup = gameplaySetup;
			_config = config;
		}

		[UIAction("#post-parse")]
		private void PostParse()
		{
			_sideBySideDistanceIncrement.Interactable = _sideBySide;
		}
	}
	public class MpexGameplaySetup : NotifiableBase, IInitializable, IDisposable
	{
		public const string ResourcePath = "MultiplayerExtensions.UI.MpexGameplaySetup.bsml";

		private Accessor<GameplaySetupViewController, bool> _showModifiers = FieldAccessor<GameplaySetupViewController, bool>.GetAccessor("_showModifiers");

		private Accessor<GameplaySetupViewController, bool> _showEnvironmentOverrideSettings = FieldAccessor<GameplaySetupViewController, bool>.GetAccessor("_showEnvironmentOverrideSettings");

		private Accessor<GameplaySetupViewController, bool> _showColorSchemesSettings = FieldAccessor<GameplaySetupViewController, bool>.GetAccessor("_showColorSchemesSettings");

		private Accessor<GameplaySetupViewController, bool> _showMultiplayer = FieldAccessor<GameplaySetupViewController, bool>.GetAccessor("_showMultiplayer");

		private GameplaySetupViewController _gameplaySetup;

		private MultiplayerSettingsPanelController _multiplayerSettingsPanel;

		private MainFlowCoordinator _mainFlowCoordinator;

		private MpexSetupFlowCoordinator _setupFlowCoordinator;

		private Config _config;

		private SiraLog _logger;

		[UIObject("vert")]
		private GameObject _vert;

		[UIValue("solo-environment")]
		private bool _soloEnvironment
		{
			get
			{
				return _config.SoloEnvironment;
			}
			set
			{
				_config.SoloEnvironment = value;
				_gameplaySetup.Setup(_showModifiers.Invoke(ref _gameplaySetup), _showEnvironmentOverrideSettings.Invoke(ref _gameplaySetup), _showColorSchemesSettings.Invoke(ref _gameplaySetup), _showMultiplayer.Invoke(ref _gameplaySetup), (PlayerSettingsPanelLayout)2);
				NotifyPropertyChanged("_soloEnvironment");
			}
		}

		internal MpexGameplaySetup(GameplaySetupViewController gameplaySetup, MainFlowCoordinator mainFlowCoordinator, MpexSetupFlowCoordinator setupFlowCoordinator, Config config, SiraLog logger)
		{
			_gameplaySetup = gameplaySetup;
			_multiplayerSettingsPanel = ReflectionUtil.GetField<MultiplayerSettingsPanelController, GameplaySetupViewController>(gameplaySetup, "_multiplayerSettingsPanelController");
			_mainFlowCoordinator = mainFlowCoordinator;
			_setupFlowCoordinator = setupFlowCoordinator;
			_config = config;
			_logger = logger;
		}

		public void Initialize()
		{
			ZenjectSingleton<BSMLParser>.Instance.Parse(BeatSaberMarkupLanguage.Utilities.GetResourceContent(Assembly.GetExecutingAssembly(), "MultiplayerExtensions.UI.MpexGameplaySetup.bsml"), ((Component)_multiplayerSettingsPanel).gameObject, this);
			while (0 < _vert.transform.childCount)
			{
				_vert.transform.GetChild(0).SetParent(((Component)_multiplayerSettingsPanel).transform);
			}
		}

		public void Dispose()
		{
		}

		[UIAction("preferences-click")]
		private void PresentPreferences()
		{
			FlowCoordinator val = DeepestChildFlowCoordinator((FlowCoordinator)(object)_mainFlowCoordinator);
			_setupFlowCoordinator.parentFlowCoordinator = val;
			val.PresentFlowCoordinator((FlowCoordinator)(object)_setupFlowCoordinator, (Action)null, (AnimationDirection)0, false, false);
		}

		private FlowCoordinator DeepestChildFlowCoordinator(FlowCoordinator root)
		{
			FlowCoordinator childFlowCoordinator = root.childFlowCoordinator;
			if ((Object)(object)childFlowCoordinator == (Object)null)
			{
				return root;
			}
			if ((Object)(object)childFlowCoordinator.childFlowCoordinator == (Object)null || (Object)(object)childFlowCoordinator.childFlowCoordinator == (Object)(object)childFlowCoordinator)
			{
				return childFlowCoordinator;
			}
			return DeepestChildFlowCoordinator(childFlowCoordinator);
		}
	}
	[ViewDefinition("MultiplayerExtensions.UI.MpexMiscViewController.bsml")]
	public class MpexMiscViewController : BSMLAutomaticViewController
	{
		private Config _config;

		[UIValue("disable-avatar-constraints")]
		private bool _disableAvatarConstraints
		{
			get
			{
				return _config.DisableAvatarConstraints;
			}
			set
			{
				_config.DisableAvatarConstraints = value;
				NotifyPropertyChanged("_disableAvatarConstraints");
			}
		}

		[UIValue("disable-player-colors")]
		private bool _disablePlayerColors
		{
			get
			{
				return _config.DisableMultiplayerColors;
			}
			set
			{
				_config.DisableMultiplayerColors = value;
				NotifyPropertyChanged("_disablePlayerColors");
			}
		}

		[UIValue("disable-platform-movement")]
		private bool _disablePlatformMovement
		{
			get
			{
				return _config.DisablePlatformMovement;
			}
			set
			{
				_config.DisablePlatformMovement = value;
				NotifyPropertyChanged("_disablePlatformMovement");
			}
		}

		[Inject]
		private void Construct(Config config)
		{
			_config = config;
		}
	}
	[ViewDefinition("MultiplayerExtensions.UI.MpexSettingsViewController.bsml")]
	public class MpexSettingsViewController : BSMLAutomaticViewController
	{
		private Config _config;

		[UIComponent("personal-miss-lighting-toggle")]
		private GenericInteractableSetting _personalMissLightingToggle;

		[UIValue("hide-player-platforms")]
		private bool _hidePlayerPlatforms
		{
			get
			{
				return _config.DisableMultiplayerPlatforms;
			}
			set
			{
				_config.DisableMultiplayerPlatforms = value;
				NotifyPropertyChanged("_hidePlayerPlatforms");
			}
		}

		[UIValue("hide-player-lights")]
		private bool _hidePlayerLights
		{
			get
			{
				return _config.DisableMultiplayerLights;
			}
			set
			{
				_config.DisableMultiplayerLights = value;
				NotifyPropertyChanged("_hidePlayerLights");
			}
		}

		[UIValue("hide-player-objects")]
		private bool _hidePlayerObjects
		{
			get
			{
				return _config.DisableMultiplayerObjects;
			}
			set
			{
				_config.DisableMultiplayerObjects = value;
				NotifyPropertyChanged("_hidePlayerObjects");
			}
		}

		[UIValue("miss-lighting")]
		private bool _missLighting
		{
			get
			{
				return _config.MissLighting;
			}
			set
			{
				_config.MissLighting = value;
				if ((Object)(object)_personalMissLightingToggle != (Object)null)
				{
					_personalMissLightingToggle.Interactable = value;
				}
				NotifyPropertyChanged("_missLighting");
			}
		}

		[UIValue("personal-miss-lighting-only")]
		private bool _personalMissLightingOnly
		{
			get
			{
				return _config.PersonalMissLightingOnly;
			}
			set
			{
				_config.PersonalMissLightingOnly = value;
				NotifyPropertyChanged("_personalMissLightingOnly");
			}
		}

		[Inject]
		private void Construct(Config config)
		{
			_config = config;
		}

		[UIAction("#post-parse")]
		private void PostParse()
		{
			_personalMissLightingToggle.Interactable = _missLighting;
		}
	}
	public class MpexSetupFlowCoordinator : FlowCoordinator
	{
		internal FlowCoordinator parentFlowCoordinator;

		private MpexSettingsViewController _settingsViewController;

		private MpexEnvironmentViewController _environmentViewController;

		private MpexMiscViewController _miscViewController;

		private ILobbyGameStateController _gameStateController;

		[Inject]
		public void Construct(MainFlowCoordinator mainFlowCoordinator, MpexSettingsViewController settingsViewController, MpexEnvironmentViewController environmentViewController, MpexMiscViewController miscViewController, ILobbyGameStateController gameStateController)
		{
			parentFlowCoordinator = (FlowCoordinator)(object)mainFlowCoordinator;
			_settingsViewController = settingsViewController;
			_environmentViewController = environmentViewController;
			_miscViewController = miscViewController;
			_gameStateController = gameStateController;
		}

		protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
		{
			if (firstActivation)
			{
				((FlowCoordinator)this).SetTitle("Multiplayer Preferences", (AnimationType)1);
				((FlowCoordinator)this).showBackButton = true;
			}
			if (addedToHierarchy)
			{
				((FlowCoordinator)this).ProvideInitialViewControllers((ViewController)(object)_settingsViewController, (ViewController)(object)_environmentViewController, (ViewController)(object)_miscViewController, (ViewController)null, (ViewController)null);
				_gameStateController.gameStartedEvent += DismissGameStartedEvent;
			}
		}

		protected override void DidDeactivate(bool removedFromHierarchy, bool screenSystemDisabling)
		{
			if (removedFromHierarchy)
			{
				_gameStateController.gameStartedEvent -= DismissGameStartedEvent;
			}
		}

		private void DismissGameStartedEvent(ILevelGameplaySetupData obj)
		{
			parentFlowCoordinator.DismissFlowCoordinator((FlowCoordinator)(object)this, null, (AnimationDirection)0, immediately: true);
		}

		protected override void BackButtonWasPressed(ViewController topViewController)
		{
			parentFlowCoordinator.DismissFlowCoordinator((FlowCoordinator)(object)this, (AnimationDirection)0, (Action)null, false);
		}
	}
}
namespace MultiplayerExtensions.Players
{
	public class MpexPlayerData : INetSerializable
	{
		public Color Color { get; set; }

		public void Serialize(NetDataWriter writer)
		{
			//IL_0007: Unknown result type (might be due to invalid IL or missing references)
			writer.Put("#" + ColorUtility.ToHtmlStringRGB(Color));
		}

		public void Deserialize(NetDataReader reader)
		{
			//IL_0016: Unknown result type (might be due to invalid IL or missing references)
			//IL_000f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0014: Unknown result type (might be due to invalid IL or missing references)
			Color defaultPlayerColor = default(Color);
			if (!ColorUtility.TryParseHtmlString(reader.GetString(), ref defaultPlayerColor))
			{
				defaultPlayerColor = Config.DefaultPlayerColor;
			}
			Color = defaultPlayerColor;
		}
	}
	public class MpexPlayerManager : IInitializable
	{
		private ConcurrentDictionary<string, MpexPlayerData> _playerData = new ConcurrentDictionary<string, MpexPlayerData>();

		private readonly MpPacketSerializer _packetSerializer;

		private readonly IMultiplayerSessionManager _sessionManager;

		private readonly Config _config;

		public IReadOnlyDictionary<string, MpexPlayerData> Players => _playerData;

		public event Action<IConnectedPlayer, MpexPlayerData> PlayerConnectedEvent;

		internal MpexPlayerManager(MpPacketSerializer packetSerializer, IMultiplayerSessionManager sessionManager, Config config)
		{
			_packetSerializer = packetSerializer;
			_sessionManager = sessionManager;
			_config = config;
		}

		public void Initialize()
		{
			_sessionManager.SetLocalPlayerState("modded", true);
			_packetSerializer.RegisterCallback<MpexPlayerData>(HandlePlayerData);
			_sessionManager.playerConnectedEvent += HandlePlayerConnected;
		}

		public void Dispose()
		{
			_packetSerializer.UnregisterCallback<MpexPlayerData>();
		}

		private void HandlePlayerConnected(IConnectedPlayer player)
		{
			//IL_0012: Unknown result type (might be due to invalid IL or missing references)
			_sessionManager.Send<MpexPlayerData>(new MpexPlayerData
			{
				Color = _config.PlayerColor
			});
		}

		private void HandlePlayerData(MpexPlayerData packet, IConnectedPlayer player)
		{
			_playerData[player.userId] = packet;
			this.PlayerConnectedEvent(player, packet);
		}

		public bool TryGetPlayer(string userId, out MpexPlayerData player)
		{
			return _playerData.TryGetValue(userId, out player);
		}

		public MpexPlayerData? GetPlayer(string userId)
		{
			if (!_playerData.ContainsKey(userId))
			{
				return null;
			}
			return _playerData[userId];
		}
	}
}
namespace MultiplayerExtensions.Patches
{
	[HarmonyPatch]
	public class AvatarPoseRestrictionPatch
	{
		[HarmonyPrefix]
		[HarmonyPatch(typeof(LimitAvatarPoseRestriction), "RestrictPose")]
		private static bool DisableAvatarRestrictions(LimitAvatarPoseRestriction __instance, Vector3 headPosition, Vector3 leftHandPosition, Vector3 rightHandPosition, out Vector3 newHeadPosition, out Vector3 newLeftHandPosition, out Vector3 newRightHandPosition)
		{
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			//IL_0003: Unknown result type (might be due to invalid IL or missing references)
			//IL_000a: Unknown result type (might be due to invalid IL or missing references)
			//IL_000b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0012: Unknown result type (might be due to invalid IL or missing references)
			//IL_0013: Unknown result type (might be due to invalid IL or missing references)
			//IL_0029: Unknown result type (might be due to invalid IL or missing references)
			//IL_002a: Unknown result type (might be due to invalid IL or missing references)
			//IL_002b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0030: Unknown result type (might be due to invalid IL or missing references)
			//IL_0038: Unknown result type (might be due to invalid IL or missing references)
			//IL_0039: Unknown result type (might be due to invalid IL or missing references)
			//IL_003a: Unknown result type (might be due to invalid IL or missing references)
			//IL_003f: Unknown result type (might be due to invalid IL or missing references)
			newHeadPosition = headPosition;
			newLeftHandPosition = leftHandPosition;
			newRightHandPosition = rightHandPosition;
			if (!Plugin.Config.DisableAvatarConstraints)
			{
				return true;
			}
			newLeftHandPosition = __instance.LimitHandPositionRelativeToHead(leftHandPosition, headPosition);
			newRightHandPosition = __instance.LimitHandPositionRelativeToHead(rightHandPosition, headPosition);
			return false;
		}
	}
	[HarmonyPatch]
	public class PlatformMovementPatch
	{
		[HarmonyPrefix]
		[HarmonyPatch(typeof(MultiplayerVerticalPlayerMovementManager), "Update")]
		private static bool DisableVerticalPlayerMovement()
		{
			return !Plugin.Config.DisablePlatformMovement;
		}
	}
	[HarmonyPatch]
	public class ResumeSpawningPatch
	{
		[HarmonyPrefix]
		[HarmonyPatch(typeof(MultiplayerConnectedPlayerFacade), "ResumeSpawning")]
		private static bool DisableMultiplayerObjects()
		{
			if (Plugin.Config.DisableMultiplayerObjects)
			{
				return false;
			}
			return true;
		}
	}
}
namespace MultiplayerExtensions.Patchers
{
	[HarmonyPatch]
	public class AvatarPlacePatcher : IAffinity
	{
		private readonly MenuEnvironmentManager _environmentManager;

		private static readonly MethodInfo _addMethod = typeof(List<MultiplayerLobbyAvatarPlace>).GetMethod("Add");

		private static readonly MethodInfo _setupAvatarPlaceMethod = SymbolExtensions.GetMethodInfo((Expression<Action>)(() => SetupAvatarPlace(null, 0)));

		internal AvatarPlacePatcher(MenuEnvironmentManager environmentManager)
		{
			_environmentManager = environmentManager;
		}

		[HarmonyTranspiler]
		[HarmonyPatch(typeof(MultiplayerLobbyAvatarPlaceManager), "SpawnAllPlaces")]
		private static IEnumerable<CodeInstruction> SpawnAllPlaces(IEnumerable<CodeInstruction> instructions)
		{
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			//IL_0020: Unknown result type (might be due to invalid IL or missing references)
			//IL_0026: Expected O, but got Unknown
			//IL_0039: Unknown result type (might be due to invalid IL or missing references)
			//IL_003f: Expected O, but got Unknown
			//IL_004b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0051: Expected O, but got Unknown
			return new CodeMatcher(instructions, (ILGenerator)null).MatchForward(false, (CodeMatch[])(object)new CodeMatch[1]
			{
				new CodeMatch((OpCode?)OpCodes.Callvirt, (object)_addMethod, (string)null)
			}).Insert((CodeInstruction[])(object)new CodeInstruction[2]
			{
				new CodeInstruction(OpCodes.Ldloc_3, (object)null),
				new CodeInstruction(OpCodes.Callvirt, (object)_setupAvatarPlaceMethod)
			}).InstructionEnumeration();
		}

		private static MultiplayerLobbyAvatarPlace SetupAvatarPlace(MultiplayerLobbyAvatarPlace avatarPlace, int sortIndex)
		{
			((Component)avatarPlace).gameObject.GetComponent<MpexAvatarPlaceLighting>().SortIndex = sortIndex;
			return avatarPlace;
		}

		[AffinityPrefix]
		[AffinityPatch(typeof(MultiplayerLobbyAvatarPlaceManager), "SpawnAllPlaces", AffinityMethodType.Normal, null, new Type[] { })]
		private void SpawnAllPlacesPrefix(ILobbyStateDataModel ____lobbyStateDataModel)
		{
			((Component)((Component)_environmentManager).transform.Find("MultiplayerLobbyEnvironment").Find("LobbyAvatarPlace")).gameObject.GetComponent<MpexAvatarPlaceLighting>().SortIndex = ____lobbyStateDataModel.localPlayer.sortIndex;
		}
	}
	public class ColorSchemePatcher : IAffinity
	{
		private readonly GameplayCoreSceneSetupData _sceneSetupData;

		private readonly Config _config;

		internal ColorSchemePatcher(GameplayCoreSceneSetupData sceneSetupData, Config config)
		{
			_sceneSetupData = sceneSetupData;
			_config = config;
		}

		[AffinityPostfix]
		[AffinityPatch(typeof(PlayersSpecificSettingsAtGameStartModel), "GetPlayerSpecificSettingsForUserId", AffinityMethodType.Normal, null, new Type[] { })]
		private void SetConnectedPlayerColorScheme(ref PlayerSpecificSettingsNetSerializable __result)
		{
			//IL_001c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0022: Unknown result type (might be due to invalid IL or missing references)
			//IL_0028: Unknown result type (might be due to invalid IL or missing references)
			//IL_002e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0034: Unknown result type (might be due to invalid IL or missing references)
			//IL_003a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0040: Unknown result type (might be due to invalid IL or missing references)
			//IL_0045: Unknown result type (might be due to invalid IL or missing references)
			//IL_004a: Unknown result type (might be due to invalid IL or missing references)
			ColorScheme colorScheme = _sceneSetupData.colorScheme;
			if (_config.DisableMultiplayerColors)
			{
				__result.colorScheme = new ColorSchemeNetSerializable(colorScheme.saberAColor, colorScheme.saberBColor, colorScheme.obstaclesColor, colorScheme.environmentColor0, colorScheme.environmentColor1, colorScheme.environmentColor0Boost, colorScheme.environmentColor1Boost);
			}
		}
	}
	[HarmonyPatch]
	public class EnvironmentPatcher : IAffinity
	{
		private readonly GameScenesManager _scenesManager;

		private readonly Config _config;

		private readonly SiraLog _logger;

		private readonly PluginMetadata _chromaMetadata;

		private List<MonoBehaviour> _behavioursToInject = new List<MonoBehaviour>();

		private List<InstallerBase> _normalInstallers = new List<InstallerBase>();

		private List<Type> _normalInstallerTypes = new List<Type>();

		private List<ScriptableObjectInstaller> _scriptableObjectInstallers = new List<ScriptableObjectInstaller>();

		private List<MonoInstaller> _monoInstallers = new List<MonoInstaller>();

		private List<MonoInstaller> _installerPrefabs = new List<MonoInstaller>();

		private List<GameObject> _objectsToEnable = new List<GameObject>();

		internal EnvironmentPatcher(GameScenesManager scenesManager, Config config, SiraLog logger)
		{
			_scenesManager = scenesManager;
			_config = config;
			_logger = logger;
			_chromaMetadata = PluginManager.GetPlugin("Chroma");
		}

		[AffinityPostfix]
		[AffinityPriority(600)]
		[AffinityPatch(typeof(SceneDecoratorContext), "GetInjectableMonoBehaviours", AffinityMethodType.Normal, null, new Type[] { })]
		private void PreventEnvironmentInjection(SceneDecoratorContext __instance, List<MonoBehaviour> monoBehaviours, DiContainer ____container)
		{
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			//IL_000b: Unknown result type (might be due to invalid IL or missing references)
			Scene scene = ((Component)__instance).gameObject.scene;
			if (_scenesManager.IsSceneInStack("MultiplayerEnvironment") && _config.SoloEnvironment)
			{
				_logger.Info("Fixing bind conflicts on scene '" + ((Scene)(ref scene)).name + "'.");
				List<MonoBehaviour> removedBehaviours = new List<MonoBehaviour>();
				if (((Scene)(ref scene)).name.Contains("Environment") && !((Scene)(ref scene)).name.Contains("Multiplayer"))
				{
					removedBehaviours.AddRange(monoBehaviours.FindAll(delegate(MonoBehaviour behaviour)
					{
						ZenjectBinding val = (ZenjectBinding)(object)((behaviour is ZenjectBinding) ? behaviour : null);
						return val != null && val.Components.Any((Component c) => c is LightWithIdManager);
					}));
				}
				if (removedBehaviours.Any())
				{
					string text = string.Join(", ", removedBehaviours.Select(delegate(MonoBehaviour behaviour)
					{
						ZenjectBinding val = (ZenjectBinding)(object)((behaviour is ZenjectBinding) ? behaviour : null);
						return (val == null) ? (((object)behaviour).GetType()?.ToString() + " " + ((Object)((Component)behaviour).gameObject).name) : string.Join(", ", val.Components.Select((Component comp) => ((object)comp).GetType()?.ToString() + " " + ((Object)comp.gameObject).name));
					}));
					_logger.Info("Removing behaviours '" + text + "' from scene '" + ((Scene)(ref scene)).name + "'.");
					monoBehaviours.RemoveAll((MonoBehaviour monoBehaviour) => removedBehaviours.Contains(monoBehaviour));
				}
				if (((Scene)(ref scene)).name.Contains("Environment") && !((Scene)(ref scene)).name.Contains("Multiplayer"))
				{
					_logger.Info("Preventing environment injection.");
					_behavioursToInject = new List<MonoBehaviour>(monoBehaviours);
					monoBehaviours.Clear();
				}
			}
			else
			{
				_behavioursToInject.Clear();
			}
		}

		[AffinityPrefix]
		[AffinityPatch(typeof(SceneDecoratorContext), "InstallDecoratorInstallers", AffinityMethodType.Normal, null, new Type[] { })]
		private void PreventEnvironmentInstall(SceneDecoratorContext __instance, List<InstallerBase> ____normalInstallers, List<Type> ____normalInstallerTypes, List<ScriptableObjectInstaller> ____scriptableObjectInstallers, List<MonoInstaller> ____monoInstallers, List<MonoInstaller> ____installerPrefabs)
		{
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			//IL_000b: Unknown result type (might be due to invalid IL or missing references)
			Scene scene = ((Component)__instance).gameObject.scene;
			if (_scenesManager.IsSceneInStack("MultiplayerEnvironment") && _config.SoloEnvironment && ((Scene)(ref scene)).name.Contains("Environment") && !((Scene)(ref scene)).name.Contains("Multiplayer"))
			{
				_logger.Info("Preventing environment installation.");
				_normalInstallers = new List<InstallerBase>(____normalInstallers);
				_normalInstallerTypes = new List<Type>(____normalInstallerTypes);
				_scriptableObjectInstallers = new List<ScriptableObjectInstaller>(____scriptableObjectInstallers);
				_monoInstallers = new List<MonoInstaller>(____monoInstallers);
				_installerPrefabs = new List<MonoInstaller>(____installerPrefabs);
				____normalInstallers.Clear();
				____normalInstallerTypes.Clear();
				____scriptableObjectInstallers.Clear();
				____monoInstallers.Clear();
				____installerPrefabs.Clear();
			}
			else if (!_scenesManager.IsSceneInStack("MultiplayerEnvironment"))
			{
				_normalInstallers.Clear();
				_normalInstallerTypes.Clear();
				_scriptableObjectInstallers.Clear();
				_monoInstallers.Clear();
				_installerPrefabs.Clear();
			}
		}

		[AffinityPrefix]
		[AffinityPatch(typeof(GameScenesManager), "ActivatePresentedSceneRootObjects", AffinityMethodType.Normal, null, new Type[] { })]
		private void PreventEnvironmentActivation(List<string> scenesToPresent)
		{
			//IL_00a7: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ac: Unknown result type (might be due to invalid IL or missing references)
			//IL_0076: Unknown result type (might be due to invalid IL or missing references)
			//IL_007b: Unknown result type (might be due to invalid IL or missing references)
			_logger.Trace("ScenesToPresent " + string.Join(", ", scenesToPresent));
			string text = scenesToPresent.FirstOrDefault((string scene) => scene.Contains("Environment") && !scene.Contains("Multiplayer"));
			if (text == null)
			{
				return;
			}
			Scene sceneByName;
			if (scenesToPresent.Contains("MultiplayerEnvironment"))
			{
				_logger.Info("Preventing environment activation. (" + text + ")");
				sceneByName = SceneManager.GetSceneByName(text);
				_objectsToEnable = ((Scene)(ref sceneByName)).GetRootGameObjects().ToList();
				scenesToPresent.Remove(text);
				return;
			}
			_logger.Trace("Ensuring HUD is enabled");
			sceneByName = SceneManager.GetSceneByName(text);
			List<GameObject> list = ((Scene)(ref sceneByName)).GetRootGameObjects().ToList();
			foreach (GameObject item in list)
			{
				CoreGameHUDController componentInChildren = ((Component)item.transform).GetComponentInChildren<CoreGameHUDController>();
				if ((Object)(object)componentInChildren != (Object)null)
				{
					((Component)componentInChildren).gameObject.SetActive(true);
				}
			}
		}

		[AffinityPostfix]
		[AffinityPatch(typeof(GameObjectContext), "GetInjectableMonoBehaviours", AffinityMethodType.Normal, null, new Type[] { })]
		private void InjectEnvironment(GameObjectContext __instance, List<MonoBehaviour> monoBehaviours)
		{
			if (((Object)((Component)__instance).transform).name.Contains("LocalActivePlayer") && _config.SoloEnvironment)
			{
				_logger.Info("Injecting environment.");
				monoBehaviours.AddRange(_behavioursToInject);
			}
		}

		[AffinityPrefix]
		[AffinityPatch(typeof(DiContainer), "QueueForInject", AffinityMethodType.Normal, null, new Type[] { })]
		private bool IHateChromaTrackLaneRingInjection(DiContainer __instance, ref object instance)
		{
			bool flag = false;
			if (PluginManager.IsEnabled(_chromaMetadata) && _scenesManager.IsSceneInStack("MultiplayerEnvironment") && _config.SoloEnvironment)
			{
				object obj = instance;
				LightPairRotationEventEffect val = (LightPairRotationEventEffect)((obj is LightPairRotationEventEffect) ? obj : null);
				if (val != null)
				{
					StackTrace stackTrace = new StackTrace();
					StackFrame frame = stackTrace.GetFrame(4);
					MethodBase method = frame.GetMethod();
					_logger.Trace("DiContainer.QueueForInject called from method: " + method.DeclaringType.FullName + "." + method.Name + ", instance type: " + instance.GetType().FullName);
					if (method.DeclaringType.FullName.StartsWith("Chroma") && method.DeclaringType.FullName.EndsWith("RingAwakeInstantiator") && method.Name == "QueueInject")
					{
						_logger.Trace("Preventing TrackLaneRing " + ((Object)val).name + " injection, parent go name: " + ((Object)((Component)((Component)val).transform.parent).gameObject).name);
						((Component)((Component)val).transform.parent).gameObject.SetActive(false);
						return false;
					}
					_logger.Trace("Not preventing injection for LightPairRotationEventEffect " + ((Object)val).name);
				}
			}
			return true;
		}

		[AffinityPrefix]
		[AffinityPatch(typeof(Context), "InstallInstallers", AffinityMethodType.Normal, null, new Type[]
		{
			typeof(List<InstallerBase>),
			typeof(List<Type>),
			typeof(List<ScriptableObjectInstaller>),
			typeof(List<MonoInstaller>),
			typeof(List<MonoInstaller>)
		})]
		private void InstallEnvironment(Context __instance, List<InstallerBase> normalInstallers, List<Type> normalInstallerTypes, List<ScriptableObjectInstaller> scriptableObjectInstallers, List<MonoInstaller> installers, List<MonoInstaller> installerPrefabs)
		{
			GameObjectContext val = (GameObjectContext)(object)((__instance is GameObjectContext) ? __instance : null);
			if (val != null && ((Object)((Component)__instance).transform).name.Contains("LocalActivePlayer") && _config.SoloEnvironment)
			{
				_logger.Info("Installing environment.");
				normalInstallers.AddRange(_normalInstallers);
				normalInstallerTypes.AddRange(_normalInstallerTypes);
				scriptableObjectInstallers.AddRange(_scriptableObjectInstallers);
				installers.AddRange(_monoInstallers);
				installerPrefabs.AddRange(_installerPrefabs);
			}
		}

		[AffinityPrefix]
		[AffinityPatch(typeof(GameObjectContext), "InstallInstallers", AffinityMethodType.Normal, null, new Type[] { })]
		private void LoveYouCountersPlus(GameObjectContext __instance)
		{
			//IL_0060: Unknown result type (might be due to invalid IL or missing references)
			//IL_0066: Expected O, but got Unknown
			//IL_00aa: Unknown result type (might be due to invalid IL or missing references)
			//IL_00be: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c3: Unknown result type (might be due to invalid IL or missing references)
			if (((Object)((Component)__instance).transform).name.Contains("LocalActivePlayer") && _config.SoloEnvironment)
			{
				DiContainer property = ReflectionUtil.GetProperty<DiContainer, GameObjectContext>(__instance, "Container");
				CoreGameHUDController val = (CoreGameHUDController)_behavioursToInject.Find((MonoBehaviour x) => x is CoreGameHUDController);
				property.Unbind<CoreGameHUDController>();
				((FromBinderGeneric<CoreGameHUDController>)(object)property.Bind<CoreGameHUDController>()).FromInstance(val).AsSingle();
				CoreGameHUDController componentInChildren = ((Component)((Component)__instance).transform).GetComponentInChildren<CoreGameHUDController>();
				((Component)componentInChildren).gameObject.SetActive(false);
				MultiplayerPositionHUDController componentInChildren2 = ((Component)((Component)__instance).transform).GetComponentInChildren<MultiplayerPositionHUDController>();
				Transform transform = ((Component)componentInChildren2).transform;
				transform.position += new Vector3(0f, 0.01f, 0f);
			}
		}

		[AffinityPostfix]
		[AffinityPatch(typeof(GameObjectContext), "InstallSceneBindings", AffinityMethodType.Normal, null, new Type[] { })]
		private void ActivateEnvironment(GameObjectContext __instance)
		{
			if (!((Object)((Component)__instance).transform).name.Contains("LocalActivePlayer") || !_config.SoloEnvironment)
			{
				return;
			}
			_logger.Info("Activating environment.");
			foreach (GameObject item in _objectsToEnable)
			{
				_logger.Trace("Enabling GameObject: " + ((Object)item).name);
				item.SetActive(true);
			}
			Transform val = ((Component)__instance).transform.Find("IsActiveObjects");
			((Component)val.Find("Lasers")).gameObject.SetActive(false);
			((Component)val.Find("Construction")).gameObject.SetActive(false);
			((Component)val.Find("BigSmokePS")).gameObject.SetActive(false);
			((Component)val.Find("DustPS")).gameObject.SetActive(false);
			((Component)val.Find("DirectionalLights")).gameObject.SetActive(false);
			MultiplayerLocalActivePlayerFacade component = ((Component)((Component)__instance).transform).GetComponent<MultiplayerLocalActivePlayerFacade>();
			GameObject[] field = ReflectionUtil.GetField<GameObject[], MultiplayerLocalActivePlayerFacade>(component, "_activeOnlyGameObjects");
			IEnumerable<GameObject> source = field.Concat<GameObject>(_objectsToEnable);
			ReflectionUtil.SetField<MultiplayerLocalActivePlayerFacade, GameObject[]>(component, "_activeOnlyGameObjects", source.ToArray());
		}

		[HarmonyPostfix]
		[HarmonyPatch(typeof(Context), "InstallSceneBindings")]
		private static void HideOtherPlayerPlatforms(Context __instance)
		{
			if (((Object)((Component)__instance).transform).name.Contains("ConnectedPlayer"))
			{
				if (Plugin.Config.DisableMultiplayerPlatforms)
				{
					((Component)((Component)__instance).transform.Find("Construction")).gameObject.SetActive(false);
				}
				if (Plugin.Config.DisableMultiplayerLights)
				{
					((Component)((Component)__instance).transform.Find("Lasers")).gameObject.SetActive(false);
				}
			}
		}

		[HarmonyPrefix]
		[HarmonyPatch(typeof(EnvironmentSceneSetup), "InstallBindings")]
		private static bool RemoveDuplicateInstalls(EnvironmentSceneSetup __instance)
		{
			DiContainer property = ReflectionUtil.GetProperty<DiContainer, MonoInstallerBase>((MonoInstallerBase)(object)__instance, "Container");
			return !property.HasBinding<InitData>();
		}

		[AffinityPostfix]
		[AffinityPatch(typeof(GameplayCoreInstaller), "InstallBindings", AffinityMethodType.Normal, null, new Type[] { })]
		private void LightInjectionFixes(GameplayCoreInstaller __instance)
		{
			//IL_021e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0204: Unknown result type (might be due to invalid IL or missing references)
			//IL_0210: Unknown result type (might be due to invalid IL or missing references)
			//IL_0223: Unknown result type (might be due to invalid IL or missing references)
			//IL_024f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0235: Unknown result type (might be due to invalid IL or missing references)
			//IL_0241: Unknown result type (might be due to invalid IL or missing references)
			//IL_0254: Unknown result type (might be due to invalid IL or missing references)
			//IL_0258: Unknown result type (might be due to invalid IL or missing references)
			//IL_025a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0274: Unknown result type (might be due to invalid IL or missing references)
			//IL_027e: Expected O, but got Unknown
			//IL_0280: Unknown result type (might be due to invalid IL or missing references)
			//IL_0282: Unknown result type (might be due to invalid IL or missing references)
			//IL_0284: Unknown result type (might be due to invalid IL or missing references)
			//IL_0286: Unknown result type (might be due to invalid IL or missing references)
			if (!_config.SoloEnvironment || !_scenesManager.IsSceneInStack("MultiplayerEnvironment"))
			{
				_logger.Debug("Either SoloEnvironment disabled or MultiplayerEnvironment not in scene stack, returning");
				return;
			}
			_logger.Debug("Running SetEnvironmentColors Patch");
			DiContainer property = ReflectionUtil.GetProperty<DiContainer, MonoInstallerBase>((MonoInstallerBase)(object)__instance, "Container");
			if (PluginManager.IsEnabled(_chromaMetadata))
			{
				IEnumerable<TrackLaneRingsManager> enumerable = _objectsToEnable.SelectMany((GameObject gameObject) => ((Component)gameObject.transform).GetComponentsInChildren<TrackLaneRingsManager>());
				foreach (TrackLaneRingsManager item in enumerable)
				{
					if ((Object)(object)item == (Object)null || item.Rings == null)
					{
						continue;
					}
					TrackLaneRing[] rings = item.Rings;
					foreach (TrackLaneRing val in rings)
					{
						if ((Object)(object)val == (Object)null)
						{
							continue;
						}
						_logger.Trace("Fixing injection and enabling go " + ((Object)((Component)val).gameObject).name);
						List<MonoBehaviour> list = new List<MonoBehaviour>();
						ZenUtilInternal.GetInjectableMonoBehavioursUnderGameObject(((Component)val).gameObject, list);
						foreach (MonoBehaviour item2 in list)
						{
							property.Inject((object)item2);
						}
						((Component)val).gameObject.SetActive(true);
					}
				}
			}
			EnvironmentColorManager val2 = property.Resolve<EnvironmentColorManager>();
			property.Inject((object)val2);
			val2.Awake();
			IEnumerable<LightSwitchEventEffect> enumerable2 = _objectsToEnable.SelectMany((GameObject gameObject) => ((Component)gameObject.transform).GetComponentsInChildren<LightSwitchEventEffect>());
			if (enumerable2 == null || enumerable2.Count() == 0)
			{
				_logger.Warn("Could not get LightSwitchEventEffect, continuing");
				return;
			}
			foreach (LightSwitchEventEffect item3 in enumerable2)
			{
				item3._usingBoostColors = false;
				Color val3 = (item3._lightOnStart ? ColorSO.op_Implicit(item3._lightColor0) : ColorExtensions.ColorWithAlpha(item3._lightColor0.color, item3._offColorIntensity));
				Color val4 = (item3._lightOnStart ? ColorSO.op_Implicit(item3._lightColor0Boost) : ColorExtensions.ColorWithAlpha(item3._lightColor0Boost.color, item3._offColorIntensity));
				item3._colorTween = new ColorTween(val3, val3, (Action<Color>)item3.SetColor, 0f, (EaseType)1, 0f);
				item3.SetupTweenAndSaveOtherColors(val3, val3, val4, val4);
			}
		}
	}
	[HarmonyPatch]
	public class MenuEnvironmentPatcher : IAffinity
	{
		private readonly GameplaySetupViewController _gameplaySetup;

		private readonly EnvironmentsListModel _environmentsListModel;

		private readonly Config _config;

		private readonly SiraLog _logger;

		private EnvironmentInfoSO _originalEnvironmentInfo;

		internal MenuEnvironmentPatcher(GameplaySetupViewController gameplaySetup, EnvironmentsListModel environmentsListModel, Config config, SiraLog logger)
		{
			_gameplaySetup = gameplaySetup;
			_environmentsListModel = environmentsListModel;
			_config = config;
			_logger = logger;
		}

		[HarmonyPrefix]
		[HarmonyPatch(typeof(GameplaySetupViewController), "Setup")]
		private static void EnableEnvironmentTab(bool showModifiers, ref bool showEnvironmentOverrideSettings, bool showColorSchemesSettings, bool showMultiplayer, PlayerSettingsPanelLayout playerSettingsPanelLayout)
		{
			if (showMultiplayer)
			{
				showEnvironmentOverrideSettings = Plugin.Config.SoloEnvironment;
			}
		}

		[AffinityPrefix]
		[AffinityPatch(typeof(MultiplayerLevelScenesTransitionSetupDataSO), "Init", AffinityMethodType.Normal, null, new Type[] { })]
		private void SetEnvironmentScene(ref MultiplayerLevelScenesTransitionSetupDataSO __instance, ref BeatmapKey beatmapKey, ref BeatmapLevel beatmapLevel, ref EnvironmentInfoSO ____loadedMultiplayerEnvironmentInfo)
		{
			//IL_0017: Unknown result type (might be due to invalid IL or missing references)
			//IL_001c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0021: Unknown result type (might be due to invalid IL or missing references)
			//IL_0037: Unknown result type (might be due to invalid IL or missing references)
			//IL_0065: Unknown result type (might be due to invalid IL or missing references)
			if (_config.SoloEnvironment)
			{
				EnvironmentName environmentName = beatmapLevel.GetEnvironmentName(beatmapKey.beatmapCharacteristic, beatmapKey.difficulty);
				_originalEnvironmentInfo = __instance.GetOrLoadMultiplayerEnvironmentInfo();
				____loadedMultiplayerEnvironmentInfo = _environmentsListModel.GetEnvironmentInfoBySerializedNameSafe(EnvironmentName.op_Implicit(environmentName));
				if (_gameplaySetup.environmentOverrideSettings.overrideEnvironments)
				{
					____loadedMultiplayerEnvironmentInfo = _gameplaySetup.environmentOverrideSettings.GetOverrideEnvironmentInfoForType(____loadedMultiplayerEnvironmentInfo.environmentType);
				}
			}
		}

		[AffinityPostfix]
		[AffinityPatch(typeof(MultiplayerLevelScenesTransitionSetupDataSO), "Init", AffinityMethodType.Normal, null, new Type[] { })]
		private void ResetEnvironmentScene(ref EnvironmentInfoSO ____loadedMultiplayerEnvironmentInfo)
		{
			if (_config.SoloEnvironment)
			{
				____loadedMultiplayerEnvironmentInfo = _originalEnvironmentInfo;
			}
		}

		[AffinityPrefix]
		[AffinityPatch(typeof(ScenesTransitionSetupDataSO), "Init", AffinityMethodType.Normal, null, new Type[] { })]
		private void AddEnvironmentOverrides(ref SceneInfo[] scenes)
		{
			if (_config.SoloEnvironment && scenes.Any((SceneInfo scene) => ((Object)scene).name.Contains("Multiplayer")))
			{
				_logger.Debug("At least one scenes name contains Multiplayer, adding original env info");
				scenes = scenes.Take(1).Concat<SceneInfo>((IEnumerable<SceneInfo>)(object)new SceneInfo[1] { _originalEnvironmentInfo.sceneInfo }).Concat<SceneInfo>(scenes.Skip(1))
					.ToArray();
			}
		}
	}
	[HarmonyPatch]
	public class PlayerPositionPatcher : IAffinity
	{
		private readonly Config _config;

		internal PlayerPositionPatcher(Config config)
		{
			_config = config;
		}

		[AffinityPrefix]
		[AffinityPatch(typeof(MultiplayerLayoutProvider), "CalculateLayout", AffinityMethodType.Normal, null, new Type[] { })]
		private bool SideBySideLayout(ref MultiplayerPlayerLayout __result)
		{
			__result = (MultiplayerPlayerLayout)2;
			return !_config.SideBySide;
		}

		[HarmonyPrefix]
		[HarmonyPatch(typeof(MultiplayerConditionalActiveByLayout), "Start")]
		private static void SideBySideLayoutConfirm(MultiplayerConditionalActiveByLayout __instance, MultiplayerLayoutProvider ____layoutProvider)
		{
			//IL_000e: Unknown result type (might be due to invalid IL or missing references)
			if (Plugin.Config.SideBySide && (int)____layoutProvider.layout == 0)
			{
				__instance.HandlePlayersLayoutWasCalculated((MultiplayerPlayerLayout)2, 2);
			}
		}

		[HarmonyPrefix]
		[HarmonyPatch(typeof(MultiplayerConditionalActiveByLayout), "HandlePlayersLayoutWasCalculated")]
		private static void SideBySideObjectDisable(ref MultiplayerPlayerLayout layout)
		{
			if (Plugin.Config.SideBySide)
			{
				layout = (MultiplayerPlayerLayout)2;
			}
		}

		[AffinityPrefix]
		[AffinityPatch(typeof(MultiplayerPlayerPlacement), "GetOuterCirclePositionAngleForPlayer", AffinityMethodType.Normal, null, new Type[] { })]
		private bool SideBySideAngle(int playerIndex, int localPlayerIndex, ref float __result)
		{
			__result = (float)(playerIndex - localPlayerIndex) * 0.01f;
			return !_config.SideBySide;
		}

		[AffinityPrefix]
		[AffinityPatch(typeof(MultiplayerPlayerPlacement), "GetPlayerWorldPosition", AffinityMethodType.Normal, null, new Type[] { })]
		private bool SoloEnvironmentPosition(float outerCirclePositionAngle, ref Vector3 __result)
		{
			//IL_0020: Unknown result type (might be due to invalid IL or missing references)
			//IL_0025: Unknown result type (might be due to invalid IL or missing references)
			__result = new Vector3(outerCirclePositionAngle * 100f * _config.SideBySideDistance, 0f, 0f);
			return !_config.SideBySide;
		}
	}
}
namespace MultiplayerExtensions.Installers
{
	internal class MpexAppInstaller : Installer
	{
		private readonly Config _config;

		public MpexAppInstaller(Config config)
		{
			_config = config;
		}

		public override void InstallBindings()
		{
			((ScopeConcreteIdArgConditionCopyNonLazyBinder)((InstallerBase)this).Container.BindInstance<Config>(_config)).AsSingle();
			((ScopeConcreteIdArgConditionCopyNonLazyBinder)((InstallerBase)this).Container.BindInterfacesAndSelfTo<SpriteManager>()).AsSingle();
			((ScopeConcreteIdArgConditionCopyNonLazyBinder)((InstallerBase)this).Container.BindInterfacesAndSelfTo<MpexPlayerManager>()).AsSingle();
			((ScopeConcreteIdArgConditionCopyNonLazyBinder)((InstallerBase)this).Container.BindInterfacesAndSelfTo<EnvironmentPatcher>()).AsSingle();
		}
	}
	internal class MpexGameInstaller : Installer
	{
		public override void InstallBindings()
		{
			((ScopeConcreteIdArgConditionCopyNonLazyBinder)((InstallerBase)this).Container.BindInterfacesAndSelfTo<PlayerPositionPatcher>()).AsSingle();
			((ScopeConcreteIdArgConditionCopyNonLazyBinder)((InstallerBase)this).Container.BindInterfacesAndSelfTo<ColorSchemePatcher>()).AsSingle();
			((InstallerBase)this).Container.RegisterRedecorator(new LocalActivePlayerRegistration(DecorateLocalActivePlayerFacade));
			((InstallerBase)this).Container.RegisterRedecorator(new LocalActivePlayerDuelRegistration(DecorateLocalActivePlayerFacade));
			((InstallerBase)this).Container.RegisterRedecorator(new ConnectedPlayerRegistration(DecorateConnectedPlayerFacade));
			((InstallerBase)this).Container.RegisterRedecorator(new ConnectedPlayerDuelRegistration(DecorateConnectedPlayerFacade));
		}

		private MultiplayerLocalActivePlayerFacade DecorateLocalActivePlayerFacade(MultiplayerLocalActivePlayerFacade original)
		{
			if (Plugin.Config.MissLighting)
			{
				((Component)original).gameObject.AddComponent<MpexPlayerFacadeLighting>();
			}
			return original;
		}

		private MultiplayerConnectedPlayerFacade DecorateConnectedPlayerFacade(MultiplayerConnectedPlayerFacade original)
		{
			if (Plugin.Config.MissLighting && !Plugin.Config.PersonalMissLightingOnly)
			{
				((Component)original).gameObject.AddComponent<MpexPlayerFacadeLighting>();
			}
			((Component)original).gameObject.AddComponent<MpexConnectedObjectManager>();
			return original;
		}
	}
	internal class MpexLobbyInstaller : Installer
	{
		public override void InstallBindings()
		{
			((InstallerBase)this).Container.RegisterRedecorator(new LobbyAvatarPlaceRegistration(DecorateAvatarPlace));
			((InstallerBase)this).Container.RegisterRedecorator(new LobbyAvatarRegistration(DecorateAvatar));
		}

		private MultiplayerLobbyAvatarPlace DecorateAvatarPlace(MultiplayerLobbyAvatarPlace original)
		{
			((Component)original).gameObject.AddComponent<MpexAvatarPlaceLighting>();
			return original;
		}

		private MultiplayerLobbyAvatarController DecorateAvatar(MultiplayerLobbyAvatarController original)
		{
			GameObject gameObject = ((Component)((Component)original).transform.Find("AvatarCaption")).gameObject;
			gameObject.AddComponent<MpexAvatarNameTag>();
			return original;
		}
	}
	public class MpexLocalActivePlayerInstaller : MonoInstaller
	{
		public override void InstallBindings()
		{
			((ScopeConcreteIdArgConditionCopyNonLazyBinder)((MonoInstallerBase)this).Container.BindInterfacesAndSelfTo<MpexLevelEndActions>()).AsSingle();
			((FromBinderGeneric<EnvironmentContext>)(object)((MonoInstallerBase)this).Container.Bind<EnvironmentContext>()).FromInstance((EnvironmentContext)0).AsSingle();
		}
	}
	internal class MpexMenuInstaller : Installer
	{
		public override void InstallBindings()
		{
			((ScopeConcreteIdArgConditionCopyNonLazyBinder)((InstallerBase)this).Container.BindInterfacesAndSelfTo<AvatarPlacePatcher>()).AsSingle();
			((ScopeConcreteIdArgConditionCopyNonLazyBinder)((InstallerBase)this).Container.BindInterfacesAndSelfTo<MenuEnvironmentPatcher>()).AsSingle();
			((ScopeConcreteIdArgConditionCopyNonLazyBinder)((FromBinder)(object)((InstallerBase)this).Container.BindInterfacesAndSelfTo<MpexSetupFlowCoordinator>()).FromNewComponentOnNewGameObject()).AsSingle();
			((FromBinder)(object)((InstallerBase)this).Container.BindInterfacesAndSelfTo<MpexSettingsViewController>()).FromNewComponentAsViewController().AsSingle();
			((FromBinder)(object)((InstallerBase)this).Container.BindInterfacesAndSelfTo<MpexEnvironmentViewController>()).FromNewComponentAsViewController().AsSingle();
			((FromBinder)(object)((InstallerBase)this).Container.BindInterfacesAndSelfTo<MpexMiscViewController>()).FromNewComponentAsViewController().AsSingle();
			((ScopeConcreteIdArgConditionCopyNonLazyBinder)((InstallerBase)this).Container.BindInterfacesAndSelfTo<MpexGameplaySetup>()).AsSingle();
			GameObject gameObject = ((Component)((Component)((InstallerBase)this).Container.Resolve<MenuEnvironmentManager>()).transform.Find("MultiplayerLobbyEnvironment").Find("LobbyAvatarPlace")).gameObject;
			Object.Destroy((Object)(object)gameObject.GetComponent<MpexAvatarPlaceLighting>());
			((InstallerBase)this).Container.Inject((object)gameObject.AddComponent<MpexAvatarPlaceLighting>());
		}
	}
}
namespace MultiplayerExtensions.Objects
{
	public class MpexPlayerTableCell : IInitializable, IDisposable, IAffinity
	{
		private readonly ServerPlayerListViewController _playerListView;

		private readonly MpEntitlementChecker _entitlementChecker;

		private readonly ILobbyPlayersDataModel _playersDataModel;

		private readonly IMenuRpcManager _menuRpcManager;

		private static float alphaIsMe = 0.4f;

		private static float alphaIsNotMe = 0.2f;

		private static Color green = new Color(0f, 1f, 0f, 1f);

		private static Color yellow = new Color(0.125f, 0.75f, 1f, 1f);

		private static Color red = new Color(1f, 0f, 0f, 1f);

		private static Color normal = new Color(0.125f, 0.75f, 1f, 0.1f);

		internal MpexPlayerTableCell(ServerPlayerListViewController playerListView, NetworkPlayerEntitlementChecker entitlementChecker, ILobbyPlayersDataModel playersDataModel, IMenuRpcManager menuRpcManager)
		{
			_playerListView = playerListView;
			_entitlementChecker = entitlementChecker as MpEntitlementChecker;
			_playersDataModel = playersDataModel;
			_menuRpcManager = menuRpcManager;
		}

		public void Initialize()
		{
			_menuRpcManager.setIsEntitledToLevelEvent += HandleSetIsEntitledToLevel;
		}

		public void Dispose()
		{
			_menuRpcManager.setIsEntitledToLevelEvent -= HandleSetIsEntitledToLevel;
		}

		[AffinityPostfix]
		[AffinityPatch(typeof(GameServerPlayerTableCell), "SetData", AffinityMethodType.Normal, null, new Type[] { })]
		public void SetDataPostfix(IConnectedPlayer connectedPlayer, ILobbyPlayerData playerData, bool hasKickPermissions, bool allowSelection, Task<EntitlementStatus> getLevelEntitlementTask, Image ____localPlayerBackgroundImage)
		{
			//IL_001e: Unknown result type (might be due to invalid IL or missing references)
			//IL_003c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0058: Unknown result type (might be due to invalid IL or missing references)
			//IL_0052: Unknown result type (might be due to invalid IL or missing references)
			//IL_0057: Unknown result type (might be due to invalid IL or missing references)
			//IL_005e: Unknown result type (might be due to invalid IL or missing references)
			((Behaviour)____localPlayerBackgroundImage).enabled = true;
			string levelId = ((ILevelGameplaySetupData)((IReadOnlyDictionary<string, ILobbyPlayerData>)_playersDataModel)[_playersDataModel.partyOwnerId]).beatmapKey.levelId;
			if (string.IsNullOrEmpty(levelId))
			{
				SetLevelEntitlement(____localPlayerBackgroundImage, (EntitlementsStatus)0);
				return;
			}
			EntitlementsStatus val = (EntitlementsStatus)0;
			if (!connectedPlayer.isMe)
			{
				val = _entitlementChecker.GetKnownEntitlement(connectedPlayer.userId, levelId);
			}
			if ((int)val != 0)
			{
				SetLevelEntitlement(____localPlayerBackgroundImage, val);
			}
			else if (!connectedPlayer.isMe)
			{
				_entitlementChecker.GetUserEntitlementStatus(connectedPlayer.userId, levelId);
			}
		}

		private void SetLevelEntitlement(Image backgroundImage, EntitlementsStatus status)
		{
			//IL_0000: Unknown result type (might be due to invalid IL or missing references)
			//IL_0002: Invalid comparison between Unknown and I4
			//IL_0010: Unknown result type (might be due to invalid IL or missing references)
			//IL_0015: Unknown result type (might be due to invalid IL or missing references)
			//IL_0004: Unknown result type (might be due to invalid IL or missing references)
			//IL_0006: Invalid comparison between Unknown and I4
			//IL_001e: Unknown result type (might be due to invalid IL or missing references)
			//IL_001f: Unknown result type (might be due to invalid IL or missing references)
			//IL_002d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0018: Unknown result type (might be due to invalid IL or missing references)
			//IL_001d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			//IL_000d: Unknown result type (might be due to invalid IL or missing references)
			Color val = (((int)status == 1) ? red : (((int)status != 3) ? normal : green));
			Color color = val;
			color.a = alphaIsNotMe;
			((Graphic)backgroundImage).color = color;
		}

		private void HandleSetIsEntitledToLevel(string userId, string levelId, EntitlementsStatus status)
		{
			_playerListView.SetDataToTable();
		}
	}
}
namespace MultiplayerExtensions.Environment
{
	public class MpexConnectedObjectManager : MonoBehaviour
	{
		private MultiplayerConnectedPlayerSpectatingSpot _playerSpectatingSpot;

		private IConnectedPlayerBeatmapObjectEventManager _beatmapObjectEventManager;

		private BeatmapObjectManager _beatmapObjectManager;

		private Config _config;

		[Inject]
		internal void Construct(MultiplayerConnectedPlayerSpectatingSpot playerSpectatingSpot, IConnectedPlayerBeatmapObjectEventManager beatmapObjectEventManager, BeatmapObjectManager beatmapObjectManager, Config config)
		{
			_playerSpectatingSpot = playerSpectatingSpot;
			_beatmapObjectEventManager = beatmapObjectEventManager;
			_beatmapObjectManager = beatmapObjectManager;
			_config = config;
		}

		private void Start()
		{
			_playerSpectatingSpot.isObservedChangedEvent += HandleIsObservedChangedEvent;
			if (_config.DisableMultiplayerObjects)
			{
				_beatmapObjectEventManager.Pause();
			}
		}

		private void OnDestroy()
		{
			if ((Object)(object)_playerSpectatingSpot != (Object)null)
			{
				_playerSpectatingSpot.isObservedChangedEvent -= HandleIsObservedChangedEvent;
			}
		}

		private void HandleIsObservedChangedEvent(bool isObserved)
		{
			if (_config.DisableMultiplayerPlatforms)
			{
				((Component)((Component)this).transform.Find("Construction")).gameObject.SetActive(isObserved);
			}
			if (_config.DisableMultiplayerLights)
			{
				((Component)((Component)this).transform.Find("Lasers")).gameObject.SetActive(isObserved);
			}
			if (_config.DisableMultiplayerObjects)
			{
				if (isObserved)
				{
					_beatmapObjectEventManager.Resume();
					return;
				}
				_beatmapObjectEventManager.Pause();
				_beatmapObjectManager.DissolveAllObjects();
			}
		}
	}
	public class MpexLevelEndActions : IAffinity, ILevelEndActions
	{
		public event Action levelFailedEvent;

		public event Action levelFinishedEvent;

		[AffinityPrefix]
		[AffinityPatch(typeof(MultiplayerLocalActivePlayerFacade), "ReportPlayerDidFinish", AffinityMethodType.Normal, null, new Type[] { })]
		private void PlayerDidFinish()
		{
			this.levelFinishedEvent?.Invoke();
		}

		[AffinityPrefix]
		[AffinityPatch(typeof(MultiplayerLocalActivePlayerFacade), "ReportPlayerNetworkDidFailed", AffinityMethodType.Normal, null, new Type[] { })]
		private void PlayerDidFail()
		{
			this.levelFailedEvent?.Invoke();
		}
	}
	internal class MpexPlayerFacadeLighting : MonoBehaviour
	{
		private readonly Accessor<MultiplayerGameplayAnimator, LightsAnimator[]> _allLightsAnimators = FieldAccessor<MultiplayerGameplayAnimator, LightsAnimator[]>.GetAccessor("_allLightsAnimators");

		private readonly Accessor<MultiplayerGameplayAnimator, LightsAnimator[]> _gameplayLightsAnimators = FieldAccessor<MultiplayerGameplayAnimator, LightsAnimator[]>.GetAccessor("_gameplayLightsAnimators");

		private readonly Accessor<MultiplayerGameplayAnimator, ColorSO> _activeLightsColor = FieldAccessor<MultiplayerGameplayAnimator, ColorSO>.GetAccessor("_activeLightsColor");

		private readonly Accessor<MultiplayerGameplayAnimator, ColorSO> _leadingLightsColor = FieldAccessor<MultiplayerGameplayAnimator, ColorSO>.GetAccessor("_leadingLightsColor");

		private readonly Accessor<MultiplayerGameplayAnimator, ColorSO> _failedLightsColor = FieldAccessor<MultiplayerGameplayAnimator, ColorSO>.GetAccessor("_failedLightsColor");

		private bool _isLeading;

		private int _highestCombo;

		private IConnectedPlayer _connectedPlayer;

		private MultiplayerController _multiplayerController;

		private IScoreSyncStateManager _scoreProvider;

		private MultiplayerLeadPlayerProvider _leadPlayerProvider;

		private MultiplayerGameplayAnimator _gameplayAnimator;

		private MultiplayerSyncState<StandardScoreSyncState, Score, int> _syncState;

		private Config _config;

		private LightsAnimator[] _allLights => _allLightsAnimators.Invoke(ref _gameplayAnimator);

		private LightsAnimator[] _gameplayLights => _gameplayLightsAnimators.Invoke(ref _gameplayAnimator);

		private ColorSO _activeColor => _activeLightsColor.Invoke(ref _gameplayAnimator);

		private ColorSO _leadingColor => _leadingLightsColor.Invoke(ref _gameplayAnimator);

		private ColorSO _failedColor => _failedLightsColor.Invoke(ref _gameplayAnimator);

		[Inject]
		internal void Construct(IConnectedPlayer connectedPlayer, MultiplayerController multiplayerController, IScoreSyncStateManager scoreProvider, MultiplayerLeadPlayerProvider leadPlayerProvider, Config config)
		{
			_connectedPlayer = connectedPlayer;
			_multiplayerController = multiplayerController;
			_scoreProvider = scoreProvider;
			_leadPlayerProvider = leadPlayerProvider;
			_config = config;
		}

		public void OnEnable()
		{
			_gameplayAnimator = ((Component)this).GetComponentInChildren<MultiplayerGameplayAnimator>();
			_syncState = ((IScoreSyncStateManager<StandardScoreSyncState, Score, int, StandardScoreSyncStateNetSerializable, StandardScoreSyncStateDeltaNetSerializable>)(object)_scoreProvider).GetSyncStateForPlayer(_connectedPlayer);
			_leadPlayerProvider.newLeaderWasSelectedEvent += HandleNewLeaderWasSelected;
		}

		public void OnDisable()
		{
			_leadPlayerProvider.newLeaderWasSelectedEvent -= HandleNewLeaderWasSelected;
		}

		private void HandleNewLeaderWasSelected(string userId)
		{
			_isLeading = userId == _connectedPlayer.userId;
		}

		private void Update()
		{
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			//IL_000c: Invalid comparison between Unknown and I4
			//IL_0064: Unknown result type (might be due to invalid IL or missing references)
			//IL_0069: Unknown result type (might be due to invalid IL or missing references)
			//IL_008b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0090: Unknown result type (might be due to invalid IL or missing references)
			//IL_0093: Unknown result type (might be due to invalid IL or missing references)
			//IL_009f: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a0: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a2: Unknown result type (might be due to invalid IL or missing references)
			if ((int)_multiplayerController.state == 4 && !ConnectedPlayerHelpers.IsFailed(_connectedPlayer))
			{
				int state = _syncState.GetState((Score)3, _syncState.player.offsetSyncTime);
				if (state > _highestCombo)
				{
					_highestCombo = state;
				}
				Color val = ColorSO.op_Implicit(_isLeading ? _leadingColor : _activeColor);
				float num = (Mathf.Min((float)_highestCombo, 20f) - (float)state) / 20f;
				Color missColor = _config.MissColor;
				missColor.a = val.a;
				SetLights(Color.Lerp(val, missColor, num));
			}
		}

		public void SetLights(Color color)
		{
			//IL_001c: Unknown result type (might be due to invalid IL or missing references)
			LightsAnimator[] array = _gameplayLightsAnimators.Invoke(ref _gameplayAnimator);
			foreach (LightsAnimator val in array)
			{
				val.SetColor(color);
			}
		}
	}
}
namespace MultiplayerExtensions.Environments
{
	public class MpexAvatarPlaceLighting : MonoBehaviour
	{
		public const float SmoothTime = 2f;

		private List<TubeBloomPrePassLight> _lights = new List<TubeBloomPrePassLight>();

		private IMultiplayerSessionManager _sessionManager;

		private MpexPlayerManager _mpexPlayerManager;

		private Config _config;

		public Color TargetColor { get; private set; } = Color.black;

		public int SortIndex { get; internal set; }

		[Inject]
		internal void Construct(IMultiplayerSessionManager sessionManager, MpexPlayerManager mpexPlayerManager, Config config)
		{
			_sessionManager = sessionManager;
			_mpexPlayerManager = mpexPlayerManager;
			_config = config;
		}

		private void Start()
		{
			//IL_004e: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c1: Unknown result type (might be due to invalid IL or missing references)
			//IL_009f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0098: Unknown result type (might be due to invalid IL or missing references)
			_lights = ((Component)this).GetComponentsInChildren<TubeBloomPrePassLight>().ToList();
			if (_sessionManager == null || _mpexPlayerManager == null || _sessionManager.localPlayer == null)
			{
				return;
			}
			if (_sessionManager.localPlayer.sortIndex == SortIndex)
			{
				SetColor(_config.PlayerColor, immediate: true);
				return;
			}
			foreach (IConnectedPlayer connectedPlayer in _sessionManager.connectedPlayers)
			{
				if (connectedPlayer.sortIndex == SortIndex)
				{
					SetColor(_mpexPlayerManager.GetPlayer(connectedPlayer.userId)?.Color ?? Config.DefaultPlayerColor, immediate: true);
					return;
				}
			}
			SetColor(Color.black);
		}

		private void OnEnable()
		{
			_mpexPlayerManager.PlayerConnectedEvent += HandlePlayerData;
			_sessionManager.playerConnectedEvent += HandlePlayerConnected;
			_sessionManager.playerDisconnectedEvent += HandlePlayerDisconnected;
		}

		private void OnDisable()
		{
			_mpexPlayerManager.PlayerConnectedEvent -= HandlePlayerData;
			_sessionManager.playerConnectedEvent -= HandlePlayerConnected;
			_sessionManager.playerDisconnectedEvent -= HandlePlayerDisconnected;
		}

		private void HandlePlayerData(IConnectedPlayer player, MpexPlayerData data)
		{
			//IL_0010: Unknown result type (might be due to invalid IL or missing references)
			if (player.sortIndex == SortIndex)
			{
				SetColor(data.Color, immediate: false);
			}
		}

		private void HandlePlayerConnected(IConnectedPlayer player)
		{
			//IL_0033: Unknown result type (might be due to invalid IL or missing references)
			//IL_0026: Unknown result type (might be due to invalid IL or missing references)
			if (player.sortIndex == SortIndex)
			{
				if (_mpexPlayerManager.TryGetPlayer(player.userId, out MpexPlayerData player2))
				{
					SetColor(player2.Color, immediate: false);
				}
				else
				{
					SetColor(Config.DefaultPlayerColor, immediate: false);
				}
			}
		}

		private void HandlePlayerDisconnected(IConnectedPlayer player)
		{
			//IL_000f: Unknown result type (might be due to invalid IL or missing references)
			if (player.sortIndex == SortIndex)
			{
				SetColor(Color.black, immediate: false);
			}
		}

		private void Update()
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			//IL_0007: Unknown result type (might be due to invalid IL or missing references)
			//IL_0009: Unknown result type (might be due to invalid IL or missing references)
			//IL_0017: Unknown result type (might be due to invalid IL or missing references)
			//IL_0019: Unknown result type (might be due to invalid IL or missing references)
			//IL_0033: Unknown result type (might be due to invalid IL or missing references)
			//IL_0035: Unknown result type (might be due to invalid IL or missing references)
			//IL_0045: Unknown result type (might be due to invalid IL or missing references)
			//IL_0027: Unknown result type (might be due to invalid IL or missing references)
			Color color = GetColor();
			if (!(color == TargetColor))
			{
				if (IsColorVeryCloseToColor(color, TargetColor))
				{
					SetColor(TargetColor);
				}
				else
				{
					SetColor(Color.Lerp(color, TargetColor, Time.deltaTime * 2f));
				}
			}
		}

		private bool IsColorVeryCloseToColor(Color color0, Color color1)
		{
			//IL_0000: Unknown result type (might be due to invalid IL or missing references)
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			//IL_0019: Unknown result type (might be due to invalid IL or missing references)
			//IL_001f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0032: Unknown result type (might be due to invalid IL or missing references)
			//IL_0038: Unknown result type (might be due to invalid IL or missing references)
			//IL_004b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0051: Unknown result type (might be due to invalid IL or missing references)
			if (Mathf.Abs(color0.r - color1.r) < 0.002f && Mathf.Abs(color0.g - color1.g) < 0.002f && Mathf.Abs(color0.b - color1.b) < 0.002f)
			{
				return Mathf.Abs(color0.a - color1.a) < 0.002f;
			}
			return false;
		}

		public void SetColor(Color color, bool immediate)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_000b: Unknown result type (might be due to invalid IL or missing references)
			TargetColor = color;
			if (immediate)
			{
				SetColor(color);
			}
		}

		public Color GetColor()
		{
			//IL_0020: Unknown result type (might be due to invalid IL or missing references)
			//IL_001a: Unknown result type (might be due to invalid IL or missing references)
			if (_lights.Count > 0)
			{
				return _lights[0].color;
			}
			return Color.black;
		}

		private void SetColor(Color color)
		{
			//IL_0017: Unknown result type (might be due to invalid IL or missing references)
			foreach (TubeBloomPrePassLight light in _lights)
			{
				light.color = color;
				((BloomPrePassLight)light).Refresh();
			}
		}
	}
}
namespace MultiplayerExtensions.Environments.Lobby
{
	public class MpexAvatarNameTag : MonoBehaviour
	{
		private enum PlayerIconSlot
		{
			Platform
		}

		private readonly Dictionary<PlayerIconSlot, ImageView> _playerIcons = new Dictionary<PlayerIconSlot, ImageView>();

		private IConnectedPlayer _player;

		private MpPlayerManager _playerManager;

		private MpexPlayerManager _mpexPlayerManager;

		private SpriteManager _spriteManager;

		private ImageView _bg;

		private CurvedTextMeshPro _nameText;

		[Inject]
		internal void Construct(IConnectedPlayer player, MpPlayerManager playerManager, MpexPlayerManager mpexPlayerManager, SpriteManager spriteManager)
		{
			_player = player;
			_playerManager = playerManager;
			_mpexPlayerManager = mpexPlayerManager;
			_spriteManager = spriteManager;
		}

		private void Awake()
		{
			//IL_00e8: Unknown result type (might be due to invalid IL or missing references)
			//IL_0113: Unknown result type (might be due to invalid IL or missing references)
			_bg = ((Component)((Component)this).transform.Find("BG")).GetComponent<ImageView>();
			_nameText = ((Component)((Component)this).transform.Find("Name")).GetComponent<CurvedTextMeshPro>();
			HorizontalLayoutGroup val = default(HorizontalLayoutGroup);
			if (!((Component)_bg).TryGetComponent<HorizontalLayoutGroup>(ref val))
			{
				HorizontalLayoutGroup val2 = ((Component)_bg).gameObject.AddComponent<HorizontalLayoutGroup>();
				((LayoutGroup)val2).childAlignment = (TextAnchor)4;
				((HorizontalOrVerticalLayoutGroup)val2).childForceExpandWidth = false;
				((HorizontalOrVerticalLayoutGroup)val2).childForceExpandHeight = false;
				((HorizontalOrVerticalLayoutGroup)val2).childScaleWidth = false;
				((HorizontalOrVerticalLayoutGroup)val2).childScaleHeight = false;
				((HorizontalOrVerticalLayoutGroup)val2).spacing = 4f;
			}
			((TMP_Text)_nameText).transform.SetParent(((Component)_bg).transform, false);
			ConnectedPlayerName val3 = default(ConnectedPlayerName);
			if (((Component)_nameText).TryGetComponent<ConnectedPlayerName>(ref val3))
			{
				Object.Destroy((Object)(object)val3);
			}
			((TMP_Text)_nameText).text = "Player";
			((TMP_Text)_nameText).text = _player.userName;
			((Graphic)_nameText).color = Color.white;
			if (_mpexPlayerManager.TryGetPlayer(_player.userId, out MpexPlayerData player))
			{
				((Graphic)_nameText).color = player.Color;
			}
			if (_playerManager.TryGetPlayer(_player.userId, out MpPlayerData player2))
			{
				SetPlatformData(player2);
			}
		}

		private void OnEnable()
		{
			_playerManager.PlayerConnectedEvent += HandlePlatformData;
			_mpexPlayerManager.PlayerConnectedEvent += HandleMpexData;
		}

		private void OnDisable()
		{
			_playerManager.PlayerConnectedEvent -= HandlePlatformData;
			_mpexPlayerManager.PlayerConnectedEvent -= HandleMpexData;
		}

		private void HandlePlatformData(IConnectedPlayer player, MpPlayerData data)
		{
			if (player == _player)
			{
				SetPlatformData(data);
			}
		}

		private void HandleMpexData(IConnectedPlayer player, MpexPlayerData data)
		{
			//IL_0010: Unknown result type (might be due to invalid IL or missing references)
			if (player == _player)
			{
				((Graphic)_nameText).color = data.Color;
			}
		}

		private void SetPlatformData(MpPlayerData data)
		{
			Sprite val = null;
			SetIcon(PlayerIconSlot.Platform, (Sprite)(data.Platform switch
			{
				Platform.Steam => _spriteManager.IconSteam64, 
				Platform.OculusQuest => _spriteManager.IconMeta64, 
				Platform.OculusPC => _spriteManager.IconOculus64, 
				_ => _spriteManager.IconToaster64, 
			}));
		}

		private void SetIcon(PlayerIconSlot slot, Sprite sprite)
		{
			//IL_0023: Unknown result type (might be due to invalid IL or missing references)
			//IL_0029: Expected O, but got Unknown
			//IL_00a6: Unknown result type (might be due to invalid IL or missing references)
			if (!_playerIcons.TryGetValue(slot, out ImageView value))
			{
				GameObject val = new GameObject($"MpexPlayerIcon({slot})");
				val.transform.SetParent(((Component)_bg).transform, false);
				val.transform.SetSiblingIndex((int)slot);
				val.layer = 5;
				val.AddComponent<CanvasRenderer>();
				value = val.AddComponent<ImageView>();
				((MaskableGraphic)value).maskable = true;
				((Image)value).fillCenter = true;
				((Image)value).preserveAspect = true;
				((Graphic)value).material = ((Graphic)_bg).material;
				_playerIcons[slot] = value;
				RectTransform component = val.GetComponent<RectTransform>();
				((Transform)component).localScale = new Vector3(3.2f, 3.2f);
			}
			((Image)value).sprite = sprite;
			((TMP_Text)_nameText).transform.SetSiblingIndex(999);
		}
	}
}
namespace System.Runtime.CompilerServices
{
	[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
	internal sealed class IgnoresAccessChecksToAttribute : Attribute
	{
		public IgnoresAccessChecksToAttribute(string assemblyName)
		{
		}
	}
}
