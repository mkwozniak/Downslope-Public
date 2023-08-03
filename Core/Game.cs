using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem.LowLevel;
using Wozware.Downslope;
using Action = System.Action;

namespace Wozware.Downslope
{
	public sealed partial class Game : MonoBehaviour
	{
		#region Events

		#endregion

		#region Public Members
		public static bool IS_PAUSED
		{
			get { return _PAUSED; }
		}

		public static readonly int PLAYER_DEFAULT_Y = 4;
		public static readonly float PLAYER_DEFAULT_MENU_SPEED = 0.08f;
		public static readonly int WORLD_GEN_DEFAULT_DIST = 24;
		public static readonly int TUTORIAL_OBSTACLE_STAGE_DIST = 100;
		public static readonly int TUTORIAL_WORLD_GEN_DIST = 8;

		private static bool _PAUSED = false;

		#endregion

		#region Private Members

		[SerializeField] private SceneLoader _loader;
		[SerializeField] private AssetPack _assets;
		[SerializeField] private DownslopeUI _ui;
		[SerializeField] private WorldGenerator _world;
		[SerializeField] private PlayerControl _player;
		[SerializeField] private AudioSource _musicSource;
		[SerializeField] private Camera _camera;

		[ReadOnly][SerializeField] private GameMode _mode;
		[ReadOnly][SerializeField] private GameState _state;

		private Dictionary<GameMode, Action> _modeCallbacks = new Dictionary<GameMode, Action>();
		private WeightedMapData _currArcadeMap;

		#endregion

		#region Public Methods

		public void PauseGameTime(bool pause)
		{
			if(pause)
			{
				DownslopeTime.LocalTimeScale = 0;
				_PAUSED = true;
				return;
			}

			DownslopeTime.LocalTimeScale = 1;
			_PAUSED = false;
		}

		public void AttachToSceneLoader(SceneLoader loader)
		{
			Debug.Log($"{this}: AttachToSceneLoader");
			_loader = loader;
		}

		public void SetArcadeMap(string id)
		{
			if(!_assets.ArcadeModeMaps.ContainsKey(id))
			{
				Debug.LogError($"{this.name}: SetArcadeMap {id} does not exist in ArcadeModeMaps.");
				return;
			}

			_currArcadeMap = _assets.ArcadeModeMaps[id];

			_world.SetWorldEdgeSize(_currArcadeMap.WorldEdgeSize, 2);
			_world.SetWorldWeights(_currArcadeMap.ObstacleWeights, _currArcadeMap.OuterObstacleWeights,
				_currArcadeMap.IcePathWeights, _world.ObstacleSeed);
			_world.SetMaxChunkWidth(_currArcadeMap.MaxIcePathWidth);
			Debug.Log(_currArcadeMap.OuterObstacleWeights);
		}

		public void EnterTutorialMode()
		{
			_world.SetWorldEdgeSize(_assets.TutorialMapList[0].WorldEdgeSize, 2);
			_world.SetWorldWeights(_assets.TutorialMapList[0].ObstacleWeights, _assets.ClearMapData.OuterObstacleWeights,
				_assets.TutorialMapList[0].IcePathWeights, _world.ObstacleSeed);
			_world.PathForwardGenerationDistance = TUTORIAL_WORLD_GEN_DIST;

			if (_loader == null)
			{
				StartTutorialMode();
				return;
			}

			_loader.OnFadeInFinish += StartTutorialMode;
			_loader.StartFadeIn();
		}

		public void EnterArcadeMode()
		{
			_world.PathForwardGenerationDistance = WORLD_GEN_DEFAULT_DIST;

			if (_loader == null)
			{
				StartArcadeMode();
				return;
			}
			
			_loader.OnFadeInFinish += StartArcadeMode;
			_loader.StartFadeIn();
		}

		public void EnterEscapeMode()
		{
			PauseGameTime(true);
			_ui.EnterEscapeViewFront();
		}

		public void ExitEscapeMode()
		{

		}

		public void PlaySound(string id)
		{
			if (!TryCreateSFX(id))
			{
				Debug.LogWarning($"Game: PlaySound: TryCreateSFX returned false. SFX {id} was ignored.");
			}
		}

