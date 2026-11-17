#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;

namespace CyberCity.Editor
{
    // Temporary local test driver; a request file opts into this single, fixed test sequence.
    [InitializeOnLoad]
    public static class RevisionVerificationRun
    {
        const string Request = "Temp/cybercity-revision-verify.request";
        const string Report = "Documentation/RevisionEvidence/full-run.txt";
        static int stage;
        static double deadline, next;
        static RevisionVerificationRun() { EditorApplication.delayCall += Start; }
        static void Start()
        {
            if (!File.Exists(Request) || EditorApplication.isPlayingOrWillChangePlaymode) return;
            File.Delete(Request);
            try
            {
                CyberCityInteriorBuilder.Build();
                CyberCityInteriorVerification.VerifyCore();
                File.Copy("Documentation/RevisionEvidence/interior-tests.txt","Documentation/RevisionEvidence/core-tests.txt",true);
                stage=1; deadline=EditorApplication.timeSinceStartup+600;
                EditorApplication.update+=Tick;
                File.WriteAllText(Report,"RUNNING: interiors, arcade input, campaign playthrough, Windows build\n");
                EditorApplication.isPlaying=true;
            }
            catch(Exception e) { Fail(e); }
        }
        static void Tick()
        {
            if(EditorApplication.timeSinceStartup<next) return;
            next=EditorApplication.timeSinceStartup+.5;
            try
            {
                if(EditorApplication.timeSinceStartup>deadline) throw new TimeoutException("Revision verification timed out.");
                if(stage==1 && EditorApplication.isPlaying && UnityEngine.Object.FindAnyObjectByType<CyberCityGame>()?.UI?.CanvasRoot!=null)
                { stage=2; CyberCityInteriorVerification.VerifyPlayMode(); }
                else if(stage==2 && CyberCityInteriorVerification.Status.StartsWith("FAILED")) throw new Exception(CyberCityInteriorVerification.Status);
                else if(stage==2 && CyberCityInteriorVerification.Status.StartsWith("PASS:"))
                { File.AppendAllText(Report,CyberCityInteriorVerification.Status+"\n"); stage=3; CyberCityVerification.StartPlaythrough(); }
                else if(stage==3 && !EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    EditorApplication.update-=Tick;
                    if(!CyberCityVerification.Status.StartsWith("PASS:")) throw new Exception(CyberCityVerification.Status);
                    File.AppendAllText(Report,CyberCityVerification.Status+"\n");
                    CyberCityBuilder.BuildWindows();
                    File.AppendAllText(Report,"PASS: Windows build includes enabled scenes.\n"); stage=4;
                }
            }
            catch(Exception e) { Fail(e); }
        }
        static void Fail(Exception e)
        {
            EditorApplication.update-=Tick;
            File.WriteAllText(Report,"FAILED: "+e+"\n");
            UnityEngine.Debug.LogError(e);
        }
    }
}
#endif
