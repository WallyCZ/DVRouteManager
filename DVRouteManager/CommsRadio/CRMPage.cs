using DV;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DVRouteManager.CommsRadio
{
    public interface ICRMPage
    {
        void OnEnter(CRMPage previousPage, CRMPageArgs args);
        void OnAction();
    }

    public abstract class CRMPage : ICRMPage
    {
        protected ICRMPageManager Manager { get; private set; }
        public CRMPage(ICRMPageManager manager)
        {
            Manager = manager;
        }

        protected virtual void Init()
        {
        }



        public abstract void OnEnter(CRMPage previousPage, CRMPageArgs args);

        public abstract void OnAction();

        public virtual bool ButtonACustomAction()
        {
            return false;
        }
        public virtual bool ButtonBCustomAction()
        {
            return false;
        }

        public virtual void OnLeave()
        {
        }

        public virtual ButtonBehaviourType GetButtonBehaviour()
        {
            return ButtonBehaviourType.Regular;
        }

        internal void DisplayText(string message, string action = "")
        {
            Manager.DisplayText(message, action);
        }

        protected void SetPage(Type nextPage, CRMPageArgs args = null)
        {
            Manager.SetPage(nextPage, args);
        }

        protected void SetSubPage(Type nextPage, CRMPageArgs args = null, Action<CRMPage> returnAction = null)
        {
            Manager.SetSubPage(nextPage, returnAction, args);
        }

        protected void SetMainMenuPage()
        {
            SetPage(typeof(MainPage));
        }

        internal void RedirectToMessagePage(string message, string action, float? timeout = null)
        {
            CRMPageArgs args = new CRMPageArgs();
            args.AddString(MessagePage.PARAM_MESSAGE, message);
            args.AddString(MessagePage.PARAM_ACTION, action);

            if(timeout.HasValue)
            {
                args.AddFloat(MessagePage.PARAM_TIMEOUT, timeout.Value);
            }

            SetPage(typeof(MessagePage), args);
        }

        internal void CallMessageSubPage(string message, string action, float? timeout = null, Action<CRMPage> returnAction = null)
        {
            CRMPageArgs args = new CRMPageArgs();
            args.AddString(MessagePage.PARAM_MESSAGE, message);
            args.AddString(MessagePage.PARAM_ACTION, action);

            if (timeout.HasValue)
            {
                args.AddFloat(MessagePage.PARAM_TIMEOUT, timeout.Value);
            }

            SetSubPage(typeof(MessageSubPage), args, returnAction);
        }

        protected MenuItem GetExitMenu()
        {
            return new MenuItem("Back", "Exit", () => SetPage(typeof(InitPage)));
        }

    }
}
