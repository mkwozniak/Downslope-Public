namespace Wozware.Downslope
{
	public enum SceneLoaderState
	{
		Title,
		Loading,
		Idle,
	}

	public enum FadeOverlayState
	{
		FadingIn,
		FadingOut,
		FadedIn,
		FadedOut,
	}

	public enum GameState
	{
		Init,
		Loading,
		Menu,
		Game,
		Paused,
	}

	public enum GameMode
	{
		Tutorial,
		Challenge,
		Arcade,
	}

	public enum PlayerState
	{
		Hidden,
		Stopped,
		Moving,
		Airborne,
		Stunned,
	}

	public enum CollisionTypes
	{
		None,
		Powder,
		Shrub,
		SmallTree,
		LargeTree,
		SmallRamp,
		Stump,
		LargeRamp,
	}

}