using System.Collections.Generic;
using UnityEngine;
using System;
using Unity.VisualScripting;
using UnityEngine.U2D;
using Wozware.Poolers;
using UnityEngine.Experimental.Rendering;
using System.Drawing;
using UnityEngine.Rendering;
using static UnityEditor.PlayerSettings;
using System.Runtime.CompilerServices;

namespace Wozware.Downslope
{
	public sealed class WorldGenerator : MonoBehaviour
	{
		#region Events

		public WorldFrameUpdating OnUpdate;
		public WorldMovementUpdating OnSetWorldSpeed;
		public DistanceUpdating OnUpdateDistanceTravelled;
		public SpeedUpdating OnUpdateKMH;
		public WorldClearing OnClearWorld;
		public SFXPlaying PlaySFX;

		#endregion

		#region Public Members

		public AssetPack Assets;
		public WozPooler<WorldSprite> PrimarySpritePooler;
		public Transform WorldCenter;
		public Transform ActiveChunkParent;
		public Transform ActiveObjectParent;
		public Transform ActiveFXParent;
		public SortingLayer ObstacleSortingLayer;

		public int ObstacleSeed = 10403;
		public int PathForwardGenerationDistance;
		public int WorldEdgeSize = 14;
		public int DefaultObstacleLayer = 3;
		public float WorldSpeed;
		public float WorldDestroyY;
		public float WorldGenYHeight;
		public float MetricSpeedScale = 3f;

		#endregion

		#region Private Members

		private bool _firstPath = true;
		private int _lastIcePathID;
		private int _currMaxChunkWidth = 0;
		private int _rightWorldEdge = 0;
		private int _currWorldXOffset = 0;
		private string _currWidthChunkName = "Path1Flat";

		[ReadOnly][SerializeField] private float _distanceTravelled = 0f;
		[ReadOnly][SerializeField] private float _metersPerSecond = 0f;
		[ReadOnly][SerializeField] private float _kmh = 0f;
		[ReadOnly][SerializeField] private int _spritePoolInactiveCount = 0;

		private System.Random _obstacleRnd;
		private System.Random _outerObstacleRnd;
		private System.Random _icePathRnd;
		private System.Random _snowVariationRnd;
		private DDRandom _distributionObstacle;
		private DDRandom _distributionOuterObstacle;
		private DDRandom _distributionIcePath;
		private DDRandom _distributionSnowVariation;

		private readonly Vector3 _worldDirection = Vector3.up;
		private readonly int _chunkDirection = -1;
		private List<int> _spriteIds = new List<int>();
		private List<int> _chunkIds = new List<int>();
		private HashSet<float> _currentChunkObstacles;
		private List<string> _currChunkObstaclePossibilities = new List<string>();
		private List<string> _currOuterObstaclePossibilities = new List<string>();
		private List<string> _currSnowVariationPossibilities = new List<string>();
		private List<string> _currObstaclePossibilities = new List<string>();
		private List<string> _currIcePathPossibilities = new List<string>();

		private Dictionary<int, WorldObject> _activeObjects;
		private Dictionary<int, WorldSprite> _activeSprites;
		private Dictionary<int, WorldChunk> _activeIcePaths;

		private float _lastDistance = 0f;

		#endregion

		#region Public Methods

		public float KMH()
		{
			return _kmh;
		}

		public int DistanceTravelled()
		{
			return (int)_distanceTravelled;
		}

		public void SetFirstIcePathName(string id)
		{
			_currWidthChunkName = id;
		}

		public void SetWorldEdgeSize(int size, int rightOffset)
		{
			WorldEdgeSize = size;
			_rightWorldEdge = size - rightOffset;
		}

