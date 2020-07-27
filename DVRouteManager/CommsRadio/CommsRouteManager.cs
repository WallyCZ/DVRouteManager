using CommandTerminal;
using DV;
using DV.Logic.Job;
using DVRouteManager.Internals;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace DVRouteManager.CommsRadio
{

    public class CommsRouteManager : MonoBehaviour, ICommsRadioMode, ICRMPageManager
    {
        CRMPage currentPage;

        Dictionary<Type, CRMPage> knownPages = new Dictionary<Type, CRMPage>();
        Stack<(CRMPage, Action<CRMPage>)> returnPages = new Stack<(CRMPage, Action<CRMPage>)>();

        public const string TITLE = "ROUTE MNGR";

        public CommsRadioDisplay display;

        public ButtonBehaviourType ButtonBehaviour { get; set; }

        public bool ButtonACustomAction()
        {
            return currentPage.ButtonACustomAction();
        }

        public bool ButtonBCustomAction()
        {
            return currentPage.ButtonBCustomAction();
        }

        public void Disable()
        {
        }

        public void Enable()
        {
        }

        public Color GetLaserBeamColor()
        {
            return new Color(0.5f, 0.5f, 0.5f, 0.0f);
        }

        public void OnUpdate()
        {         
        }

        public void OnUse()
        {
            currentPage.OnAction();
            return;
        }

        public void OverrideSignalOrigin(Transform signalOrigin)
        {
        }

        public void SetStartingDisplay()
        {
            try
            {
                if (currentPage == null)
                {

                    var types =
                        from type in Assembly.GetExecutingAssembly().GetTypes()
                        where ! type.IsAbstract && type.IsSubclassOf(typeof(CRMPage))
                        select type;

                    foreach (Type type in types)
                    {
                        CRMPage page = Activator.CreateInstance(type, (ICRMPageManager) this) as CRMPage;
                        knownPages.Add(type, page);
                    }

                    SetPage(typeof(InitPage));
                }
                else
                {
                    SetPage(currentPage.GetType());
                }
            }
            catch (Exception exc)
            {
                Terminal.Log($"{exc.Message} {exc.StackTrace}");

                if(exc.InnerException != null)
                {
                    Terminal.Log($"{exc.InnerException.Message} {exc.InnerException.StackTrace}");
                }
            }
        }

        public void DisplayText(string message, string action)
        {
            display.SetDisplay(TITLE, message, action);
        }

        public void SetPage(Type nextPageType, CRMPageArgs args = null, CRMPage prevPage = null)
        {
            try
            {
                CRMPage nextPage;

                if (!knownPages.TryGetValue(nextPageType, out nextPage))
                {
                    Terminal.Log($"Unknown page {nextPageType}");
                    return;
                }
                if (currentPage != null)
                {
                    currentPage.OnLeave();
                }

                CRMPage lastPage = prevPage ?? currentPage;
                currentPage = nextPage;

                currentPage.OnEnter(lastPage, args ?? new CRMPageArgs());

                this.ButtonBehaviour = currentPage.GetButtonBehaviour();
            }
            catch (Exception exc)
            {
                Terminal.Log($"{exc.Message} {exc.StackTrace}");

                if (exc.InnerException != null)
                {
                    Terminal.Log($"{exc.InnerException.Message} {exc.InnerException.StackTrace}");
                }
            }

        }

        public void SetSubPage(Type nextPage, Action<CRMPage> action, CRMPageArgs args = null, CRMPage prevPage = null)
        {
            if( ! typeof(ICRMSubPage).IsAssignableFrom(nextPage))
            {
                Terminal.Log($"Page {nextPage} called as subpage but it does not implement ICRMSubPage");
            }

            returnPages.Push((currentPage, action));

            SetPage(nextPage, args ?? new CRMPageArgs(), prevPage);
        }

        public bool CanReturn()
        {
            return returnPages.Count > 0;
        }

        public void Return()
        {
            if( ! CanReturn())
            {
                Terminal.Log("No return page!!!");
                SetPage(typeof(InitPage));
                return;
            }

            var (returnPage, returnAction) = returnPages.Pop();
            currentPage.OnLeave();
            var lastPage = currentPage;
            currentPage = returnPage;
            if (returnAction == null)
            {
                currentPage.OnEnter(lastPage, new CRMPageArgs());
            }
            else
            {
                returnAction(lastPage);
            }

            this.ButtonBehaviour = currentPage.GetButtonBehaviour();
        }

        public string GetDisplayText()
        {
            return display.content.text;
        }
    }
}
