using System.Collections.Generic;
using System.Net.NetworkInformation;
using Unity.VisualScripting.Antlr3.Runtime;
using UnityEngine;
using Wozware.Downslope;
using Action = System.Action;

namespace Wozware.Downslope
{
	[System.Serializable]
	public sealed partial class Game
	{
		#region Events
		private event Action OnFinishDistanceTutorialStage;
		#endregion

		#region Public Members

		#endregion

		#region Private Members

		private bool _distanceTutorialStage = false;
		private bool[] _currTuturialInputsCompleted = new bool[3];
		[ReadOnly][SerializeField] private int _currTutorialStage = 0;
		[ReadOnly][SerializeField] private int _currTutorialObstacleStage = 0;
		private int _currDistanceTutorialVal = 0;

		private Dictionary<string, List<Action>> _tutorialInputPerformCallbacks = new Dictionary<string, List<Action>>();
		private Dictionary<string, List<Action>> _tutorialInputCancelCallbacks = new Dictionary<string, List<Action>>();

		#endregion

		#region Public Methods

		/// <summary>
		/// Resets the current tutorial progress data.
		/// </summary>
		public void ResetTutorialProgress()
		{
			_currTutorialStage = 0;

			for (int i = 0; i < _currTuturialInputsCompleted.Length; i++)
			{
				_currTuturialInputsCompleted[i] = false;
			}
		}

		/// <summary>
		/// Reset tutorial data and starts the initial tutorial stage.
		/// </summary>
		public void StartTutorialInitialStage()
		{
			ResetTutorialProgress();
			_ui.EnterTutorialView();
			_ui.EnableTutorialMessageView(true);
			_ui.EnableTutorialObjectiveView(false);
			_ui.SetTutorialMessageLabel(_ui.Tutorial.TutorialStages[_currTutorialStage].Message);
			_world.SetSnowVariationWeights(_assets.TutorialMapList[_currTutorialObstacleStage].SnowVariationWeights);
			PauseGameTime(true);

			// subscribe to enter input
			SubscribeTutorialInputPerformed(DownslopeInput.ENTER, EnterTutorialStageCarving);
			//DownslopeInput.SubscribeInputPerformed(DownslopeInput.ENTER, EnterTutorialStageCarving);
		}

		private void SubscribeTutorialInputPerformed(string input, Action action)
		{
			if(!_tutorialInputPerformCallbacks.ContainsKey(input))
			{
				_tutorialInputPerformCallbacks[input] = new List<Action>();
			}

			_tutorialInputPerformCallbacks[input].Add(action);
			DownslopeInput.SubscribeInputPerformed(input, action);
		}

		private void SubscribeTutorialInputCancelled(string input, Action action)
		{
			if (!_tutorialInputCancelCallbacks.ContainsKey(input))
			{
				_tutorialInputCancelCallbacks[input] = new List<Action>();
			}

			_tutorialInputCancelCallbacks[input].Add(action);
			DownslopeInput.SubscribeInputCancelled(input, action);
		}

		private void ClearTutorialInputPerformed(string input)
		{
			if (!_tutorialInputPerformCallbacks.ContainsKey(input))
			{
				return;
			}

			for (int i = 0; i < _tutorialInputPerformCallbacks[input].Count; i++)
			{
				DownslopeInput.UnsubscribeInputPerformed(input, _tutorialInputPerformCallbacks[input][i]);
			}
		}

		private void ClearTutorialInputCancelled(string input)
		{
			if (!_tutorialInputCancelCallbacks.ContainsKey(input))
			{
				return;
			}

			for (int i = 0; i < _tutorialInputCancelCallbacks[input].Count; i++)
			{
				DownslopeInput.UnsubscribeInputCancelled(input, _tutorialInputCancelCallbacks[input][i]);
			}
		}

		private void EnterTutorialStageCarving()
		{
			// enter next tutorial view
			EnterNextTutorialStageView();

			// unsubscribe any previous events on ENTER input
			ClearTutorialInputPerformed(DownslopeInput.ENTER);

			// subscribe new events to ENTER input
			SubscribeTutorialInputPerformed(DownslopeInput.ENTER, ContinueTutorialCarvingStage);
			SubscribeTutorialInputPerformed(DownslopeInput.ENTER, _player.StartInitialMovement);

			// pause game
			PauseGameTime(true);
		}

