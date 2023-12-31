using System.Collections.Generic;
using System;
using UnityEngine;
using TMPro;
using Random = UnityEngine.Random;
using Wozware.Downslope;
using static UnityEngine.ParticleSystem;
using static UnityEditor.Experimental.GraphView.GraphView;

namespace Wozware.Downslope
{
	public sealed class PlayerControl : MonoBehaviour
	{
		#region Events

		public Action OnSpeedUp;
		public Action OnStartMoving;
		public ActionFinishing OnStopMoving;
		public SpeedUpdating OnSpeedUpdated;
		public IceColliding OnHitIce;
		public PowderColliding OnHitPowder;
		public JumpLanding OnLandAirJump;

		public SFXPlaying PlaySFX;
		public FXCreating CreatePFX;
		public AudioClipReturning GetAmbientClip;
		public GameStateReturning GetGameState;
		public SpeedReturning GetKMH;

		#endregion

		#region Public Members

		[Header("Core")]
		public Animator Anim;
		public BoxCollider2D Collider;
		public SpriteRenderer Rend;
		public SpriteRenderer ShadowRenderer;

		[Header("Forward Movement")]
		public float DefaultForwardSpeed = 0.18f;
		public float MinimumForwardAcceleration = 0.1f;
		public float AccelerationSpeed = 0.0075f;
		public float PowderForwardPenalty = 0.50f;
		public float PowderFastForwardPenalty = 0.80f;
		public float PowderPenaltyKMHThreshold = 20f;

		[Header("Braking")]
		public float BrakingSpeed = 0.005f;
		public float ManualBreakBonus = 1.5f;

		[Header("Carving & Horizontal")]
		public float PlayerHorizontalSpeed = 0.1f;
		public float FastCarveHorizontalBonus = 1.1f;
		public float FastCarveForwardPenalty = 0.85f;
		public float CarveAirDragPenalty = 0.2f;
		public float CarveAirDragIntensity = 1.0f;
		public float CarveAirRecovery = 1.0f;
		public float CarveForwardHorizontalModifier = 1.0f;
		public float CarveMinimumSpeed = 0.1f;

		[Header("Air Jumping")]
		public float AirForwardBonus = 1.25f;
		public float SpeedInAirMultiplier = 1f;
		public float JumpSpriteRiseSpeed = 0.5f;
		public float JumpSpriteRiseMax = 1.5f;
		public float JumpSpriteLowerSpeed = 0.5f;
		public float AirShadowLargeThreshold = 0.33f;
		public float AirShadowSmallThreshold = 0.66f;
		public float DefaultWindVolume = 0.2f;
		public float JumpWindVolume = 0.5f;

		[Header("Trail")]
		public float StraightTrailTime = 1f;
		public float CarveTrailTime = 1f;

		[Header("Misc")]
		public LayerMask IceLayerMask;
		public float StunTime = 0.25f;
		public float SoftJumpSoundKMHThreshold = 10;

		public Vector3 StraightTrailOffset = new Vector3(0, 0.08f, 0);
		public Vector3 CarveTrailOffset = new Vector3(0, 0.08f, 0);

		public Sprite[] ShadowSprites;
		public AudioClip ClipLoopSkiPath;
		public AudioClip ClipLoopSkiPathMed;
		public AudioClip ClipLoopSkiPathSlow;
		public AudioClip ClipLoopCarvingPath;
		public AudioClip ClipLoopCarvingPowder;
		public AudioClip ClipLoopJumpWind;
		public ParticleSystem SkiPowderFX;
		public AudioSource SkiAmbientSource;
		public AudioSource SkiCarvingSource;
		public AudioSource SkiWindSource;

		#endregion

		#region Private Members

		[Header("Inputs")]
		[ReadOnly][SerializeField] private bool _inputStartMoving = false;
		[ReadOnly][SerializeField] private bool _inputBrake = false;
		[ReadOnly][SerializeField] private bool[] _inputHorizontal = new bool[2];