		public void SetWorldWeights(List<WeightIdentity> obstacleWeights, List<WeightIdentity> outerObstacleWeights, List<WeightIdentity> icePathWeights, int seed = 0)
		{
			_obstacleRnd = new System.Random();
			_outerObstacleRnd = new System.Random();
			_icePathRnd = new System.Random();
			_currChunkObstaclePossibilities.Clear();
			_currOuterObstaclePossibilities.Clear();
			_currIcePathPossibilities.Clear();

			if (seed != 0)
			{
				_obstacleRnd = new System.Random(ObstacleSeed);
				_outerObstacleRnd = new System.Random(ObstacleSeed);
				_icePathRnd = new System.Random(ObstacleSeed);
			}

			List<int> obsWeights = new List<int>();
			List<int> outerObsWeights = new List<int>();
			List<int> iceWeights = new List<int>();

			int i = 0;
			for (i = 0; i < obstacleWeights.Count; i++)
			{
				obsWeights.Add(obstacleWeights[i].Weight);
				_currChunkObstaclePossibilities.Add(obstacleWeights[i].Name);
			}
			for (i = 0; i < outerObstacleWeights.Count; i++)
			{
				outerObsWeights.Add(outerObstacleWeights[i].Weight);
				_currOuterObstaclePossibilities.Add(outerObstacleWeights[i].Name);
			}
			for (i = 0; i < icePathWeights.Count; i++)
			{
				iceWeights.Add(icePathWeights[i].Weight);
				_currIcePathPossibilities.Add(icePathWeights[i].Name);
			}

			_distributionObstacle = new DDRandom(obsWeights, _obstacleRnd);
			_distributionOuterObstacle = new DDRandom(outerObsWeights, _outerObstacleRnd);
			_distributionIcePath = new DDRandom(iceWeights, _icePathRnd);
		}

		public void SetSnowVariationWeights(List<WeightIdentity> variationWeights, int seed = 0)
		{
			_snowVariationRnd = new System.Random();
			_currSnowVariationPossibilities.Clear();

			if (seed != 0)
			{
				_snowVariationRnd = new System.Random(seed);
			}

			List<int> weightValues = new List<int>();
			for (int i = 0; i < variationWeights.Count; i++)
			{
				weightValues.Add(variationWeights[i].Weight);
				_currSnowVariationPossibilities.Add(variationWeights[i].Name);
			}

			_distributionSnowVariation = new DDRandom(weightValues, _snowVariationRnd);
		}

		public void SetMaxChunkWidth(int width)
		{
			_currMaxChunkWidth = width;
		}

		/// <summary> Generates the next random (chunks, obstacles, etc) 
		/// forward world from last generated position. </summary>
		public void GenerateForward()
		{
			Vector3 defaultVector = Vector3.zero;
			Vector3 pos = defaultVector + WorldCenter.position;
			Vector3 lastChunkPos = pos;
			int chunkId = 1;

			if (!_firstPath)
			{
				// if not first path, last pos is the last active chunk y - 1
				lastChunkPos.y = _activeIcePaths[_lastIcePathID].transform.position.y;

				// get a new random directional path width from last chunks possibilities
				RandomizeIcePath();
			}

			// randomize new height of generation
			int chunkAbove = 0;

			for (int i = 0; i < PathForwardGenerationDistance; i++)
			{
				
				if(!_firstPath)
				{
					lastChunkPos.x = _activeIcePaths[_lastIcePathID].transform.position.x;
				}

				// pos of chunk is current height + the last chunk y
				pos = new Vector3(0, _chunkDirection, 0) + lastChunkPos;

				chunkAbove = _lastIcePathID;

				// create the appropriate width chunk
				CreateChunk(pos, _currWidthChunkName);
				RandomizeIcePath();
				_currentChunkObstacles.Clear();

				// generate row obstacle
				GenerateRowObstacle(_lastIcePathID);
				GenerateRowOuterObstacle(_activeIcePaths[_lastIcePathID].transform.position.y);

				// generate the final level edges
				GenerateRowLevelEdge(_activeIcePaths[_lastIcePathID].transform.position.y);
				if(chunkAbove == _lastIcePathID)
				{
					Debug.Log($"Stuck On Chunk {_lastIcePathID}");
				}

				lastChunkPos = pos;
				if(i == 0)
				{
					// chunk finished, subscribe to its height event
					// chunk will auto generate next chunk when its passed the world height threshold
					_activeIcePaths[_lastIcePathID].ReachHeightThresholdSubscribe(WorldAutoGenerate);
				}

				//Debug.Log($"Creating Obstacles For Chunk: {_lastChunkID}");
				
			}

			_firstPath = false;
		}

