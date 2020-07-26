using CommandTerminal;
using DV;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DVRouteManager.CommsRadio
{
    public abstract class CRMSelectorPage : CRMPage
    {
        protected Selector<MenuItem> menuSelector { get; private set; }

        protected bool selectorSetup = false;

        public CRMSelectorPage(ICRMPageManager manager) :
            base(manager)
        {
        }

        protected void SetupSelector()
        {
            selectorSetup = true;

            menuSelector = new Selector<MenuItem>(CreateMenuItems());

            if (menuSelector.Current == null)
            {
                menuSelector.MoveNextRewind();
            }

            if(menuSelector.Current == null)
            {
                Terminal.Log("Selector has no items!");
            }
        }

        protected abstract List<MenuItem> CreateMenuItems();

        public override ButtonBehaviourType GetButtonBehaviour() => ButtonBehaviourType.Override;

        public override void OnAction()
        {
            menuSelector.Current.action();
        }

        public override void OnEnter(CRMPage previousPage, CRMPageArgs args)
        {
            if(!selectorSetup)
            {
                SetupSelector();
            }

            PrintCurrentSelector();
        }

        public override bool ButtonACustomAction()
        {
            menuSelector.MoveNextRewind();
            DisplayText(menuSelector.Current.displayText, menuSelector.Current.actionName);
            return true;
        }
        public override bool ButtonBCustomAction()
        {
            menuSelector.MovePrevRewind();
            DisplayText(menuSelector.Current.displayText, menuSelector.Current.actionName);
            return true;
        }

        public virtual void PrintCurrentSelector()
        {
            DisplayText(menuSelector.Current.displayText, menuSelector.Current.actionName);
        }

    }
}