		[Header("States")]
		[ReadOnly][SerializeField] private PlayerState _state;
		[ReadOnly][SerializeField] private bool _hidden = false;
		[ReadOnly][SerializeField] private bool _onIce = false;
		[ReadOnly][SerializeField] private bool _onPowder = false;
		[ReadOnly][SerializeField] private bool _carving = false;
		[ReadOnly][SerializeField] private bool _moving = false;
		[ReadOnly][SerializeField] private bool _accelerating = false;
		[ReadOnly][SerializeField] private bool _decelerating = false;
		[ReadOnly][SerializeField] private bool _fastCarvingEffects = false;
		[ReadOnly][SerializeField] private bool _updateForwardSpeed = false;
		[ReadOnly][SerializeField] private bool _braking = false;
		[ReadOnly][SerializeField] private bool _airJumping = false;
		[ReadOnly][SerializeField] private bool _stunned = false;
		[ReadOnly][SerializeField] private bool _softJump = false;
		[ReadOnly][SerializeField] private bool _trails = true;
		[ReadOnly][SerializeField] private bool _firstAfterCarve = false;

		[Header("Current Values")]
		[ReadOnly][SerializeField] private Vector3 _currHorizontalVelocity = Vector3.zero;
		[ReadOnly][SerializeField] private int _currHorizontalDir = 0;
		[ReadOnly][SerializeField] private float _currForwardSpeed = 0f;
		[ReadOnly][SerializeField] private float _currAcceleration = 0f;
		[ReadOnly][SerializeField] private float _currForwardMovePenalty = 1f;
		[ReadOnly][SerializeField] private float _currForwardCarvePenalty = 1f;
		[ReadOnly][SerializeField] private float _currForwardTerrainPenalty = 1f;
		[ReadOnly][SerializeField] private float _currForwardBrakePenalty = 1f;
		[ReadOnly][SerializeField] private float _currHorizontalMoveBonus = 1f;
		[ReadOnly][SerializeField] private float _currHorizontalForwardPenalty = 1f;
		[ReadOnly][SerializeField] private float _currMaxForwardSpeed = 0f;
		[ReadOnly][SerializeField] private float _currBrakingPower = 0f;
		[ReadOnly][SerializeField] private float _currTrailTime = 0f;
		[ReadOnly][SerializeField] private float _currCarveAirDrag = 0f;
		[ReadOnly][SerializeField] private float _currMaxAirHeight = 0f;
		[ReadOnly][SerializeField] private float _currAirHeightSpeed = 0f;
		[ReadOnly][SerializeField] private float _currAirTime = 0f;
		[ReadOnly][SerializeField] private float _currAirPercentage = 0f;

		[Header("Timers")]
		[ReadOnly][SerializeField] private float _trailTimer = 0f;
		[ReadOnly][SerializeField] private float _airTimer = 0f;
		[ReadOnly][SerializeField] private float _stunnedTimer = 0f;

		private Dictionary<PlayerState, Action> _stateCallbacks = new Dictionary<PlayerState, Action>();

		private RaycastHit2D _iceRayHit;

		private int[] _iceTrailIds = new int[] { 0, 1, 2 };
		private int[] _powderTrailIds = new int[] { 3, 4, 2 };
		private int[] _currentTrailIds = new int[] { 0, 0, 0 };

		#endregion

		#region Public Methods

		public void GameInitialize()
		{
			Hide();
			SetMenuMoveMode();
		}

		public void ArcadeModeStart()
		{
			StopMovement();
			Show();
		}

		public void TutorialStart()
		{
			StopMovement();
			Show();
		}

		public void StartInitialMovement()
		{
			if (_moving || _hidden)
				return;

			_moving = true;
			_onIce = false;
			_onPowder = true;
			_updateForwardSpeed = true;

			_currTrailTime = StraightTrailTime;
			_currAcceleration = MinimumForwardAcceleration;
			_currMaxForwardSpeed = DefaultForwardSpeed;

			_state = PlayerState.Moving;

			EnableTrails(true);
			SkiAmbientSource.clip = ClipLoopSkiPath;
			SkiAmbientSource.volume = _currAcceleration + .5f;
			SkiAmbientSource.Play();

			OnStartMoving.Invoke();
		}

		public void EnableTrails(bool val)
		{
			_trails = val;
		}

		#endregion

		#region Private Methods