		/// <summary> Clears the currently generated world. </summary>
		public void ClearWorld(ActionFinishing callbackOrigin = null)
		{
			if (callbackOrigin != null)
			{
				callbackOrigin -= ClearWorld;
			}

			// invoke the clear world event which chunks/sprites are subscribed to
			// will call TriggerDestroy on all of them
			OnClearWorld.Invoke();

			_lastIcePathID = 0;
			_lastDistance = 0;
			_distanceTravelled = 0;
			_spriteIds.Clear();
			_chunkIds.Clear();
			_activeObjects.Clear();
			_activeSprites.Clear();
			_firstPath = true;
		}

		public void SetWorldSpeed(float speed)
		{
			WorldSpeed = speed;
			OnSetWorldSpeed.Invoke(WorldSpeed * DownslopeTime.TimeScale);
		}

		public void CreatePlayerTrail(Vector3 pos, int id)
		{
			// create trail props
			WorldSpriteProps objProps = new WorldSpriteProps(pos, Assets.PlayerTrailSprites[id], 1, ActiveObjectParent);

			// try create sprite
			WorldSprite sprite;
			bool spriteSuccess = CreateWorldObject(objProps, DestroyObstacleObject, out sprite);
			if(!spriteSuccess)
			{
				Debug.LogError($"World CreatePlayerTrail failed. CreateWorldObject call returned false.");
				return;
			}

			sprite.name = $"TrailFX[{Assets.PlayerTrailSprites[id].name}][{sprite.UID}]";
		}

		public void CreatePlayerPowderFX(Vector3 pos)
		{
			WorldSpriteProps objProps = new WorldSpriteProps(pos, Assets.GetSprite(SpriteID.EMPTY), 0, ActiveFXParent);
			WorldSprite fxSprite;
			bool spriteSuccess = CreateWorldObject(objProps, DestroyObstacleObject, out fxSprite);
			if (!spriteSuccess)
			{
				Debug.LogError($"World CreatePlayerPowderFX failed. CreateWorldObject call returned false.");
				return;
			}
			GameObject g = Instantiate(Assets.CollidePlayerFX[CollisionTypes.Powder], fxSprite.transform.position, Quaternion.identity, fxSprite.transform);
			fxSprite.name = $"FX[{Assets.CollidePlayerFX[CollisionTypes.Powder].name}][{fxSprite.UID}]";
			Destroy(g, 1f);
		}

		public void CreatePFXSprite(string id, Vector3 pos)
		{
			WorldSpriteProps objProps = new WorldSpriteProps(pos, Assets.GetSprite(SpriteID.EMPTY), 0, ActiveFXParent);
			WorldSprite fxSprite;
			bool spriteSuccess = CreateWorldObject(objProps, DestroyObstacleObject, out fxSprite);
			if (!spriteSuccess)
			{
				Debug.LogError($"World CreatePlayerPowderFX failed. CreateWorldObject call returned false.");
				return;
			}

			ParticleSystem pfx;
			if(!Assets.TryCreatePFX(id, pos, out pfx, fxSprite.transform))
			{
				return;
			}

			Destroy(pfx.gameObject, pfx.main.startLifetime.constant + 0.1f);
			fxSprite.name = $"FX[{pfx.name}][{fxSprite.UID}]";
		}

		public void CreateSnowVariation(string id, Vector3 pos)
		{
			// create snow props
			WorldSpriteProps objProps = new WorldSpriteProps(pos, Assets.Sprites[id], 0, ActiveObjectParent);

			// try create sprite
			WorldSprite sprite;
			bool spriteSuccess = CreateWorldObject(objProps, DestroyObstacleObject, out sprite);
			if (!spriteSuccess)
			{
				Debug.LogError($"World CreateSnowVariation failed. CreateWorldObject call returned false.");
				return;
			}

			sprite.name = $"TrailFX[{Assets.Sprites[id].name}][{sprite.UID}]";
		}

		#endregion

		#region Private Methods

