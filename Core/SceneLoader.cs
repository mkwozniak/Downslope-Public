using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Image = UnityEngine.UI.Image;

namespace Wozware.Downslope
{
	public class SceneLoader : MonoBehaviour
	{
		#region Events

		public event LoadingActionFinishing OnFadeOutFinish;
		public event LoadingActionFinishing OnFadeInFinish;
		public event LoadingActionFinishing OnSceneLoadingFinished;

		#endregion

		#region Public Members

		// core
		/// <summary> The first scene that will be loaded. </summary>
		public string StartupSceneName = "Game";

		/// <summary> The list of objects that will persist between all scene loading. </summary>
		public List<GameObject> PersistentObjects;

		/// <summary> The in canvas loading view object. </summary>
		public GameObject LoadingView;

		/// <summary> The canvas image mask for the loading bar. </summary>
		public Image LoadingBarMask;

		/// <summary> The minimum time to stall loading before proceeding. </summary>
		public float MinimumLoadShowTime = 1.0f;

		/// <summary> The in canvas title view object. </summary>
		public GameObject TitleView;

		/// <summary> The minimum time to stall the title screen before proceeding. </summary>
		public float MinimumTitleShowTime = 1.0f;

		/// <summary> If the title chime sfx will be played. </summary>
		public bool PlayTitleChime = true;

		/// <summary> The source of the chime sfx. </summary>
		public AudioSource TitleChimeSource;

		/// <summary> The canvas image to overlay a black fade screen. </summary>
		public Image FadeOverlay;

		/// <summary> The speed of the fade screen. </summary>
		public float FadeSpeed;

		#endregion

		#region Private Members

		/// <summary> The state of the loader. </summary>
		[ReadOnly][SerializeField] private SceneLoaderState _state;

		/// <summary> The state of the fade screen. </summary>
		[ReadOnly][SerializeField] private FadeOverlayState _fadeState;

		/// <summary> The primary Game instance that must be in the first loaded scene. </summary>
		[ReadOnly][SerializeField] private Game _game;

		/// <summary> The current async loading operation. </summary>
		private AsyncOperation _currLoadingOperation;

		/// <summary> The name of current loading scene </summary>
		private string _currLoadingScene;

		/// <summary> The current fade in/out callback. </summary>
		private System.Action _currFadeAction;

		/// <summary> The current fade overlay color. </summary>
		private Color _currFadeColor;

		/// <summary> If the minimum time for the current loading has passed. </summary>
		private bool _loadMinTimeWaited = false;

		/// <summary> If the current loading operation is completely ready and finished. </summary>
		private bool _currLoadingOperationFinished = false;

		/// <summary> If the minimum time for the current title has passed. </summary>
		private bool _titleMinTimeWaited = false;

		/// <summary> The amount of time elapsed for the current minimum loading time. </summary>
		private float _minLoadTimeElapsed = 0.0f;

		/// <summary> The amount of time elapsed for the current minimum title time. </summary>
		private float _minTitleTimeElapsed = 0.0f;

		/// <summary> Method callbacks per state. </summary>
		private Dictionary<SceneLoaderState, System.Action> _stateCallbacks = new Dictionary<SceneLoaderState, System.Action>();

		#endregion

		#region Public Methods

		/// <summary> Fade and start loading a scene by string name. </summary>
		public void StartSceneLoad(string sceneName, LoadingActionFinishing onFinishCallback = null)
		{
			if (onFinishCallback != null)
			{
				Debug.Log($"{this}: StartSceneLoad, OnFinish callback: {onFinishCallback.Method}");
				OnSceneLoadingFinished += onFinishCallback;
			}

			Debug.Log($"{this} Starting Scene Loading FadeOut.");

			// cache loading scene
			_currLoadingScene = sceneName;

			SetLoadingViewActive(true);

			// start fading
			StartFadeOut(EnterLoadingStateFromCurrent);		
		}

		/// <summary> Resets loading data and goes to the idle state. </summary>
		public void FinishCurrentSceneLoad(LoadingActionFinishing onFinishCallback = null)
		{
			if (onFinishCallback != null)
			{
				Debug.Log($"{this}: FinishCurrentSceneLoad from callback: {onFinishCallback.Method}");
				onFinishCallback -= FinishCurrentSceneLoad;
			}

			SetProgress(0f);
			OnSceneLoadingFinished.Invoke();
			StartFadeOut();

			_currLoadingOperation = null;
			_minLoadTimeElapsed = 0.0f;
			_state = SceneLoaderState.Idle;
		}