		private void InitializeEvents()
		{
			// initialize events with lambda so they are not null
			OnSpeedUpdated = (f) => { };
			OnStartMoving = () => { };
			OnStopMoving = (cb) => { };
			OnHitIce = (p, id) => { };
			OnHitPowder = (p) => { };
			OnLandAirJump = (p) => { };
			PlaySFX = (id) => { return false; };
			CreatePFX = (id, pos) => { };
			GetKMH = () => { return 0; };

			_stateCallbacks[PlayerState.Hidden] = () => { };
			_stateCallbacks[PlayerState.Stopped] = () => { };
			_stateCallbacks[PlayerState.Moving] = () => { };
			_stateCallbacks[PlayerState.Stunned] = () => { };
			_stateCallbacks[PlayerState.Airborne] = () => { };

			_stateCallbacks[PlayerState.Hidden] += UpdateAcceleration;

			// _stateCallbacks[PlayerState.Stopped] += CheckPlayerStart;

			_stateCallbacks[PlayerState.Moving] += UpdateAcceleration;
			_stateCallbacks[PlayerState.Moving] += UpdateAccelerationSounds;
			_stateCallbacks[PlayerState.Moving] += UpdateCarving;
			_stateCallbacks[PlayerState.Moving] += UpdateFastCarving;
			_stateCallbacks[PlayerState.Moving] += UpdateTrail;
			_stateCallbacks[PlayerState.Moving] += CheckBraking;
			_stateCallbacks[PlayerState.Moving] += CheckHitTerrainType;

			_stateCallbacks[PlayerState.Stunned] += UpdateStunned;

			_stateCallbacks[PlayerState.Airborne] += UpdateInAir;
			_stateCallbacks[PlayerState.Airborne] += UpdateAcceleration;
		}

		private void EnableCollision(bool enable)
		{
			Collider.enabled = enable;
		}

		private void SetMenuMoveMode()
		{
			_moving = true;
			_currMaxForwardSpeed = Game.PLAYER_DEFAULT_MENU_SPEED;
			_currAcceleration = 1f;
			_currBrakingPower = 1f;
			_state = PlayerState.Hidden;
			_updateForwardSpeed = true;
			OnStartMoving.Invoke();
		}

		private void Hide()
		{
			StopMovement();
			EnableCollision(false);
			Anim.enabled = false;
			Rend.enabled = false;
			_trails = false;
			_hidden = true;
			_state = PlayerState.Hidden;
			Debug.Log("Hiding Player");
		}

		private void Show()
		{
			EnableCollision(true);
			Anim.enabled = true;
			Rend.enabled = true;
			_trails = true;
			_hidden = false;
			_state = PlayerState.Stopped;
			Debug.Log("Showing Player.");
		}

		private void StopMovement()
		{
			_moving = false;
			_currMaxForwardSpeed = 0f;
			_currAcceleration = 0f;
			_updateForwardSpeed = true;
			_state = PlayerState.Stopped;
			OnStopMoving.Invoke();
			Debug.Log("Stopping Player Movement.");
		}

		private void StopBraking()
		{
			_currBrakingPower = BrakingSpeed;
			_currForwardBrakePenalty = 1f;
			StopCarvingSounds();
			Anim.SetBool("Braking", false);
			_braking = false;
		}

		private void RecoverFromStunned()
		{
			_stunned = false;
			_stunnedTimer = 0;
			_currMaxForwardSpeed = DefaultForwardSpeed;
		}

		private void ModifyForwardSpeed(float percentageModifier)
		{
			_currAcceleration = _currAcceleration * percentageModifier;
			_updateForwardSpeed = true;
		}

		private void CheckForwardSpeedUpdate()
		{
			if (!_updateForwardSpeed)
				return;

			_currForwardSpeed = _currAcceleration * _currMaxForwardSpeed;
			// Debug.Log($"{this.name}: Forward Speed Last Updated: {_currAcceleration * _currMaxForwardSpeed}");
			// Debug.Log($"{this.name}: Acl: {_currAcceleration}");
			// Debug.Log($"{this.name}: Max: {_currMaxForwardSpeed}");
			UpdateSpeed(_currForwardSpeed);
			_updateForwardSpeed = false;
		}

