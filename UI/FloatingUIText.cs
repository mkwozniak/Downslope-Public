using System.Collections.Generic;
using UnityEngine;
using Text = TMPro.TextMeshProUGUI;

namespace Wozware.Downslope
{
	[RequireComponent(typeof(Text))]
	[RequireComponent(typeof(RectTransform))]
	public class FloatingUIText : MonoBehaviour
	{
		#region Events

		#endregion

		#region Public Members
		public float Speed;
		public Vector2 Direction;
		public bool UseFixedUpdate = false;
		public bool Loop;
		public float LoopMaxTime;
		public float FixedLoopMax;
		public float FixedLoopInterval;
		public List<string> LoopMessages;

		public bool FadeTextOverTime;
		public bool FadeFromTransparent;
		public float FadeSpeed;
		public float FixedFadeStep;
		#endregion

		#region Private Members
		private Vector2 _originalPosition;
		private RectTransform _transform;
		private Text _text;
		private float _currLoopTime;
		private Color _originalTextColor;
		private Color _currTextColor;
		private int _currLoopMessage;
		#endregion

		#region Public Methods

		#endregion

		#region Private Methods

		private void UpdateTextFade(bool fixedUpdate = false)
		{
			if(!FadeTextOverTime)
			{
				return;
			}

			_currTextColor = _text.color;
			if (FadeFromTransparent && _text.color.a < 1)
			{
				if(!fixedUpdate)
				{
					_currTextColor.a += FadeSpeed * Time.deltaTime;
				}
				else
				{
					_currTextColor.a += FadeSpeed * FixedFadeStep;
				}

			}

			if (_text.color.a > 0 && !FadeFromTransparent)
			{
				if (!fixedUpdate)
				{
					_currTextColor.a -= FadeSpeed * Time.deltaTime;
				}
				else
				{
					_currTextColor.a -= FadeSpeed * FixedFadeStep;
				}
			}
			_text.color = _currTextColor;
		}

		private void UpdateLoop(bool fixedUpdate = false)
		{
			if (!Loop)
			{
				return;
			}

			if(!fixedUpdate)
			{
				_currLoopTime += Time.deltaTime;
				if (_currLoopTime < LoopMaxTime)
				{
					return;
				}
			}
			else
			{
				_currLoopTime += FixedLoopInterval;
				if(_currLoopTime < FixedLoopMax)
				{
					return;
				}
			}


			_transform.anchoredPosition = _originalPosition;
			_currLoopTime = 0;
			SetLoopTextColor();
			_text.color = _currTextColor;

			if(LoopMessages.Count > 0)
			{
				IncrementLoopMessage();
			}
		}

		private void SetLoopTextColor()
		{
			if (FadeFromTransparent)
			{
				_currTextColor.a = 0;
				return;
			}
			_currTextColor = _originalTextColor;
		}

		private void IncrementLoopMessage()
		{
			_currLoopMessage += 1;
			if (_currLoopMessage >= LoopMessages.Count)
			{
				_currLoopMessage = 0;
			}
			_text.text = LoopMessages[_currLoopMessage];
		}

		#endregion

		#region Unity Methods

		private void Awake()
		{
			_text = GetComponent<Text>();
			_transform = GetComponent<RectTransform>();
			_originalPosition = _transform.anchoredPosition;
			_originalTextColor = _text.color;

			if(Loop && LoopMessages.Count > 0)
			{
				_currLoopMessage = 0;
				_text.text = LoopMessages[_currLoopMessage];
			}

			if (FadeFromTransparent)
			{
				_currTextColor = _text.color;
				_currTextColor.a = 0;
				_text.color = _currTextColor;
			}
		}

		private void Start()
		{

		}

		private void Update()
		{
			if(UseFixedUpdate)
			{
				return;
			}

			_transform.anchoredPosition += Direction * Speed * Time.deltaTime;
			UpdateTextFade();
			UpdateLoop();
		}

		private void FixedUpdate()
		{
			if(!UseFixedUpdate)
			{
				return;
			}

			_transform.anchoredPosition += Direction * Speed;
			UpdateTextFade(true);
			UpdateLoop(true);
		}

		#endregion
	}
}

