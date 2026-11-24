using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

internal static class VerifyOfficeAudioAndView
{
    const string Dir = "Temp/OfficeAudioCheck/";
    const string Running = "OfficeAudioAndView.Verify";
    static int phase;
    static double change, deadline;
    static Keyboard keyboard;
    static Camera cam;
    static AudioSource salsa;
    static PlayerInput input;
    static Animator animator;
    static Vector3 firstPosition;
    static double Now => EditorApplication.timeSinceStartup;
    [InitializeOnLoadMethod]
    static void Register()
    {
        EditorApplication.delayCall += Begin;
    }
    static void Assert(bool ok,string message) { if (!ok) throw new Exception(message); }
    static void Begin()
    {
        if (File.Exists(Dir+"checked.txt")) return;
        if (SessionState.GetBool(Running,false))
        {
            deadline=Now+45;
            EditorApplication.update += Tick;
            return;
        }
        try
        {
            var scene=EditorSceneManager.OpenPreviewScene("Assets/Scenes/Oficina.unity");
            try
            {
                var all=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<AudioSource>()).ToArray();
                foreach(var s in all) File.AppendAllText(Dir+"report.txt",$"{s.name}: {s.clip?.name} awake={s.playOnAwake} loop={s.loop} spatial={s.spatialBlend} duration={s.clip?.length}\n");
                Assert(all.Length==3 && all.All(s=>s.clip && s.loop),"Three looping sources must load");
                Assert(all.Any(s=>s.playOnAwake && s.spatialBlend>.99f && s.clip.name.Contains("lights-flicker")),"Positional light");
                Assert(all.Any(s=>s.playOnAwake && s.spatialBlend==0 && s.clip.name.Contains("abandoned-rooms")),"Ambience");
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
            var active=SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || active.isDirty || active.path!="Assets/Scenes/Oficina.unity")
            { File.WriteAllText(Dir+"checked.txt","Sources checked. Interactive session busy."); return; }
            SessionState.SetBool(Running,true);
            deadline=Now+60;
            EditorApplication.update += Tick;
            EditorApplication.EnterPlaymode();
        }
        catch(Exception ex) { End(ex.ToString()); }
    }
    static void End(string result)
    {
        bool started=SessionState.GetBool(Running,false);
        SessionState.SetBool(Running,false);
        EditorApplication.update-=Tick;
        if(keyboard!=null && keyboard.added) InputSystem.RemoveDevice(keyboard);
        File.WriteAllText(Dir+"checked.txt",result);
        if(started && EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
    }
    static void Press(params Key[] keys) => InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));
    static void Next() { phase++; change=Now; }
    static void Tick()
    {
        try
        {
            if(Now>deadline) throw new Exception("Timeout at phase "+phase);
            if(!EditorApplication.isPlaying || Time.frameCount<8 || Now-change<.28) return;
            switch(phase)
            {
                case 0:
                    var player=UnityEngine.Object.FindFirstObjectByType<PlayerFPS>();
                    input=player.GetComponent<PlayerInput>();
                    cam=player.playerCamera.GetComponent<Camera>();
                    salsa=player.GetComponent<AudioSource>();
                    animator=player.GetComponentInChildren<Animator>();
                    var all=UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None);
                    if(all.Count(s=>s.playOnAwake && s.isPlaying)!=2) return;
                    Assert(!salsa.isPlaying,"Salsa plays on entry");
                    firstPosition=cam.transform.localPosition;
                    keyboard=InputSystem.AddDevice<Keyboard>();
                    input.SwitchCurrentControlScheme("Keyboard&Mouse",keyboard,Mouse.current);
                    Press(Key.F3);
                    Next(); break;
                case 1:
                    Assert((cam.cullingMask & LayerMask.GetMask("LocalPlayerHead"))!=0 && Vector3.Distance(firstPosition,cam.transform.localPosition)>.45f,"F3 did not pull back camera and show the head");
                    File.AppendAllText(Dir+"report.txt","F3 third-person local position "+cam.transform.localPosition+"\n");
                    Press(Key.G); Next(); break;
                case 2:
                    if(!salsa.isPlaying || !animator.GetCurrentAnimatorStateInfo(0).IsName("Dance")) return;
                    Press(); Next(); break;
                case 3:
                    var state=animator.GetCurrentAnimatorStateInfo(0);
                    File.AppendAllText(Dir+"trace.txt",$"{Time.time} state={state.shortNameHash} norm={state.normalizedTime} loop={state.loop} active={animator.GetBool("Dance")} playing={salsa.isPlaying} cursor={salsa.time} grounded={animator.GetBool("Grounded")}\n");
                    if (Now-change<9 && state.normalizedTime<=1f) return;
                    Assert(salsa.isPlaying && animator.GetCurrentAnimatorStateInfo(0).IsName("Dance") && animator.GetCurrentAnimatorStateInfo(0).normalizedTime>1,"Dance must continue beyond its first loop");
                    Press(Key.G); Next(); break;
                case 4:
                    if(salsa.isPlaying) return;
                    Press(Key.F3); Next(); break;
                case 5:
                    Assert((firstPosition-cam.transform.localPosition).sqrMagnitude<.001f && (cam.cullingMask & LayerMask.GetMask("LocalPlayerHead"))==0,"F3 did not restore first person");
                    Press(Key.G); Next(); break;
                case 6:
                    if(!salsa.isPlaying) return;
                    Press(Key.W); Next(); break;
                case 7:
                    if(salsa.isPlaying) return;
                    End("PASS: awake ambience, spatial electricity, dance music, dance loop, G stop, movement interruption and F3 round trip."); break;
            }
        } catch(Exception ex) { End(ex.ToString()); }
    }
}
