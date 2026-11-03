#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CyberCity.Editor
{
    public static class CyberCityInputVerification
    {
        static readonly List<string> cycleErrors = new List<string>();
        static InputSettings cycleSettings;
        static string cycleJson;
        static int completedCycles;
        static Action nextAction;
        static double nextActionTime;
        public static string Status { get; private set; } = "Not run";
        static void Require(bool value, string message)
        { if (!value) throw new InvalidOperationException("INPUT LIFECYCLE FAIL: " + message); }

        static void Invoke(string method)
        {
            typeof(CyberCityVerification).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
        }

        [MenuItem("Tools/CyberCity/Verify input settings lifecycle")]
        public static void Verify()
        {
            var original = InputSystem.settings;
            Require(original != null, "InputSettings must be alive before verification");
            string before = JsonUtility.ToJson(original);
            var keyboard = Keyboard.current;
            try
            {
                Invoke("ConfigureVerificationInput");
                Require(original != null, "Verification setup must not destroy Unity's default InputSettings");
                Require(ReferenceEquals(InputSystem.settings, original), "Verification keeps the same settings object");
                Require(InputSystem.settings.backgroundBehavior == InputSettings.BackgroundBehavior.IgnoreFocus,
                    "Verification temporarily accepts background input");
                Invoke("RestoreVerificationInput");
                Invoke("RestoreVerificationInput");
                Require(original != null && ReferenceEquals(InputSystem.settings, original), "Repeated cleanup leaves settings alive and active");
                Require(JsonUtility.ToJson(InputSystem.settings) == before, "Cleanup restores every original setting");
                Require(keyboard == null || !keyboard.added || Keyboard.current == keyboard, "Cleanup restores the active keyboard");
                Debug.Log("CyberCity PASS: input settings lifecycle");
            }
            finally
            {
                Invoke("RestoreVerificationInput");
                // Keep even a failing regression run from poisoning the next Play session.
                if (InputSystem.settings == null)
                {
                    var recovered = ScriptableObject.CreateInstance<InputSettings>();
                    recovered.hideFlags = HideFlags.HideAndDontSave;
                    JsonUtility.FromJsonOverwrite(before, recovered);
                    InputSystem.settings = recovered;
                }
            }
        }

        [MenuItem("Tools/CyberCity/Verify repeated Play Stop input")]
        public static void VerifyPlayStopCycles()
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Start repeated Play/Stop verification in Edit mode");
            Require(EditorSettings.enterPlayModeOptionsEnabled &&
                (EditorSettings.enterPlayModeOptions & EnterPlayModeOptions.DisableDomainReload) != 0,
                "This regression verifies the project's disabled Domain Reload configuration");
            Require(Status != "Running", "A cycle verification is already running");
            cycleSettings = InputSystem.settings; Require(cycleSettings != null, "Settings alive before first Play");
            cycleJson = JsonUtility.ToJson(cycleSettings); completedCycles = 0; cycleErrors.Clear(); Status = "Running";
            Application.logMessageReceived += CaptureCycleError;
            EditorApplication.playModeStateChanged += OnCycleState;
            EditorApplication.update += CycleTick;
            Schedule(() => EditorApplication.isPlaying = true);
        }

        static void Schedule(Action action)
        { nextAction = action; nextActionTime = EditorApplication.timeSinceStartup + .25; }

        static void CycleTick()
        {
            if (nextAction == null || EditorApplication.timeSinceStartup < nextActionTime) return;
            var action = nextAction; nextAction = null;
            try { action(); }
            catch (Exception e) { FinishCycles(e.Message); }
        }

        static void OnCycleState(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
                Schedule(() => {
                    Require(InputSystem.settings != null, "Settings alive after RuntimeInitialize");
                    Require(UnityEngine.Object.FindAnyObjectByType<CyberCityGame>()?.UI?.CanvasRoot != null,
                        "Menu initializes on each Play entry");
                    Verify();
                    // The second cycle deliberately stops with verification input still active.
                    if (completedCycles == 1) Invoke("ConfigureVerificationInput");
                    EditorApplication.isPlaying = false;
                });
            else if (state == PlayModeStateChange.EnteredEditMode)
                Schedule(() => {
                    Require(cycleSettings != null && ReferenceEquals(InputSystem.settings, cycleSettings),
                        "Play/Stop preserves the original native settings object");
                    Require(JsonUtility.ToJson(InputSystem.settings) == cycleJson, "Play/Stop restores original input values");
                    completedCycles++;
                    if (completedCycles == 3) FinishCycles(null);
                    else Schedule(() => EditorApplication.isPlaying = true);
                });
        }

        static void CaptureCycleError(string message, string stack, LogType type)
        {
            if ((type == LogType.Assert || type == LogType.Error || type == LogType.Exception) &&
                (message.Contains("InputSettings") || message.Contains("Assertion failed") || stack.Contains("InputSystem")))
                cycleErrors.Add(message + "\n" + stack);
        }

        static void FinishCycles(string failure)
        {
            Application.logMessageReceived -= CaptureCycleError;
            EditorApplication.playModeStateChanged -= OnCycleState;
            EditorApplication.update -= CycleTick; nextAction = null;
            Invoke("RestoreVerificationInput");
            if (failure != null) cycleErrors.Add(failure);
            Status = cycleErrors.Count == 0 ? "PASS: 3 Play/Stop cycles; normal and interrupted input cleanup; zero input errors/assertions"
                : "FAIL: " + string.Join("\n", cycleErrors);
            Directory.CreateDirectory("Documentation");
            File.WriteAllText("Documentation/input-lifecycle-verification.txt", Status + "\n");
            if (cycleErrors.Count == 0) Debug.Log("CyberCity " + Status); else Debug.LogError(Status);
            if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
        }
    }
}
#endif