		/// <summary> Initializes event callbacks and lambdas. </summary>
		private void InitializeEvents()
		{
			OnUpdate = () => { };
			OnSetWorldSpeed = (speed) => { };
			OnUpdateDistanceTravelled = (f) => { };
			OnUpdateKMH = (f) => { };
			OnClearWorld = () => { };

			// OnSetWorldSpeed += UpdateDistanceTravelled;

			PrimarySpritePooler.OnAddToPool += WorldSpritePoolAdded;
			PrimarySpritePooler.OnReturnToPool += WorldSpritePoolReturned;
			PrimarySpritePooler.OnDestroyExcess += WorldSpritePoolDestroyed;
			OnUpdate += PrimarySpritePooler.CheckTrim;
		}

		private void InitializeStructures()
		{
			_activeObjects = new Dictionary<int, WorldObject>();
			_activeSprites = new Dictionary<int, WorldSprite>();
			_activeIcePaths = new Dictionary<int, WorldChunk>();
			_currentChunkObstacles = new HashSet<float>();

			PrimarySpritePooler.Initialize();
		}

		/// <summary> Initializes a newly created WorldSprite. </summary>
		private void InitializeSpriteObject(WorldSprite worldSprite, Sprite sprite, int order = 0)
		{
			worldSprite.SetSortingOrder(order);
			worldSprite.SetSprite(sprite);
		}

		/// <summary> Initializes a newly created Unity GameObject. </summary>
		private void InitializeGameObject(GameObject obj, Vector3 pos, Transform parent)
		{
			obj.transform.position = pos;
			obj.transform.SetParent(parent);
			obj.SetActive(true);
		}

		/// <summary> Initializes a WorldObject given an initialized GameObject and WorldSprite. </summary>
		private void InitializeWorldObject(int uid, GameObject gameObj, WorldSprite sprite, WorldObjectAction worldDestroyCallback)
		{
			// initialize props
			WorldObjectProps props = new WorldObjectProps(uid, gameObj);
			WorldMovementProps movement = new WorldMovementProps(_worldDirection, WorldDestroyY, WorldGenYHeight);

			// add to structures
			_activeObjects.Add(uid, sprite.WorldObj);
			_activeSprites.Add(uid, sprite);
			_spriteIds.Add(uid);

			sprite.WorldObj.Initialize(props, movement);
			sprite.WorldObj.SetObjectSpeed(WorldSpeed * DownslopeTime.TimeScale);

			// subscribe to events
			// OnUpdate += _activeObjects[uid].UpdateObject;
			OnSetWorldSpeed += _activeObjects[uid].SetObjectSpeed;
			OnClearWorld += _activeObjects[uid].TriggerDestroy;
			_activeObjects[uid].OnDestroyTriggered += worldDestroyCallback;
		}

		/// <summary> Initializes a newly created PathChunk. </summary>
		private void InitializeChunk(WorldChunk chunk, WorldChunkAction destroyCallback)
		{
			// initialize props
			int uid = chunk.GetHashCode() + _activeIcePaths.Count;
			WorldMovementProps movement = new WorldMovementProps(_worldDirection, WorldDestroyY, WorldGenYHeight);
			WorldObjectProps props = new WorldObjectProps(uid, chunk.gameObject);
			chunk.UID = uid;
			chunk.WorldObj.Initialize(props, movement);
			chunk.Initialize();
			chunk.SetMovement(movement);
			chunk.WorldObj.SetObjectSpeed(WorldSpeed * DownslopeTime.TimeScale);

			// add to structures
			_activeIcePaths.Add(uid, chunk);
			_chunkIds.Add(uid);

			// subscribe to events
			// OnUpdate += _activeIcePaths[chunk.UID].UpdateChunk;
			OnSetWorldSpeed += _activeIcePaths[uid].WorldObj.SetObjectSpeed;
			_activeIcePaths[uid].OnDestroy += destroyCallback;
			OnClearWorld += _activeIcePaths[uid].TriggerDestroy;

			// cache this uid as the last uid created
			_lastIcePathID = uid;
		}

		/// <summary> Callback to a chunks reached height event. Generates the next forward chunk. </summary>
		private void WorldAutoGenerate(WorldChunk chunk)
		{
			chunk.ReachHeightThresholdUnsubscribe(WorldAutoGenerate);
			GenerateForward();
		}

		private void WorldSpritePoolAdded(WorldSprite sprite)
		{
			sprite.UID = sprite.GetHashCode() + PrimarySpritePooler.Count;
			sprite.gameObject.SetActive(false);
			sprite.name = $"PooledSprite[{sprite.UID}]";
		}

