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
    public class InitPage : CRMPage
    {
        private bool updateExecuted = false;
        private bool updateFinished = false;

        public InitPage(ICRMPageManager manager) :
            base(manager)
        {
        }

        public override void OnEnter(CRMPage previousPage, CRMPageArgs args)
        {
            if (Module.VersionForUpdate != null && !updateExecuted)
            {
                DisplayText($"NEW VERSION {Module.VersionForUpdate.Version} AVAILABLE!", "UPDATE");
                updateExecuted = true;

                Module.StartCoroutine(DoUpdate());
            }
            else
            {
                SetMainDisplay();
            }

        }

        private IEnumerator DoUpdate()
        {
            UnityWebRequest www = null;

            try
            {
                DisplayText("Downloading update...");

                www = UnityWebRequest.Get(Module.VersionForUpdate.downloadUrl);
                www.downloadHandler = new DownloadHandlerBuffer();
            }
            catch (Exception e)
            {
                Terminal.Log(e.Message + " " + e.StackTrace);
                SetPage(typeof(MainPage));
            }

            if (www != null)
            {
                yield return www.SendWebRequest();

                while (!www.downloadHandler.isDone)
                    yield return null;

                try
                {
                    if (!www.isHttpError && !www.isNetworkError)
                    {
                        DisplayText("Extracting update...");
                        string outFile = Path.GetTempFileName();

                        List<(string from, string to)> renamed = new List<(string, string)>();

                        try
                        {
                            File.WriteAllBytes(outFile, www.downloadHandler.data);

                            string assemblyPath = Path.GetDirectoryName(this.GetType().Assembly.Location);

                            //rename currently used DLLs
                            foreach (string file in Directory.GetFiles(assemblyPath, "*.dll"))
                            {
                                if (Path.GetFileName(file).StartsWith("DVRouteManager"))
                                    continue;

                                string renameTo = file + ".old";
                                File.Delete(renameTo);
                                System.IO.File.Move(file, renameTo);
                                renamed.Add((file, renameTo));
                            }

                            using (Unzip unzip = new Unzip(outFile))
                            {
                                unzip.ExtractToDirectory(
                                    Path.GetDirectoryName(assemblyPath)); //get parent directory
                            }

                            DisplayText("Done, update applies after game restart", "OK");

                            updateFinished = true;
                        }
                        catch(Exception exc)
                        {
                            Terminal.Log(exc.Message + ": " + exc.StackTrace);

                            //restore renamed files
                            foreach (var renamedFile in renamed)
                            {
                                System.IO.File.Move(renamedFile.to, renamedFile.from);
                            }
                        }
                        finally
                        {
                            File.Delete(outFile);
                        }
                    }
                }
                catch (Exception e)
                {
                    Terminal.Log(e.Message + " " + e.StackTrace);
                    SetPage(typeof(MainPage));
                }
            }
            else
            {
                SetPage(typeof(MainPage));
            }


        }
        public void SetMainDisplay()
        {
            DisplayText("Route Manager v" + this.GetType().Assembly.GetName().Version.ToString(3), "Menu");
        }

        public override void OnAction()
        {
            if(!updateExecuted || updateFinished)
            {
                SetPage(typeof(MainPage));
            }
        }
    }
}