		private void CheckHitTerrainType()
		{
			if (_airJumping)
				return;

			_iceRayHit = Physics2D.Raycast(transform.position, Vector3.down, 1, IceLayerMask);
			// if no ice hit
			if (!_iceRayHit.transform)
			{
				// disable on ice and hit powder
				if (_onIce)
				{
					_onIce = false;
					_onPowder = true;
					CreatePFX.Invoke("PowderFX", transform.position);
					HitPowder();
				}

				// check powder penalty and return
				if (GetKMH.Invoke() <= PowderPenaltyKMHThreshold)
				{
					_currForwardTerrainPenalty = PowderForwardPenalty;
					return;
				}

				_currForwardTerrainPenalty = PowderFastForwardPenalty;
				return;
			}

			// enable on ice
			if (!_onIce)
			{
				_onIce = true;
				_onPowder = false;
				StopPowderSounds();
				StartIceSounds();
			}

			// disable penalty
			_currForwardTerrainPenalty = 1;
		}

		private bool CheckObstacleDirectHit(Vector3 player, Vector3 obstacle, float hitDist)
		{
			player.y = 0;
			obstacle.y = 0;
			float dist = Vector2.Distance(player, obstacle);
			Debug.Log($"Player Obstacle Direct Hit Distance: {dist}. Distance Needed: {hitDist}.");

			if (Vector2.Distance(player, obstacle) < hitDist)
			{
				return true;
			}

			return false;
		}

		private void CheckBraking()
		{
			if (_airJumping)
			{
				_currForwardBrakePenalty = 1f;
				Anim.SetBool("Braking", false);
				_braking = false;
				return;
			}

			if (_braking && _currAcceleration <= MinimumForwardAcceleration)
			{
				_currAcceleration = MinimumForwardAcceleration;
			}

			if (_inputBrake && !_braking)
			{
				Anim.SetBool("Braking", true);
				_currBrakingPower = BrakingSpeed * ManualBreakBonus;
				_currForwardBrakePenalty = 0f;
				StartCarvingSounds();
				_braking = true;
				return;
			}

			if (!_inputBrake && _braking)
			{
				StopBraking();
			}
		}

		private void EnableFastCarvingEffects()
		{
			_currForwardCarvePenalty = FastCarveForwardPenalty;
			_currHorizontalMoveBonus = FastCarveHorizontalBonus;
			_fastCarvingEffects = true;
		}

		private void DisableFastCarvingEffects()
		{
			_currForwardCarvePenalty = 1;
			_currHorizontalMoveBonus = 1;
			_fastCarvingEffects = false;
		}

		private void StartCarvingSounds(bool pauseAmbientAfter = true)
		{
			SkiCarvingSource.clip = ClipLoopCarvingPath;

			if (!_onIce)
			{
				SkiCarvingSource.clip = ClipLoopCarvingPowder;
			}

			SkiCarvingSource.Play();
			if (pauseAmbientAfter)
				SkiAmbientSource.Pause();
		}

		private void StopCarvingSounds(bool playAmbientAfter = true)
		{
			SkiCarvingSource.Pause();
			if (playAmbientAfter)
				SkiAmbientSource.Play();
		}

		private void HitPowder()
		{
			SkiPowderFX.Play();
			PlaySFX.Invoke("PlayerHitPowder0");
		}

		private void StopPowderSounds()
		{
			SkiPowderFX.Stop();
		}

		private void StartIceSounds()
		{
			PlaySFX.Invoke("PlayerHitIceTerrain0");
		}

		private void StartAirJump(float rampPower, float verticalPower, float verticalMax)
		{
			Rend.sortingOrder = 3;
			ShadowRenderer.gameObject.SetActive(true);

			string jumpSFXId = "PlayerHitJump0";
			if (GetKMH.Invoke() < SoftJumpSoundKMHThreshold || _softJump)
			{
				jumpSFXId = "PlayerHitJumpSoft0";
			}

			PlaySFX.Invoke(jumpSFXId);

			SkiAmbientSource.Pause();
			SkiWindSource.volume = JumpWindVolume;
			StopCarvingSounds(false);

			_currAirTime = _currForwardSpeed * SpeedInAirMultiplier * rampPower;
			_currAirHeightSpeed = _currAirTime * verticalPower;
			_currMaxAirHeight = verticalMax;
			_currForwardTerrainPenalty = AirForwardBonus;

			_airTimer = 0f;
			_airJumping = true;
			_state = PlayerState.Airborne;
		}

