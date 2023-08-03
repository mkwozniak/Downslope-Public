using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
// using UnityEngine.InputSystem;
using Button = UnityEngine.UI.Button;
using Image = UnityEngine.UI.Image;
using Text = TMPro.TextMeshProUGUI;

namespace Wozware.Downslope
{
	[RequireComponent(typeof(RectTransform))]
	public class UIElement : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
	{
		#region Public Members

		public bool IsButton;
		public Button Btn;

		public bool HideBackgroundOnHover;
		public UnityEvent OnHoverEvents;

		public Image Background;
		public Image Icon;
		public Text Txt;

		public Vector3 DefaultScale;
		public Vector3 OnHoverScale;

		public bool HideOnCoverWorldTransform;
		public Transform WorldHideTransform;
		public Vector2 WorldHideBoundSize;

		#endregion

		#region Private Members

		private RectTransform _transform;
		[ReadOnly][SerializeField] private Vector3 _coverWorldScreenPos;
		[SerializeField] private Bounds _selfBounds;
		[SerializeField] private Bounds _coverBounds;

		private Color _bgColor;
		private Color _txtColor;
		private Color _iconColor;

		#endregion

		#region Public Methods

		public void SetParent(RectTransform t)
		{
			_transform.parent = t;
		}

		public void Hide()
		{
			if (Background != null)
			{		
				Background.color = Color.clear;
			}

			if(Txt != null)
			{
				Txt.color = Color.clear;
			}

			if(Icon != null)
			{
				Icon.color = Color.clear;
			}
		}

		public void OnHoverEnter()
		{
			_transform.localScale = OnHoverScale;

			if (HideBackgroundOnHover)
			{
				Background.color = Color.clear;
			}

			if (OnHoverEvents != null)
			{
				OnHoverEvents.Invoke();
			}
		}

		public void OnHoverExit()
		{
			_transform.localScale = DefaultScale;

			if (Background != null)
			{
				Background.color = _bgColor;
			}
		}

		public void OnPointerEnter(PointerEventData eventData)
		{
			OnHoverEnter();
		}

		public void OnPointerExit(PointerEventData eventData)
		{
			OnHoverExit();
		}

		private void Awake()
		{
			_transform = GetComponent<RectTransform>();

			if (Background != null)
			{
				_bgColor = Background.color;
			}

			if (Txt != null)
			{
				_txtColor = Txt.color;
			}

			if (Icon != null)
			{
				_iconColor = Icon.color;
			}

			if (IsButton)
			{
				Btn = GetComponent<Button>();
			}
		}

		#endregion

		#region Private Methods

		#endregion

		#region Unity Methods

		private void Start()
		{

		}

		private void Update()
		{
			if (HideOnCoverWorldTransform)
			{
				Vector3 worldPos = WorldHideTransform.position;
				worldPos.z = _transform.position.z;

				_coverWorldScreenPos = Camera.main.WorldToScreenPoint(worldPos);
				_coverWorldScreenPos.z = _transform.position.z;
				_selfBounds = new Bounds(_transform.position, new Vector3(_transform.sizeDelta.x, _transform.sizeDelta.y, 1));		
				_coverBounds = new Bounds(_coverWorldScreenPos, new Vector3(WorldHideBoundSize.x, WorldHideBoundSize.y, 1));

				if (!_selfBounds.Intersects(_coverBounds))
				{
					gameObject.SetActive(true);
					return;
				}

				gameObject.SetActive(false);
			}
		}

		#endregion
	}
}

