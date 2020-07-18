using CommandTerminal;
using DV;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DVRouteManager.CommsRadio
{
    public abstract class CRMSelectorSubPage : CRMSelectorPage, ICRMSubPage
    {
        public CRMSelectorSubPage(ICRMPageManager manager) :
            base(manager)
        {
        }

        public void Return()
        {
            Manager.Return();
        }
    }
}