		private void StopAirJump()
		{
			Rend.sortingOrder = 2;

			string landSFXId = "PlayerLandJump0";
			if (GetKMH.Invoke() < SoftJumpSoundKMHThreshold || _softJump)
			{
				landSFXId = "PlayerLandJumpSoft0";
			}

			PlaySFX.Invoke(landSFXId);
			SkiWindSource.volume = DefaultWindVolume;

			Anim.transform.position = new Vector3(transform.position.x, Game.PLAYER_DEFAULT_Y, 0);
			ShadowRenderer.gameObject.SetActive(false);
			CreatePFX("PowderFX", transform.position);
			_airJumping = false;
			_state = PlayerState.Moving;
		}

		private void StunFromHit()
		{
			if (!_stunned)
			{
				_currHorizontalVelocity = Vector3.zero;
				_accelerating = false;
				_currMaxForwardSpeed = 0f;
				_currAcceleration = 0f;
				_decelerating = false;

				StopBraking();

				_carving = false;
				_fastCarvingEffects = false;
				_stunned = true;
				_state = PlayerState.Stunned;
			}
		}

		private void CollideWorldSprite(WorldSprite sprite)
		{
			Obstacle obstacle;

			if (!sprite.TryGetObstacleData(out obstacle))
			{
				return;
			}

			// check if obstacle collidable while air jumping
			if (_airJumping && !obstacle.AirCollidable)
			{
				return;
			}

			// check if obstacle is a ramp and jump if it is
			if (obstacle.IsRamp && !_airJumping)
			{
				_softJump = obstacle.IsSoftJump;
				StartAirJump(obstacle.ForwardRampPower, obstacle.VerticalRampPower, obstacle.VerticalRampMax);
				return;
			}

			// check if hit obstacle mid air
			if (obstacle.AirCollidable && _airJumping)
			{
				StopAirJump();
			}

			// check if the obstacle can and is a direct hit
			if (obstacle.IsCenterCollidable && CheckObstacleDirectHit(transform.position, sprite.transform.position, obstacle.CenterCollisionDistance))
			{
				// modify the speed, play sound and stun the player if need be
				ModifyForwardSpeed(obstacle.CenterCollisionSpeedPenalty);

				// react sprite to collision
				sprite.CollidePlayerCenter();

				if (obstacle.StunOnCenterCollision)
				{
					StunFromHit();
				}

				return;
			}

			// check for regular collision penalty and play sound
			if (obstacle.HasCollisionSpeedPenalty)
			{
				ModifyForwardSpeed(obstacle.CollisionSpeedPenalty);
			}

			// react sprite to collision
			sprite.CollidePlayer();
		}

		private void UpdateMovement(float timeScale)
		{
			transform.position += _currHorizontalVelocity * timeScale;

			if (_accelerating && !_carving)
			{
				_currAcceleration += AccelerationSpeed;
				_updateForwardSpeed = true;
			}

			if (_decelerating && _currAcceleration > MinimumForwardAcceleration)
			{
				_currAcceleration -= _currBrakingPower;
				_updateForwardSpeed = true;
			}
		}

		private void UpdateSpeed(float speed)
		{
			OnSpeedUpdated.Invoke(speed);
		}

		private void UpdateInput()
		{
			_inputHorizontal[0] = Input.GetKey(KeyCode.A);
			_inputHorizontal[1] = Input.GetKey(KeyCode.D);
			_inputBrake = Input.GetKey(KeyCode.Space);
			_inputStartMoving = Input.GetKeyDown(KeyCode.Space);
		}

