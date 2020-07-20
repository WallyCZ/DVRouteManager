using CommandTerminal;
using DV;
using DVRouteManager.Internals;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityAsync;
using UnityEngine.Networking;

namespace DVRouteManager.CommsRadio
{
    public class MessagePage : CRMPage
    {
        public const string PARAM_MESSAGE = "message";
        public const string PARAM_ACTION = "action";
        public const string PARAM_TIMEOUT = "timeout";
        public string message;
        public string action;
        protected bool exited = false;
        protected CancellationTokenSource waitForTimeoutCancellation;

        public MessagePage(ICRMPageManager manager) :
            base(manager)
        {
        }

        public override ButtonBehaviourType GetButtonBehaviour() => ButtonBehaviourType.Override;

        public override void OnEnter(CRMPage previousPage, CRMPageArgs args)
        {
            exited = false;

            string msg = args.GetString(PARAM_MESSAGE);
            if (msg != null)
            {
                action = args.GetString(PARAM_ACTION);
                message = msg;
                DisplayText(msg, action ?? "");
            }
            else
            {
                DisplayText(message, this.action ?? "");
            }

            float? timeout = args.GetFloat(PARAM_TIMEOUT);
            if(timeout.HasValue)
            {
                waitForTimeoutCancellation = new CancellationTokenSource();
                WaitForTimeout(timeout.Value);
            }
        }

        public override void OnLeave()
        {
            base.OnLeave();
            exited = true;
            if(waitForTimeoutCancellation != null)
            {
                waitForTimeoutCancellation.Cancel();
            }
        }

        public override void OnAction()
        {
            if (!exited)
            {
                SetPage(typeof(MainPage));
            }
        }

        protected async void WaitForTimeout(float timeout)
        {
            try
            {
                await new WaitForSeconds(timeout).ConfigureAwait(waitForTimeoutCancellation.Token);

                OnAction();
            }
            catch (OperationCanceledException)
            {
                // waiting was cancelled
            }

            waitForTimeoutCancellation = null;
        }
    }
}
