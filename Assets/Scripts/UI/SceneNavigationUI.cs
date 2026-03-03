using ProjectZ.Core;
using UnityEngine;

namespace ProjectZ.UI
{
    public class SceneNavigationUI : MonoBehaviour
    {
        public void OpenHome()
        {
            GameFlowManager.Instance.GoToHome();
        }

        public void OpenCollection()
        {
            GameFlowManager.Instance.GoToCollection();
        }

        public void OpenTeamSelect()
        {
            GameFlowManager.Instance.GoToTeamSelect();
        }

        public void StartRun()
        {
            GameFlowManager.Instance.StartRun();
        }

        public void OpenBoard()
        {
            GameFlowManager.Instance.OpenBoard();
        }

        public void StartFight()
        {
            GameFlowManager.Instance.StartFight();
        }

        public void ShowVictoryResult()
        {
            GameFlowManager.Instance.ShowResult(true);
        }

        public void ShowDefeatResult()
        {
            GameFlowManager.Instance.ShowResult(false);
        }

        public void NextBoardNode()
        {
            GameFlowManager.Instance.NextBoardNode();
        }

        public void EndRunWithSmallReward()
        {
            GameFlowManager.Instance.EndRun(10);
        }
    }
}