		public void SetMusic(int id)
		{
			if (id >= _assets.Music.Count || id < 0)
				return;
			_musicSource.clip = _assets.Music[id];
			_musicSource.Play();
		}

		#endregion

		#region Private Methods

		private void InitializeEvents()
		{
			_modeCallbacks[GameMode.Tutorial] = UpdateTutorial;
			_modeCallbacks[GameMode.Arcade] = () => { };
			_modeCallbacks[GameMode.Challenge] = () => { };

			OnFinishDistanceTutorialStage = () => { };

			_world.PlaySFX = TryCreateSFX;

			_world.OnUpdateKMH += _ui.SetKMHLabel;
			_world.OnUpdateDistanceTravelled += _ui.SetDistanceLabel;

			_player.PlaySFX = TryCreateSFX;
			_player.CreatePFX = _world.CreatePFXSprite;
			_player.GetKMH = _world.KMH;

			_player.OnStartMoving += _world.GenerateForward;
			_player.OnSpeedUpdated += _world.SetWorldSpeed;
			_player.OnHitIce += _world.CreatePlayerTrail;
		}

		private void StartTutorialMode(LoadingActionFinishing callbackOrigin = null)
		{
			if (callbackOrigin != null)
			{
				callbackOrigin -= StartTutorialMode;
			}

			_world.ClearWorld();
			_player.TutorialStart();
			_world.GenerateForward();
			_ui.ExitMainMenu();
			_ui.EnterGameViewFront();
			SetMusic(1);
			StartTutorialInitialStage();
			_mode = GameMode.Tutorial;

			if (_loader != null)
			{
				_loader.StartFadeOut();
			}
		}

		private void StartArcadeMode(LoadingActionFinishing callbackOrigin = null)
		{
			if (callbackOrigin != null)
			{
				callbackOrigin -= StartArcadeMode;
			}

			_world.ClearWorld();
			_player.ArcadeModeStart();
			_world.GenerateForward();
			_ui.ExitMainMenu();
			_ui.EnterGameViewFront();
			_mode = GameMode.Arcade;
			SetMusic(2);

			DownslopeInput.SubscribeInputPerformed(DownslopeInput.ENTER, ArcadeModeStarted);
			DownslopeInput.SubscribeInputPerformed(DownslopeInput.ENTER, _player.StartInitialMovement);
			DownslopeInput.SubscribeInputPerformed(DownslopeInput.ESCAPE, EnterEscapeMode);

			if (_loader != null)
			{
				_loader.StartFadeOut();
			}
		}

		private void ArcadeModeStarted()
		{
			Debug.Log($"{this.name} Arcade Mode Started");
			DownslopeInput.UnsubscribeInputPerformed(DownslopeInput.ENTER, ArcadeModeStarted);
			DownslopeInput.UnsubscribeInputPerformed(DownslopeInput.ENTER, _player.StartInitialMovement);
		}

		private bool TryCreateSFX(string id)
		{
			AudioClip clip;
			if (!_assets.TryGetSFX(id, out clip))
			{
				Debug.LogWarning($"Game: TryCreateSFX: Assets.TryGetSFX returned false. SFX {id} was ignored.");
				return false;
			}

			AudioSource source = Instantiate(_assets.SFXPrefab, Vector3.zero, Quaternion.identity);
			source.clip = clip;
			source.Play();

			Destroy(source.gameObject, clip.length + 0.1f);
			return true;
		}

		#endregion

		#region Unity Methods

		private void Awake()
		{
			_assets.Initialize();
		}

		private void Start()
		{
			InitializeEvents();
			DownslopeInput.InitializeControls();

			_world.SetFirstIcePathName("PathEmpty");
			_world.SetWorldEdgeSize(10, 0);
			_world.SetWorldWeights(_assets.DefaultObstacleWeights, _assets.DefaultOuterObstacleWeights,
				_assets.DefaultIcePathWeights, _world.ObstacleSeed);
			_world.SetSnowVariationWeights(_assets.DefaultSnowVariationWeights);

			_player.GameInitialize();
			_player.EnableTrails(false);
			_ui.ExitGameView();
			_ui.ExitTutorialView();
			_ui.EnterMainMenuFront();
		}

		private void Update()
		{
			_modeCallbacks[_mode].Invoke();
		}

		#endregion

	}
}


