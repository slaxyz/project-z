using ProjectZ.Core;
using UnityEngine;

namespace ProjectZ.UI
{
    public class SceneNavigationUI : MonoBehaviour
    {
        private static GameFlowManager Manager => GameFlowManager.Instance;

        public void OpenHome()
        {
            if (Manager == null)
            {
                return;
            }

            Manager.GoToHome();
        }

        public void OpenCollection()
        {
            if (Manager == null)
            {
                return;
            }

            Manager.GoToCollection();
        }

        public void OpenTeamSelect()
        {
            if (Manager == null)
            {
                return;
            }

            Manager.OpenPlayEntry();
        }

        public void StartRun()
        {
            if (Manager == null)
            {
                return;
            }

            Manager.StartRun();
        }

        public void OpenBoard()
        {
            if (Manager == null)
            {
                return;
            }

            Manager.OpenBoard();
        }

        public void StartFight()
        {
            if (Manager == null)
            {
                return;
            }

            Manager.StartFight();
        }

        public void ShowVictoryResult()
        {
            if (Manager == null)
            {
                return;
            }

            Manager.ShowResult(true);
        }

        public void ShowDefeatResult()
        {
            if (Manager == null)
            {
                return;
            }

            Manager.ShowResult(false);
        }

        public void NextBoardNode()
        {
            if (Manager == null)
            {
                return;
            }

            Manager.NextBoardNode();
        }

        public void EndRunWithSmallReward()
        {
            if (Manager == null)
            {
                return;
            }

            Manager.EndRun(10);
        }
    }
}
