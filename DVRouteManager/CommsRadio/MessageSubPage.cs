using CommandTerminal;
using DVRouteManager.Internals;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine.Networking;

namespace DVRouteManager.CommsRadio
{
    public class MessageSubPage : MessagePage, ICRMSubPage
    {
        public MessageSubPage(ICRMPageManager manager) :
            base(manager)
        {
        }

        public override void OnAction()
        {
            if (!exited)
            {
                Return();
            }
        }

        public void Return()
        {
            Manager.Return();
        }
    }
}