		/// <summary> Starts a fade in action. </summary>
		public void StartFadeIn(LoadingActionFinishing onFinishCallback = null)
		{
			if (onFinishCallback != null)
			{
				Debug.Log($"{this}: StartFadeIn. OnFinish callback: {onFinishCallback.Method}");
				OnFadeInFinish += onFinishCallback;
			}

			FadeOverlay.raycastTarget = true;
			_currFadeAction = UpdateFadeIn;
		}

		/// <summary> Starts a fade out action. </summary>
		public void StartFadeOut(LoadingActionFinishing onFinishCallback = null)
		{
			if (onFinishCallback != null)
			{
				Debug.Log($"{this}: StartFadeOut. OnFinish callback: {onFinishCallback.Method}");
				OnFadeOutFinish += onFinishCallback;
			}
			_currFadeAction = UpdateFadeOut;
		}

		#endregion

		#region Private Methods

		/// <summary> Assign state events and lambdas. </summary>
		private void InitializeEvents()
		{
			_stateCallbacks[SceneLoaderState.Loading] = UpdateLoadingState;
			_stateCallbacks[SceneLoaderState.Title] = UpdateTitleState;
			_stateCallbacks[SceneLoaderState.Idle] = () => { };

			_currFadeAction = () => { };
			OnFadeInFinish = (cb) => { };
			OnFadeOutFinish = (cb) => { };
		}

		/// <summary> Calls DontDestroyOnLoad on current list of PersistentObjects. </summary>
		private void KeepPersistentObjects()
		{
			for (int i = 0; i < PersistentObjects.Count; i++)
			{
				DontDestroyOnLoad(PersistentObjects[i]);
			}
		}
		/// <summary> Loads the startup scene from string. </summary>
		private void StartTitleScreenToStartupLoad()
		{
			SetTitleViewActive(true);
			StartFadeOut(EnterTitleState);
		}

		/// <summary> Resets title data and enters the Title state.  </summary>
		private void EnterTitleState(LoadingActionFinishing callbackOrigin = null)
		{
			if (callbackOrigin != null)
			{
				OnFadeOutFinish -= EnterTitleState;
			}

			Debug.Log($"{this} Enter Title State.");
			if (PlayTitleChime)
			{
				TitleChimeSource.Play();
			}
			// reset title data and set state
			_titleMinTimeWaited = false;
			_minTitleTimeElapsed = 0f;
			_state = SceneLoaderState.Title;
		}

		/// <summary> Starts loading operation based on current loading scene string. </summary>
		private void EnterLoadingStateFromCurrent(LoadingActionFinishing callbackOrigin = null)
		{
			if (callbackOrigin != null)
			{
				OnFadeOutFinish -= EnterLoadingStateFromCurrent;
			}

			_currLoadingOperation = SceneManager.LoadSceneAsync(_currLoadingScene);

			// do not allow scene activation yet
			_currLoadingOperation.allowSceneActivation = false;

			Debug.Log($"{this} Enter Loading State.");
			// reset load data and set state
			SetProgress(0f);
			_loadMinTimeWaited = false;
			_minLoadTimeElapsed = 0f;
			_state = SceneLoaderState.Loading;
		}

		/// <summary> Finishes the title state. </summary>
		private void FinishTitleState(LoadingActionFinishing callbackOrigin = null)
		{
			if (callbackOrigin != null)
			{
				OnFadeInFinish -= FinishTitleState;
			}

			Debug.Log($"{this} Finish Title State.");
			SetTitleViewActive(false);
			StartLoadStartupScene();
		}

		/// <summary> Finishes the loading state. </summary>
		private void FinishLoadingState(LoadingActionFinishing callbackOrigin = null)
		{		
			if (callbackOrigin != null)
			{
				OnFadeInFinish -= FinishLoadingState;
			}

			Debug.Log($"{this} Finish Loading State.");
			SetLoadingViewActive(false);
			FinishCurrentSceneLoad();
		}

		/// <summary> Loads the startup scene from string. </summary>
		private void StartLoadStartupScene(LoadingActionFinishing callbackOrigin = null)
		{
			if (callbackOrigin != null)
			{
				callbackOrigin -= StartLoadStartupScene;
			}

			Debug.Log($"{this} Start Load Startup Scene");
			StartSceneLoad(StartupSceneName, FindStartupObjects);
		}