		private void WorldSpritePoolReturned(WorldSprite sprite)
		{
			sprite.SetParent(PrimarySpritePooler.PoolParent);
			sprite.gameObject.SetActive(false);
			sprite.name = $"PooledSprite[{sprite.UID}]";
			sprite.ResetSprite();
		}

		private void WorldSpritePoolDestroyed(WorldSprite sprite)
		{
			Destroy(sprite);
		}

		/// <summary> Generates a random obstacle at the given position with DDRandom. </summary>
		private void CheckGenerateObstacle(Vector3 pos, DDRandom ranDistribution)
		{
			int ran = ranDistribution.Next();

			// 0 is no obstacle
			if (ran == 0)
				return;

			if (ran >= _currObstaclePossibilities.Count)
				return;

			string currObstacle = _currObstaclePossibilities[ran];

			// if this obstacle is extended, make sure it can fit
			if (Assets.ObstacleIDs[currObstacle].ExtendedObstacle && _activeIcePaths[_lastIcePathID].RightEdge)
			{
				if ((pos.x + 1) >= _activeIcePaths[_lastIcePathID].RightEdge.transform.position.x)
					return;
				if (_currentChunkObstacles.Contains(pos.x + 1))
					return;
			}

			// if no obstacle there already, create one
			if (!_currentChunkObstacles.Contains(pos.x))
			{
				CreateObstacle(currObstacle, pos);
			}
		}

		private void CheckGenerateSnowVariation(Vector3 pos, DDRandom ranDistribution)
		{
			int ran = ranDistribution.Next();

			// 0 is no variation
			if (ran == 0)
				return;

			if (ran >= _currSnowVariationPossibilities.Count)
				return;

			string id = _currSnowVariationPossibilities[ran];
			CreateSnowVariation(id, pos);
		}

		private void GenerateRowObstacle(int chunkID)
		{
			_currObstaclePossibilities = _currChunkObstaclePossibilities;

			// generate the obstacles on the chunks center
			for (int j = 0; j < _activeIcePaths[chunkID].CenterSprites.Count; j++)
			{
				Vector3 colPos = _activeIcePaths[chunkID].CenterSprites[j].position;
				CheckGenerateObstacle(colPos, _distributionObstacle);
			}
		}

		private void GenerateRowOuterObstacle(float yPos)
		{
			_currObstaclePossibilities = _currOuterObstaclePossibilities;

			int thicknessLeft = (int)_activeIcePaths[_lastIcePathID].LeftEdge.transform.position.x - _activeIcePaths[_lastIcePathID].Thickness;
			int startingRight = (int)_activeIcePaths[_lastIcePathID].RightEdge.transform.position.x + 1;

			// generate the obstacles on outer left
			for (int j = thicknessLeft; j > -WorldEdgeSize; j--)
			{
				Vector3 colPos = new Vector3(j, yPos, 0);
				CheckGenerateObstacle(colPos, _distributionOuterObstacle);
				CheckGenerateSnowVariation(colPos, _distributionSnowVariation);
			}
				
			// generate the obstacles on outer right
			for (int j = startingRight; j < _rightWorldEdge; j++)
			{
				Vector3 colPos = new Vector3(j, yPos, 0);
				CheckGenerateObstacle(colPos, _distributionOuterObstacle);
				CheckGenerateSnowVariation(colPos, _distributionSnowVariation);
			}
			
		}

		private void GenerateRowLevelEdge(float yPos)
		{
			Vector3 edgeTreePos = new Vector3(0, yPos, 0);

			// generate left
			edgeTreePos.x = -WorldEdgeSize;
			CreateObstacle("LargeTree", edgeTreePos);

			// generate right
			edgeTreePos.x = _rightWorldEdge;
			CreateObstacle("LargeTree", edgeTreePos);
		}

		/// <summary> Randomizes the next path to stay the same or expand or contract. </summary>
		private void RandomizeIcePath()
		{
			if (_activeIcePaths[_lastIcePathID].PossibleChunks.Count == 0)
				return;

			int id = _distributionIcePath.Next();
			List<string> possibilities = GetIcePathPossibilities(_currIcePathPossibilities[id]);

			if (possibilities.Count == 0)
			{
				possibilities = _activeIcePaths[_lastIcePathID].PossibleChunks;
			}

			_currWidthChunkName = possibilities[UnityEngine.Random.Range(0, possibilities.Count)];
		}

