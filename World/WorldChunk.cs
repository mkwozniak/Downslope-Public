using System.Collections.Generic;
using UnityEngine;

namespace Wozware.Downslope
{
	[RequireComponent(typeof(WorldObject))]
	public sealed class WorldChunk : MonoBehaviour
	{
		public WorldChunkAction OnDestroy;
		public WorldChunkAction OnReachHeightThreshold;

		public int Thickness;
		public float XOffset;
		public List<Transform> CenterSprites = new List<Transform>();
		public List<string> PossibleChunks = new List<string>();
		public List<string> PossibleExpandChunks = new List<string>();
		public List<string> PossibleContractChunks = new List<string>();
		public List<string> PossibleTurningChunks = new List<string>();
		public SpriteRenderer LeftEdge;
		public SpriteRenderer RightEdge;
		public int UID;

		public WorldObject WorldObj
		{
			get
			{
				return _worldObject;
			}
		}

		private WorldObject _worldObject;
		private bool _initialized = false;
		private bool _destroyed;
		private bool _heightThresholdActive;

		public void Initialize()
		{
			_initialized = true;
			_destroyed = false;
			_worldObject.OnUpdate = UpdateChunk;
		}

		public void SetMovement(WorldMovementProps props)
		{
			_worldObject.SetMovement(props);
		}

		public void ReachHeightThresholdSubscribe(WorldChunkAction callback)
		{
			_heightThresholdActive = true;
			OnReachHeightThreshold += callback;
		}

		public void ReachHeightThresholdUnsubscribe(WorldChunkAction callback)
		{
			_heightThresholdActive = false;
			OnReachHeightThreshold -= callback;
		}

		public void UpdateChunk()
		{
			if(!_initialized)
			{
				return;
			}

			if (transform.position.y > _worldObject.Movement.MaxY && !_destroyed)
			{
				OnDestroy.Invoke(this);
				_initialized = false;
				_destroyed = true;
			}

			if (_heightThresholdActive && transform.position.y > _worldObject.Movement.HeightThresholdY)
			{
				OnReachHeightThreshold.Invoke(this);
			}
		}

		public void TriggerDestroy()
		{
			_initialized = false;
			OnDestroy.Invoke(this);
			_destroyed = true;
		}

		private void Awake()
		{
			_worldObject = GetComponent<WorldObject>();
		}

		private void Start()
		{

		}

		private void Update()
		{
			UpdateChunk();
		}

		private void FixedUpdate()
		{
			// UpdateChunkMovement();
		}
	}
}