		private void FindStartupObjects(LoadingActionFinishing callbackOrigin = null)
		{
			if (callbackOrigin != null)
			{
				OnSceneLoadingFinished -= FindStartupObjects;
			}

			bool startupSceneHasGame = GameObject.FindGameObjectWithTag("Game").TryGetComponent(out _game);
			if(!startupSceneHasGame)
			{
				Debug.LogError($"{this} Critical Error: FindStartupObjects: Startup scene does not have any object with " +
					$"'Game' tag or the object does not have the 'Game' component.");
				return;
			}

			_game.AttachToSceneLoader(this);
			Debug.Log($"{this} Success attaching to Game instance.");
		}

		/// <summary> Updates the loading UI such as bar and percentage. </summary>
		private void SetProgress(float progress)
		{
			// Update the fill's scale based on how far the game has loaded:
			LoadingBarMask.fillAmount = progress;
		}

		/// <summary> Enables/Disables the in canvas loading view. </summary>
		private void SetTitleViewActive(bool state)
		{
			TitleView.SetActive(state);
		}

		/// <summary> Enables/Disables the in canvas loading view. </summary>
		private void SetLoadingViewActive(bool state)
		{
			LoadingView.SetActive(state);
		}

		/// <summary> Update callback for overlay FadeIn action. </summary>
		private void UpdateFadeIn()
		{
			_currFadeColor = FadeOverlay.color;
			_currFadeColor.a += FadeSpeed * Time.deltaTime;
			FadeOverlay.color = _currFadeColor;
			_fadeState = FadeOverlayState.FadingIn;
			if (FadeOverlay.color.a >= 1f)
			{
				FinishFadeAction();
				OnFadeInFinish.Invoke(OnFadeInFinish);
				_fadeState = FadeOverlayState.FadedIn;
			}
		}

		/// <summary> Update callback for overlay FadeOut action. </summary>
		private void UpdateFadeOut()
		{
			_currFadeColor = FadeOverlay.color;
			_currFadeColor.a -= FadeSpeed * Time.deltaTime;
			FadeOverlay.color = _currFadeColor;
			_fadeState = FadeOverlayState.FadingOut;
			if (FadeOverlay.color.a <= 0f)
			{
				FinishFadeAction();
				OnFadeOutFinish.Invoke(OnFadeOutFinish);
				FadeOverlay.raycastTarget = false;
				_fadeState = FadeOverlayState.FadedOut;
			}
		}

		/// <summary> Default OnFinish callback for any overlay action. </summary>
		private void FinishFadeAction()
		{
			Debug.Log($"{this}: Fade Action Finished.");
			_currFadeAction = () => { };
		}

		/// <summary> Update callback for Title state. </summary>
		private void UpdateTitleState()
		{
			_minTitleTimeElapsed += Time.deltaTime;
			if(_minTitleTimeElapsed >= MinimumTitleShowTime && !_titleMinTimeWaited)
			{
				StartFadeIn(FinishTitleState);
				_titleMinTimeWaited = true;
			}
		}

		/// <summary> Update callback for Loading state. </summary>
		private void UpdateLoadingState()
		{
			if (_currLoadingOperation == null)
				return;

			SetProgress(_currLoadingOperation.progress);

			// continue loading
			_minLoadTimeElapsed += Time.deltaTime;
			if (_minLoadTimeElapsed >= MinimumLoadShowTime)
			{
				// min time waited and finished operation, loading can officially finish
				_loadMinTimeWaited = true;
				_currLoadingOperation.allowSceneActivation = true;
			}

			// if done, hide and return
			if (_currLoadingOperation.isDone && _loadMinTimeWaited && !_currLoadingOperationFinished)
			{
				StartFadeIn(FinishLoadingState);
				_currLoadingOperationFinished = true;
			}
		}

		#endregion

		#region Unity Methods

		private void Awake()
		{
			InitializeEvents();
			KeepPersistentObjects();
			StartTitleScreenToStartupLoad();
		}

		private void Start()
		{

		}

		private void Update()
		{
			_stateCallbacks[_state].Invoke();
			_currFadeAction.Invoke();
		}

		#endregion
	}
}