		private List<string> GetIcePathPossibilities(string choice)
		{
			List<string> possibilities = new();

			// stay same width
			if (choice == WeightedRandomID.ICE_PATH_FLAT)
			{
				possibilities = _activeIcePaths[_lastIcePathID].PossibleChunks;
				return possibilities;
			} 

			// expand path
			if (choice == WeightedRandomID.ICE_PATH_EXPAND)
			{
				if(!(_activeIcePaths[_lastIcePathID].Thickness + 1 < _currMaxChunkWidth))
				{
					return possibilities;
				}	

				possibilities = _activeIcePaths[_lastIcePathID].PossibleExpandChunks;
				return possibilities;
			} 

			// contract path
			if (choice == WeightedRandomID.ICE_PATH_CONTRACT)
			{
				possibilities = _activeIcePaths[_lastIcePathID].PossibleContractChunks;
				return possibilities;
			}

			int lastChunkX = (int)_activeIcePaths[_lastIcePathID].LeftEdge.transform.position.x;

			// turn path
			if (choice == WeightedRandomID.ICE_PATH_TURN && lastChunkX - 1 > -WorldEdgeSize)
			{
				possibilities = _activeIcePaths[_lastIcePathID].PossibleTurningChunks;
				return possibilities;
			}

			Debug.Log($"{this}: RandomizeIcePath: {choice} does not correlate with any ice path behavior.");
			return possibilities;
		}

		/// <summary> Creates and initializes a WorldTile given sprite props and returns the WorldSprite.  </summary>
		private bool CreateWorldObject(WorldSpriteProps objectProps, WorldObjectAction destroyCallback, out WorldSprite sprite)
		{
			// get sprite from pool			
			bool poolSuccess = PrimarySpritePooler.GetFromPool(out sprite);
			if (!poolSuccess)
			{
				Debug.LogError($"World CreateWorldObject failed. GetFromPool call returned false.");
				return false;
			}

			// initialize new sprite from props
			InitializeSpriteObject(sprite, objectProps.Sprite, objectProps.Order);

			// initialize game object from props
			InitializeGameObject(sprite.gameObject, objectProps.Position, objectProps.Parent);

			// initialize world object from sprite and game object
			InitializeWorldObject(sprite.UID, sprite.gameObject, sprite, destroyCallback);
			return true;
		}

		/// <summary> Create and initialize a chunk at position given width id. </summary>
		private void CreateChunk(Vector3 pos, string id)
		{
			if (!Assets.PathChunkPrefabs.ContainsKey(id))
			{
				Debug.LogError($"Cannot CreateChunk(). Chunk {id} does not exist.");
				return;
			}

			pos.x += Assets.PathChunkPrefabs[id].XOffset;

			WorldChunk chunk = Instantiate(Assets.PathChunkPrefabs[id], pos, Quaternion.identity, ActiveChunkParent);
			InitializeChunk(chunk, DestroyChunk);
		}

		private void CreateObstacle(string obstacleID, Vector3 pos)
		{
			// create new obstacle data and sprite props
			Obstacle obstacle = Assets.ObstacleIDs[obstacleID];
			WorldSpriteProps objProps = new(pos + obstacle.Offset, Assets.GetSprite(obstacle.SpriteID), obstacle.SortID, ActiveObjectParent);

			// try create sprite
			WorldSprite sprite;
			bool spriteSuccess = CreateWorldObject(objProps, DestroyObstacleObject, out sprite);

			if(!spriteSuccess)
			{
				Debug.LogError($"World CreateObstacle failed. CreateWorldObject call returned false.");
				return;
			}

			// set the sprite obstacle data
			sprite.SetObstacleData(obstacle);

			// subscribe sprite events
			sprite.PlaySFX = PlaySFX;
			sprite.CreatePFX = CreatePFXSprite;

			// add animator
			if (obstacle.AnimatorControllerID != 0)
			{
				sprite.EnableAnimator(Assets.AnimatorControllers[obstacle.AnimatorControllerID]);
			}

			// check for destroy on collision event subscribe
			if (obstacle.DestroyOnCollision)
			{
				sprite.OnObjectCollide += DestroyObstacleObject;
			}

			// check for extended obstacle
			if (obstacle.ExtendedObstacle)
			{
				float colPosX = pos.x + 1;
				CreateObstacle(obstacle.ExtendedObstacleID, new Vector3(colPosX, pos.y, pos.z));
				_currentChunkObstacles.Add(colPosX);
			}

			// add obstacle x position
			_currentChunkObstacles.Add(pos.x);
		}

