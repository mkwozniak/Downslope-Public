using System.Collections.Generic;
using UnityEngine.InputSystem;
using Action = System.Action;

namespace Wozware.Downslope
{
	public static class DownslopeInput
	{
		#region Events

		#endregion

		#region Public Members

		public static Dictionary<string, bool> INPUTS = new Dictionary<string, bool>();
		public static string ENTER;
		public static string ESCAPE;
		public static string CARVE_LEFT;
		public static string CARVE_RIGHT;
		public static string BRAKE;

		#endregion

		#region Private Members

		private static DownslopeControls _CONTROLS;
		private static Dictionary<string, InputAction> _ACTIONS = new Dictionary<string, InputAction>();
		private static Dictionary<string, Action> _PERFORMED = new Dictionary<string, Action>();
		private static Dictionary<string, Action> _CANCELLED = new Dictionary<string, Action>();

		#endregion

		#region Public Methods

		/// <summary>
		/// Initializes the DownslopeControls class and binds its actions.
		/// </summary>
		public static void InitializeControls()
		{
			_CONTROLS = new DownslopeControls();

			BindAction(_CONTROLS.Player.Enter, ref ENTER);
			BindAction(_CONTROLS.Player.Escape, ref ESCAPE);
			BindAction(_CONTROLS.Player.CarveLeft, ref CARVE_LEFT);
			BindAction(_CONTROLS.Player.CarveRight, ref CARVE_RIGHT);
			BindAction(_CONTROLS.Player.Brake, ref BRAKE);
		}

		/// <summary>
		/// Subscribe to performed input event with an action.
		/// </summary>
		/// <param name="input"> The name of the input. </param>
		/// <param name="callback"> The callback to subscribe. </param>
		public static void SubscribeInputPerformed(string input, Action callback)
		{
			if (!_PERFORMED.ContainsKey(input))
				return;
			_PERFORMED[input] += callback;
		}

		/// <summary>
		/// Subscribe to cancelled input event with an action.
		/// </summary>
		/// <param name="input"> The name of the input. </param>
		/// <param name="callback"> The callback to subscribe. </param>
		public static void SubscribeInputCancelled(string input, Action callback)
		{
			if (!_CANCELLED.ContainsKey(input))
				return;
			_CANCELLED[input] += callback;
		}

		/// <summary>
		/// Usubscribe to performed input event with an action.
		/// </summary>
		/// <param name="input"> The name of the input. </param>
		/// <param name="callback"> The callback to unsubscribe. </param>
		public static void UnsubscribeInputPerformed(string input, Action callback)
		{
			if (!_PERFORMED.ContainsKey(input))
				return;
			_PERFORMED[input] -= callback;
		}

		/// <summary>
		/// Unsubscribe from cancelled input event with an action.
		/// </summary>
		/// <param name="input"> The name of the input.</param>
		/// <param name="callback"> The callback to unsubscribe. </param>
		public static void UnsubscribeInputCancelled(string input, Action callback)
		{
			if (!_CANCELLED.ContainsKey(input))
				return;
			_CANCELLED[input] -= callback;
		}

		#endregion

		#region Private Methods

		/// <summary>
		/// Binds an input action by enabling it and adding its default callbacks.
		/// </summary>
		/// <param name="action"> The Unity InputAction to bind. </param>
		private static void BindAction(InputAction action, ref string name)
		{
			name = action.name;
			_ACTIONS[name] = action;
			_ACTIONS[name].Enable();
			_ACTIONS[name].performed += OnInputPerformed;
			_ACTIONS[name].canceled += OnInputCanceled;
			_PERFORMED.Add(name, () => { });
			_CANCELLED.Add(name, () => { });
			INPUTS[name] = false;
		}

		/// <summary>
		/// Callback for when an input is performed.
		/// </summary>
		/// <param name="action"></param>
		private static void OnInputPerformed(InputAction.CallbackContext action)
		{
			string name = action.action.name;
			INPUTS[name] = true;

			if (_PERFORMED.ContainsKey(name))
			{
				_PERFORMED[name].Invoke();
			}
		}

		/// <summary>
		/// Callback for when an input is cancelled.
		/// </summary>
		/// <param name="action"></param>
		private static void OnInputCanceled(InputAction.CallbackContext action)
		{
			string name = action.action.name;
			INPUTS[name] = false;

			if (_CANCELLED.ContainsKey(name))
			{
				_CANCELLED[name].Invoke();
			}
		}


		#endregion
	}
}


