using DV;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DVRouteManager.CommsRadio
{
    public interface ICRMSubPage : ICRMPage
    {
        void Return();
    }
    public abstract class CRMSubPage : CRMPage, ICRMSubPage
    {
        public CRMSubPage(ICRMPageManager manager) :
            base(manager)
        {
        }

        public void Return()
        {
            Manager.Return();
        }
    }
}