		private void UpdateAcceleration()
		{
			if (!_moving)
				return;

			Anim.SetBool("Accelerating", _carving ? false : _accelerating);
			SkiAmbientSource.volume = _currAcceleration + .5f;
			SkiCarvingSource.volume = _currAcceleration + .5f;

			if(_currAcceleration <= MinimumForwardAcceleration)
			{
				//_currAcceleration = MinimumForwardAcceleration;
				//_accelerating = true;
			}

			// if accelerating and reached max, stop
			if (_accelerating)
			{
				if (_currAcceleration >= _currForwardMovePenalty)
				{
					_currAcceleration = _currForwardMovePenalty;
					_accelerating = false;
					return;
				}
			}

			// if decelerating and reached lowest, stop
			if (_decelerating)
			{
				if (_currAcceleration <= _currForwardMovePenalty)
				{
					_currAcceleration = _currForwardMovePenalty;
					_decelerating = false;
					return;
				}
			}

			// total forward penalty is (terrainPenalty * carvePenalty * brakePenalty)
			_currForwardMovePenalty = _currForwardTerrainPenalty * _currForwardCarvePenalty * _currForwardBrakePenalty;

			// player should be accelerating if accel is less than penalty and not stunned
			if (_currAcceleration < _currForwardMovePenalty && !_stunned)
			{
				_accelerating = true;
				_decelerating = false;
			}

			// player should be decelerating if accel is greater than penalty
			if (_currAcceleration > _currForwardMovePenalty)
			{
				_decelerating = true;
				_accelerating = false;
			}
		}

		private void UpdateCarving()
		{
			if (_stunned)
				return;

			// apply horizontal carving velocity
			_currHorizontalForwardPenalty = (_currForwardSpeed * CarveForwardHorizontalModifier) + CarveMinimumSpeed;
			if (_currHorizontalForwardPenalty > 1)
			{
				_currHorizontalForwardPenalty = 1;
			}
			_currHorizontalVelocity = (Vector3.right * PlayerHorizontalSpeed * _currHorizontalMoveBonus * _currHorizontalForwardPenalty) * _currHorizontalDir;

			if (_airJumping)
			{
				if (_currHorizontalMoveBonus > CarveAirDragPenalty)
				{
					_currHorizontalMoveBonus -= DownslopeTime.DeltaTime * CarveAirDragIntensity;
				}
				else
				{
					_currHorizontalMoveBonus = CarveAirDragPenalty;
				}
				return;
			}

			if (!_airJumping && !_fastCarvingEffects && _currHorizontalMoveBonus < 1)
			{
				_currHorizontalMoveBonus += DownslopeTime.DeltaTime * CarveAirRecovery;
				if (_currHorizontalMoveBonus >= 1)
				{
					_currHorizontalMoveBonus = 1;
				}
			}

			// no horizontal input
			if (!_inputHorizontal[0] && !_inputHorizontal[1])
			{
				_currHorizontalDir = 0;
				Anim.SetFloat("Horizontal", 0);

				// stop carving
				if (_carving)
				{
					StopCarvingSounds();
					_firstAfterCarve = true;
					_currTrailTime = StraightTrailTime;
					_carving = false;
				}

				// stop fast carving if was
				if (_fastCarvingEffects)
				{
					DisableFastCarvingEffects();
				}

				return;
			}

			// horizontal input, start carving
			if (!_carving)
			{
				_trailTimer = 0f;
				_currTrailTime = CarveTrailTime;
				_carving = true;
			}

			if (!SkiCarvingSource.isPlaying)
			{
				StartCarvingSounds();
			}

			// set carve left velocity then return
			if (_inputHorizontal[0])
			{
				_currHorizontalDir = -1;
				Anim.SetFloat("Horizontal", -1);
				return;
			}

			// set carve right velocity
			_currHorizontalDir = 1;
			Anim.SetFloat("Horizontal", 1);
		}

		private void UpdateFastCarving()
		{
			// dont fast carve if not carving or air jumping
			if (!_carving || _airJumping)
				return;

			// if brake input is false and applied effects, reset effects and return
			if (!_braking)
			{
				if (_fastCarvingEffects)
				{
					DisableFastCarvingEffects();
				}

				return;
			}

			// braking input is active, if didnt apply effects then do so
			if (!_fastCarvingEffects)
			{
				EnableFastCarvingEffects();
			}
		}

		private void UpdateAccelerationSounds()
		{
			if (!_moving || _carving || _airJumping || SkiAmbientSource.clip == null)
				return;

			if (_currForwardSpeed >= 0.18f && SkiAmbientSource.clip.name != ClipLoopSkiPath.name)
			{
				SkiAmbientSource.clip = ClipLoopSkiPath;
			}

			if (_currForwardSpeed < 0.18f && _currForwardSpeed >= 0.09f
				&& SkiAmbientSource.clip.name != ClipLoopSkiPathMed.name)
			{
				SkiAmbientSource.clip = ClipLoopSkiPathMed;
			}

			if (_currForwardSpeed < 0.09f
				&& SkiAmbientSource.clip.name != ClipLoopSkiPathSlow.name)
			{
				SkiAmbientSource.clip = ClipLoopSkiPathSlow;
			}

			if (!SkiAmbientSource.isPlaying)
			{
				SkiAmbientSource.Play();
			}
		}

