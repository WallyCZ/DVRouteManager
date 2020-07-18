using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DVRouteManager.CommsRadio
{
    public interface ICRMPageManager
    {
        void DisplayText(string message, string action);
        
        string GetDisplayText();

        void SetPage(Type nextPage, CRMPageArgs args = null, CRMPage prevPage = null);

        void SetSubPage(Type nextPage, Action<CRMPage> returnAction, CRMPageArgs args = null, CRMPage prevPage = null);

        void Return();

        bool CanReturn();

    }
}