		/// <summary> Callback for when a chunk should be destroyed. </summary>
		private void DestroyChunk(WorldChunk chunk)
		{
			// unsub events
			// OnUpdate -= _activeIcePaths[chunk.UID].UpdateChunk;
			OnSetWorldSpeed -= _activeIcePaths[chunk.UID].WorldObj.SetObjectSpeed;
			_activeIcePaths[chunk.UID].OnDestroy -= DestroyChunk;
			OnClearWorld -= _activeIcePaths[chunk.UID].TriggerDestroy;
			_activeIcePaths.Remove(chunk.UID);

			// destroy chunk
			Destroy(chunk.gameObject);
		}

		/// <summary> Destroy base WorldObject given id. </summary>
		private void DestroyWorldObject(int id)
		{
			// unsub events
			// OnUpdate -= _activeObjects[id].UpdateObject;
			OnSetWorldSpeed -= _activeObjects[id].SetObjectSpeed;

			// return to pool
			PrimarySpritePooler.ReturnToPool(_activeSprites[id]);

			// remove object
			_activeSprites.Remove(id);
			_activeObjects.Remove(id);
		}

		/// <summary> Destroy obstacle given WorldObject. </summary>
		/// <param name="obj"> The base tile to destroy. </param>
		private void DestroyObstacleObject(WorldObject obj)
		{
			int id = obj.GetId();

			// unsub events
			_activeObjects[id].OnDestroyTriggered -= DestroyObstacleObject;
			_activeSprites[id].OnObjectCollide -= DestroyObstacleObject;
			OnClearWorld -= _activeObjects[id].TriggerDestroy;
			_activeSprites[id].PlaySFX = null;
			_activeSprites[id].CreatePFX = null;

			// call base
			DestroyWorldObject(id);
		}

		/// <summary> Destroy obstacle given WorldObject id. </summary>
		/// <param name="uid"> The UID of the object to destroy. </param>
		private void DestroyObstacleObject(int id)
		{
			if (!_activeObjects.ContainsKey(id))
				return;

			// unsub events
			_activeSprites[id].OnObjectCollide -= DestroyObstacleObject;
			_activeObjects[id].OnDestroyTriggered -= DestroyObstacleObject;

			// call base
			DestroyWorldObject(id);
		}

		/// <summary> Updates the distance travelled and other metrics given speed </summary>
		/// <param name="speed"> The speed to update by. </param>
		private void UpdateDistanceTravelled(float speed)
		{
			// update distance travelled
			_distanceTravelled += speed;
			OnUpdateDistanceTravelled.Invoke(_distanceTravelled);

			// mps is the difference in distance travelled * 50 (fixed update 0.02s, which is 50 per sec)
			_metersPerSecond = (_distanceTravelled - _lastDistance) * 50;

			// 1 mps = 3.6 kmh
			_kmh = _metersPerSecond * 3.6f;
			OnUpdateKMH.Invoke(_kmh);

			// last distance is now the distance travelled
			_lastDistance = _distanceTravelled;
		}

		#endregion

		#region Unity Methods

		private void Awake()
		{
			InitializeStructures();
		}

		private void Start()
		{
			InitializeEvents();
		}

		private void Update()
		{
			OnUpdate.Invoke();
			_spritePoolInactiveCount = PrimarySpritePooler.Count;
		}

		private void FixedUpdate()
		{
			// OnUpdateWorldMovement.Invoke(WorldSpeed * DownslopeTime.TimeScale);
			UpdateDistanceTravelled(WorldSpeed * DownslopeTime.TimeScale);
		}

		#endregion
	}
}