		private void UpdateTrail()
		{
			if (_airJumping || !_moving || !_trails)
				return;

			if (_onPowder)
			{
				_currentTrailIds = _powderTrailIds;
			}
			else
			{
				_currentTrailIds = _iceTrailIds;
			}

			_trailTimer += DownslopeTime.DeltaTime * _currForwardSpeed;
			if (_trailTimer < _currTrailTime)
				return;

			Vector3 pos;
			if (!_carving)
			{
				pos = transform.position + StraightTrailOffset;
				_trailTimer = 0f;

				if (_firstAfterCarve)
				{
					//pos.y -= 1;
					OnHitIce.Invoke(pos, _currentTrailIds[2]);
					_firstAfterCarve = false;
					return;
				}

				OnHitIce.Invoke(pos, _currentTrailIds[0]);
				return;
			}

			pos = transform.position + CarveTrailOffset;
			OnHitIce.Invoke(pos, _currentTrailIds[1]);
			_trailTimer = 0f;
		}

		private void UpdateInAir()
		{
			if (!_airJumping)
				return;

			_airTimer += DownslopeTime.DeltaTime;
			_currAirPercentage = _airTimer / _currAirTime;

			if (_currAirPercentage <= AirShadowLargeThreshold
				&& ShadowRenderer.sprite != ShadowSprites[0])
			{
				ShadowRenderer.sprite = ShadowSprites[0];
			}
			if (_currAirPercentage > AirShadowLargeThreshold
				&& _currAirPercentage <= AirShadowSmallThreshold
				&& ShadowRenderer.sprite != ShadowSprites[1])
			{
				ShadowRenderer.sprite = ShadowSprites[1];
			}

			if (_currAirPercentage > AirShadowSmallThreshold
				&& ShadowRenderer.sprite != ShadowSprites[2])
			{
				ShadowRenderer.sprite = ShadowSprites[2];
			}

			if (_currAirPercentage < .75f
				&& Anim.transform.localPosition.y < _currMaxAirHeight)
			{
				Anim.transform.position += new Vector3(0, JumpSpriteRiseSpeed * DownslopeTime.DeltaTime * _currAirHeightSpeed, 0);
			}
			else if (_currAirPercentage >= .75f
				&& Anim.transform.localPosition.y > 0f)
			{
				Anim.transform.position -= new Vector3(0, JumpSpriteLowerSpeed * DownslopeTime.DeltaTime, 0);
			}

			if (_airTimer >= _currAirTime || _stunned)
			{
				Anim.transform.localPosition = new Vector3(0, 0, 0);
				StopAirJump();
			}
		}

		private void UpdateStunned()
		{
			if (!_stunned)
				return;
			_stunnedTimer += DownslopeTime.DeltaTime;
			_currAcceleration = 0;
			_currMaxForwardSpeed = 0;
			if (_stunnedTimer > StunTime)
			{
				RecoverFromStunned();
				_state = PlayerState.Moving;
			}
		}

		#endregion

		#region Unity Methods

		private void Awake()
		{
			InitializeEvents();
			transform.position = new Vector3(0, Game.PLAYER_DEFAULT_Y, 0);
		}

		private void Start()
		{
			SkiWindSource.volume = DefaultWindVolume;
		}

		private void Update()
		{
			if (Game.IS_PAUSED)
				return;

			UpdateInput();
			_stateCallbacks[_state].Invoke();
			CheckForwardSpeedUpdate();
		}

		private void FixedUpdate()
		{
			UpdateMovement(DownslopeTime.TimeScale);
		}

		private void OnTriggerEnter2D(Collider2D collision)
		{
			WorldSprite hit;
			bool isWorldSprite = collision.TryGetComponent(out hit);

			// check if its even a world sprite
			if (!isWorldSprite)
			{
				return;
			}

			// obstacle is a valid collision
			CollideWorldSprite(hit);
		}

		#endregion
	}
}


