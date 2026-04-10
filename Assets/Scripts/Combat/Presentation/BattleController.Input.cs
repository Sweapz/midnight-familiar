using UnityEngine;

namespace MidnightFamiliar.Combat.Presentation
{
    public partial class BattleController
    {
        private void HandlePlayerKeyboardShortcuts()
        {
            if (_activePlayerActor == null || _isMovingActor || _awaitingOpportunityChoice)
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.Space))
            {
                HandleActionPanelEndTurnClicked();
                return;
            }

            if (Input.GetKeyDown(KeyCode.Q))
            {
                HandleActionPanelActionClicked(0);
                return;
            }

            if (Input.GetKeyDown(KeyCode.E))
            {
                HandleActionPanelActionClicked(1);
                return;
            }

            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                HandleActionPanelActionClicked(2);
                return;
            }

            if (Input.GetKeyDown(KeyCode.Alpha2))
            {
                HandleActionPanelActionClicked(3);
                return;
            }

            if (Input.GetKeyDown(KeyCode.Alpha3))
            {
                HandleActionPanelActionClicked(4);
            }
        }
    }
}
