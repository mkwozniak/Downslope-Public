using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Text = TMPro.TextMeshProUGUI;

namespace Wozware.Downslope
{
	[System.Serializable]
	public sealed class DownslopeUI
	{
		#region Events

		#endregion

		#region Public Members

		public Transform MainMenuRootView;
		public Transform MainMenuFrontPanel;
		public Transform GameRootView;
		public Transform GameSidePanel;
		public Transform GameHealthPanel;
		public Transform EscapeRootView;

		public Text KMHLabel;
		public Text DistanceLabel;
		public TutorialUI Tutorial;

		#endregion

		#region Private Members


		#endregion

		#region Public Methods

		public void EnterMainMenuFront()
		{
			EnableMainMenuRoot(true);
			MainMenuFrontPanel.gameObject.SetActive(true);
		}

		public void ExitMainMenu()
		{
			EnableMainMenuRoot(false);
			MainMenuFrontPanel.gameObject.SetActive(false);
		}

		public void EnterGameViewFront()
		{
			EnableGameViewRoot(true);
			GameSidePanel.gameObject.SetActive(true);
			GameHealthPanel.gameObject.SetActive(true);
		}

		public void ExitGameView()
		{
			EnableGameViewRoot(false);
			GameSidePanel.gameObject.SetActive(false);
			GameHealthPanel.gameObject.SetActive(false);
		}

		public void EnterEscapeViewFront()
		{
			EnableEscapeViewRoot(true);
		}

		public void ExitEscapeView()
		{
			EnableEscapeViewRoot(false);
		}

		public void EnterTutorialView()
		{
			Tutorial.RootUIView.SetActive(true);
		}

		public void ExitTutorialView()
		{
			Tutorial.RootUIView.SetActive(false);
		}

		public void EnableTutorialMessageView(bool enable)
		{
			Tutorial.MessageView.SetActive(enable);
		}

		public void EnableTutorialObjectiveView(bool enable)
		{
			Tutorial.ObjectiveView.SetActive(enable);
		}

		public void SetTutorialMessageLabel(string text)
		{
			Tutorial.MessageLabel.text = text;
		}

		public void SetTutorialObjectiveLabel(string text)
		{
			Tutorial.ObjectiveLabel.text = text;
		}

		public void SetKMHLabel(float val)
		{
			KMHLabel.text = ((int)val).ToString();
		}

		public void SetDistanceLabel(float val)
		{
			string km = (val * 0.001f).ToString("F1");
			string m = ((int)val).ToString();
			DistanceLabel.text = (m).ToString();
		}

		#endregion

		#region Private Methods

		private void EnableMainMenuRoot(bool val)
		{
			MainMenuRootView.gameObject.SetActive(val);
		}

		private void EnableGameViewRoot(bool val)
		{
			GameRootView.gameObject.SetActive(val);
		}

		private void EnableTutorialViewRoot(bool val)
		{
			Tutorial.RootUIView.SetActive(val);
		}

		private void EnableEscapeViewRoot(bool val)
		{
			EscapeRootView.gameObject.SetActive(val);
		}

		#endregion

	}
}