		private void EnterTutorialStageBraking()
		{
			if (_currTuturialInputsCompleted[0] && _currTuturialInputsCompleted[1])
			{
				// unsubscribe any previous events on CARVE inputs
				ClearTutorialInputCancelled(DownslopeInput.CARVE_LEFT);
				ClearTutorialInputCancelled(DownslopeInput.CARVE_RIGHT);

				// subscribe new events to BRAKE and ENTER input
				SubscribeTutorialInputCancelled(DownslopeInput.BRAKE, EnterNextObstacleTutorialStage);
				SubscribeTutorialInputPerformed(DownslopeInput.ENTER, ContinueTutorialPrompt);

				// pause game
				PauseGameTime(true);

				// hide objective view and show panel
				_ui.EnableTutorialObjectiveView(false);
				_ui.EnableTutorialMessageView(true);

				// enter next tuturoail view
				EnterNextTutorialStageView();

				// next stage is obstacles so prepare data
				_currTutorialObstacleStage = 0;
				_world.SetWorldWeights(_assets.TutorialMapList[0].ObstacleWeights, _assets.TutorialMapList[_currTutorialObstacleStage].OuterObstacleWeights,
					_assets.TutorialMapList[0].IcePathWeights, _world.ObstacleSeed);
			}
		}

		private void EnterNextObstacleTutorialStage()
		{
			// if the obstacle stage is 0, we just came from the previous non obstacle stage
			if(_currTutorialObstacleStage == 0)
			{
				if(IS_PAUSED)
				{
					return;
				}

				// unsubcribe any previous events on BRAKE or ENTER input
				ClearTutorialInputCancelled(DownslopeInput.BRAKE);
				ClearTutorialInputPerformed(DownslopeInput.ENTER);

				_ui.EnableTutorialObjectiveView(false);
			}

			OnFinishDistanceTutorialStage -= EnterNextObstacleTutorialStage;

			if (_currTutorialStage >= _ui.Tutorial.TutorialStages.Count)
			{
				return;
			}

			PauseGameTime(true);
			EnterNextTutorialStageView();
			DownslopeInput.SubscribeInputPerformed(DownslopeInput.ENTER, ContinueTutorialPrompt);

			_currTutorialObstacleStage++;

			if (_currTutorialObstacleStage >= _ui.Tutorial.TutorialStages.Count)
			{
				_distanceTutorialStage = false;
				_currDistanceTutorialVal = 0;
				return;
			}

			_world.SetWorldWeights(_assets.TutorialMapList[0].ObstacleWeights, _assets.TutorialMapList[_currTutorialObstacleStage].OuterObstacleWeights,
					_assets.TutorialMapList[0].IcePathWeights, _world.ObstacleSeed);

			_currDistanceTutorialVal = _world.DistanceTravelled();
			_distanceTutorialStage = true;
			OnFinishDistanceTutorialStage += EnterNextObstacleTutorialStage;
		}


		private void EnterNextTutorialStageView()
		{
			_currTutorialStage++;
			if (_currTutorialStage >= _ui.Tutorial.TutorialStages.Count)
			{
				_ui.ExitTutorialView();
				return;
			}

			_ui.EnableTutorialMessageView(true);
			_ui.SetTutorialMessageLabel(_ui.Tutorial.TutorialStages[_currTutorialStage].Message);
			if(_currTutorialStage != 1)
				PlaySound(SoundID.CHALLENGE_SUCCESS);
		}

		private void ContinueTutorialCarvingStage()
		{
			// unsubscribe any previous events on ENTER input
			ClearTutorialInputPerformed(DownslopeInput.ENTER);

			// subscribe to new events on cancel for CARVE inputs
			SubscribeTutorialInputCancelled(DownslopeInput.CARVE_LEFT, TutorialCarveLeft);
			SubscribeTutorialInputCancelled(DownslopeInput.CARVE_RIGHT, TutorialCarveRight);

			// continue tutorial prompt
			ContinueTutorialPrompt();
		}

		private void ContinueTutorialPrompt()
		{
			// unpause
			PauseGameTime(false);

			// hide tutorial panel and show objective
			_ui.EnableTutorialMessageView(false);
			_ui.EnableTutorialObjectiveView(true);
			_ui.SetTutorialObjectiveLabel(_ui.Tutorial.TutorialStages[_currTutorialStage].Objective);
		}

		private void TutorialCarveLeft()
		{
			_currTuturialInputsCompleted[0] = true;
			EnterTutorialStageBraking();
		}

		private void TutorialCarveRight()
		{
			_currTuturialInputsCompleted[1] = true;
			EnterTutorialStageBraking();
		}

		#endregion

		#region Private Methods

		private void UpdateTutorial()
		{
			if(!_distanceTutorialStage)
			{
				return;
			}

			int obstacleTutorialTravelled = _world.DistanceTravelled() - _currDistanceTutorialVal;
			if (obstacleTutorialTravelled >= TUTORIAL_OBSTACLE_STAGE_DIST)
			{
				_distanceTutorialStage = false;
				OnFinishDistanceTutorialStage.Invoke();
			}

			_ui.SetTutorialObjectiveLabel($"SURVIVE {TUTORIAL_OBSTACLE_STAGE_DIST - obstacleTutorialTravelled} METRES");
		}

		#endregion
	}

}
