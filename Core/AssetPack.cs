using System.Collections.Generic;
using UnityEngine;
using static UnityEngine.RuleTile.TilingRuleOutput;

namespace Wozware.Downslope
{
	[CreateAssetMenu(fileName = "AssetPack", menuName = "Downslope/AssetPack", order = 1)]
	public sealed class AssetPack : ScriptableObject
	{
		public List<RuntimeAnimatorController> AnimatorControllers;
		public AudioSource SFXPrefab;
		public GameObject PowderSnowFX;
		public GameObject SmallTreeSnowFX;

		public List<Sprite> SpriteList;
		public List<Obstacle> Obstacles;
		public List<WeightIdentity> DefaultObstacleWeights;
		public List<WeightIdentity> DefaultOuterObstacleWeights;
		public List<WeightIdentity> DefaultSnowVariationWeights;
		public List<WeightIdentity> DefaultIcePathWeights;
		public List<AudioClip> Music;
		public List<AudioClip> AmbientSounds;
		public List<SFXData> SFXList;
		public List<PFXData> PFXList;
		public WeightedMapData ClearMapData;
		public List<WeightedMapData> TutorialMapList;
		public List<WeightedMapData> BeginnerArcadeMapList;

		public Dictionary<string, Sprite> Sprites;
		public Dictionary<CollisionTypes, GameObject> CollidePlayerFX;
		public Dictionary<string, WeightedMapData> ArcadeModeMaps;
		public Dictionary<string, WorldChunk> PathChunkPrefabs;
		public Dictionary<string, ObstacleColliderData> ObstacleColliders;
		public Dictionary<string, SFXData> SFX;
		public Dictionary<string, PFXData> PFX;
		public Dictionary<int, Sprite> PlayerTrailSprites;
		public Dictionary<string, Obstacle> ObstacleIDs;

		[SerializeField] private List<WorldChunk> _pathChunkPrefabs;
		[SerializeField] private List<Sprite> _playerTrailSprites;

		public void Initialize()
		{
			SFX = new Dictionary<string, SFXData>();
			PFX = new Dictionary<string, PFXData>();
			Sprites = new Dictionary<string, Sprite>();
			ObstacleIDs = new Dictionary<string, Obstacle>();
			ArcadeModeMaps = new Dictionary<string, WeightedMapData>();
			CollidePlayerFX = new Dictionary<CollisionTypes, GameObject>();
			PathChunkPrefabs = new Dictionary<string, WorldChunk>();
			ObstacleColliders = new Dictionary<string, ObstacleColliderData>();
			PlayerTrailSprites = new Dictionary<int, Sprite>();

			int i;
			for (i = 0; i < _pathChunkPrefabs.Count; i++)
			{
				PathChunkPrefabs.Add(_pathChunkPrefabs[i].name, _pathChunkPrefabs[i]);
			}

			for (i = 0; i < SpriteList.Count; i++)
			{
				Sprites.Add(SpriteList[i].name, SpriteList[i]);
			}

			for (i = 0; i < Obstacles.Count; i++)
			{
				ObstacleIDs.Add(Obstacles[i].Name, Obstacles[i]);
			}

			for (i = 0; i < SFXList.Count; i++)
			{
				SFX.Add(SFXList[i].Name, SFXList[i]);
			}

			for (i = 0; i < PFXList.Count; i++)
			{
				PFX.Add(PFXList[i].Name, PFXList[i]);
			}

			for (i = 0; i < _playerTrailSprites.Count; i++)
			{
				PlayerTrailSprites.Add(i, _playerTrailSprites[i]);
			}

			for (i = 0; i < BeginnerArcadeMapList.Count; i++)
			{
				ArcadeModeMaps.Add(BeginnerArcadeMapList[i].Name, BeginnerArcadeMapList[i]);
			}

		}

		public Sprite GetSprite(string id)
		{
			if (!Sprites.ContainsKey(id))
				return Sprites["empty"];

			return Sprites[id];
		}

		public AudioClip GetAmbientClip(int id)
		{
			if (id >= SFXList.Count || id < 0)
				return AmbientSounds[0];

			return AmbientSounds[id];
		}

		public bool TryGetSFX(string id, out AudioClip clip)
		{
			if (!SFX.ContainsKey(id))
			{
				Debug.LogWarning($"AssetPack: TryGetSFX: Sound {id} does not exist. Out is null.");
				clip = null;
				return false;
			}

			int ranClip = Random.Range(0, SFX[id].Clips.Count);
			clip = SFX[id].Clips[ranClip];
			return true;
		}

		public bool TryCreatePFX(string id, Vector3 pos, out ParticleSystem pfxOut, UnityEngine.Transform parent = null)
		{
			if (!PFX.ContainsKey(id))
			{
				Debug.LogWarning($"AssetPack: TryCreatePFX does not contain id: {id}. Out is null.");
				pfxOut = null;
				return false;
			}

			int ranFX = Random.Range(0, PFX[id].FX.Count);
			pfxOut = Instantiate(PFX[id].FX[ranFX], pos, Quaternion.identity, parent);
			return true;
		}
	}
}



