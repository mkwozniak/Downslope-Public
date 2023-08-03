using System.Collections.Generic;
using UnityEngine;
using System;

namespace Wozware.Downslope
{
	public sealed class WorldObject : MonoBehaviour
	{
		public WorldObjectAction OnDestroyTriggered;
		public Action OnUpdate;

		public WorldMovementProps Movement
		{
			get
			{
				return _movement;
			}
		}

		private Vector3 _velocity;
		private WorldObjectProps _props;
		private WorldMovementProps _movement;
		private bool _initialized = false;
		private bool _destroyed;
		private float _currSpeed;

		public void Initialize(WorldObjectProps props, WorldMovementProps movement)
		{
			_props = props;
			_movement = movement;
			_initialized = true;
			_destroyed = false;
			OnUpdate = UpdateObject;
		}

		public void SetMovement(WorldMovementProps movement)
		{
			_movement = movement;
		}

		public GameObject GetObj()
		{
			return _props.Obj;
		}

		public int GetId()
		{
			return _props.GenID;
		}

		public void SetParent(Transform transform)
		{
			_props.Obj.transform.SetParent(transform);
		}

		public void SetObjectSpeed(float speed)
		{
			_currSpeed = speed;
		}

		public void UpdateObjectMovement()
		{
			if (_destroyed || !_initialized)
			{
				return;
			}

			_velocity.x = _movement.Direction.x * _currSpeed;
			_velocity.y = _movement.Direction.y * _currSpeed;

			_props.Obj.transform.position += _velocity;
		}

		public void UpdateObject()
		{
			if(!_initialized)
			{
				return;
			}

			if (_props.Obj.transform.position.y > _movement.MaxY && !_destroyed)
			{
				TriggerDestroy();
			}
		}

		public void TriggerDestroy()
		{
			OnDestroyTriggered.Invoke(this);
			_initialized = false;
			_destroyed = true;
		}

		public void Update()
		{
			UpdateObject();
		}

		public void FixedUpdate()
		{
			UpdateObjectMovement();
		}
	}
}

