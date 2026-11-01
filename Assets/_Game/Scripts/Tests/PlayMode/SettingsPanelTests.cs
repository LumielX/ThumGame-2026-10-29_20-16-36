using System.Collections;
using System.IO;
using NUnit.Framework;
using SpiritMovers.Core.Save;
using SpiritMovers.Core.Text;
using SpiritMovers.Player;
using SpiritMovers.Save;
using SpiritMovers.Session;
using SpiritMovers.Settings;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace SpiritMovers.Tests
{
    public class SettingsPanelTests : GameFlowFixture
    {
        private Keyboard m_Keyboard;
        private Mouse m_Mouse;
        private InputSettings.BackgroundBehavior m_Background;
        private InputSettings.EditorInputBehaviorInPlayMode m_EditorInput;

        private static SettingsPanel Panel => Object.FindAnyObjectByType<SettingsPanel>();
        private static InputAction Action(string path) => SettingsApplier.Actions.FindAction(path, true);
        private string SettingsFile => Path.Combine(DataDir, "settings.json");

        [SetUp]
        public void AddDevices()
        {
            // A batch-mode editor has no focused Game view, and by default keyboard state only updates while it has
            // focus: these tests let input through without focus (restored afterwards) and use virtual devices.
            var settings = InputSystem.settings;
            m_Background = settings.backgroundBehavior;
            m_EditorInput = settings.editorInputBehaviorInPlayMode;
            settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            m_Keyboard = InputSystem.AddDevice<Keyboard>();
            m_Mouse = InputSystem.AddDevice<Mouse>();
            m_Keyboard.MakeCurrent();
            m_Mouse.MakeCurrent();
        }

        [TearDown]
        public void RemoveDevices()
        {
            InputSystem.RemoveDevice(m_Keyboard);
            InputSystem.RemoveDevice(m_Mouse);
            InputSystem.settings.backgroundBehavior = m_Background;
            InputSystem.settings.editorInputBehaviorInPlayMode = m_EditorInput;
        }

        /// <summary>Press and release a key through the frame's own input update, as a real key press arrives.</summary>
        private IEnumerator Tap(Key key)
        {
            InputSystem.QueueStateEvent(m_Keyboard, new KeyboardState(key));
            yield return null;
            InputSystem.QueueStateEvent(m_Keyboard, new KeyboardState());
            yield return null;
            // The rebinding waits 0.1 s for a better match before it settles.
            yield return new WaitForSeconds(0.25f);
            InputSystem.Update();
            yield return null;
        }

        private IEnumerator OpenFromMenu()
        {
            Object.FindAnyObjectByType<MainMenuPanel>().ClickSettings();
            yield return null;
            Assert.IsTrue(Panel.IsOpen);
        }

        [UnityTest]
        public IEnumerator Open_FromMenuAndPause()
        {
            yield return OpenFromMenu();
            Panel.Close();
            Assert.IsFalse(Panel.IsOpen);

            yield return SoloIntoLobby();
            yield return Press(i => i.Next.PausePressed = true);
            var pause = Object.FindAnyObjectByType<PauseMenu>();
            Assert.IsTrue(pause.IsOpen);
            pause.ClickSettings();
            yield return null;
            Assert.IsTrue(Panel.IsOpen);
            yield return Tap(Key.Escape);
            Assert.IsFalse(Panel.IsOpen, "Esc closes the settings");
            Assert.IsTrue(pause.IsOpen, "back on the pause menu");
            Assert.IsFalse(PlayerMotor.Local.InputEnabled, "no walking until the pause menu closes too");
        }

        [UnityTest]
        public IEnumerator Changes_ApplyNowAndSaveOnClose()
        {
            yield return OpenFromMenu();
            Panel.SetVolume(AudioGroup.Master, 0.5f);
            Assert.AreEqual(0.5f, AudioListener.volume, 0.001f, "heard at once");
            Assert.IsFalse(File.Exists(SettingsFile) && File.ReadAllText(SettingsFile).Contains("\"master\": 0.5"), "not saved yet");
            Panel.Close();
            Assert.AreEqual(0.5f, JsonUtility.FromJson<SettingsData>(File.ReadAllText(SettingsFile)).master, 0.001f);
        }

        [UnityTest]
        public IEnumerator Language_RebuildsPanelText()
        {
            yield return OpenFromMenu();
            Panel.SetLanguage("en");
            yield return WaitFor(() => Panel.LastText.Contains("Settings"), 3f, "English panel");
            Panel.SetLanguage("th");
            yield return WaitFor(() => ThaiShaper.Unshape(Panel.LastText).Contains("ตั้งค่า"), 3f, "Thai panel");
        }

        [UnityTest]
        public IEnumerator Rebind_PressedKeyBecomesBinding()
        {
            yield return OpenFromMenu();
            Panel.ShowTab(2);
            var row = Panel.Row("Gameplay/Ready");
            row.StartRebind();
            Assert.IsTrue(row.Waiting);
            yield return Tap(Key.T);
            Assert.IsFalse(row.Waiting);
            Assert.AreEqual("<Keyboard>/t", Action("Gameplay/Ready").bindings[0].effectivePath);
            StringAssert.Contains("T", row.KeyText);
        }

        [UnityTest]
        public IEnumerator Rebind_EscapeCancels()
        {
            yield return OpenFromMenu();
            Panel.ShowTab(2);
            var row = Panel.Row("Gameplay/Ready");
            row.StartRebind();
            yield return Tap(Key.Escape);
            Assert.IsFalse(row.Waiting);
            Assert.AreEqual("<Keyboard>/r", Action("Gameplay/Ready").bindings[0].effectivePath);
            Assert.IsTrue(Panel.IsOpen, "Esc only cancelled the key change");
        }

        [UnityTest]
        public IEnumerator Rebind_MouseMoveIgnored()
        {
            yield return OpenFromMenu();
            Panel.ShowTab(2);
            var row = Panel.Row("Gameplay/Ready");
            row.StartRebind();
            InputSystem.QueueStateEvent(m_Mouse, new MouseState { delta = new Vector2(30f, -12f), position = new Vector2(200f, 200f) });
            InputSystem.Update();
            yield return new WaitForSeconds(0.25f);
            InputSystem.Update();
            Assert.IsTrue(row.Waiting, "moving the mouse is not a key");
            yield return Tap(Key.T);
            Assert.AreEqual("<Keyboard>/t", Action("Gameplay/Ready").bindings[0].effectivePath);
        }

        [UnityTest]
        public IEnumerator Rebind_ConflictIsShownOnBothRows()
        {
            yield return OpenFromMenu();
            Panel.ShowTab(2);
            Panel.Row("Gameplay/Ready").StartRebind();
            yield return Tap(Key.Space);
            Assert.IsTrue(Panel.Row("Gameplay/Ready").Conflicted);
            Assert.IsTrue(Panel.Row("Gameplay/Jump").Conflicted);
            Assert.IsFalse(Panel.Row("Gameplay/Throw").Conflicted, "Throw and Use share the right button on purpose");
            StringAssert.Contains("ซ้ำกับ", ThaiShaper.Unshape(Panel.LastText));
        }

        [UnityTest]
        public IEnumerator ResetBindings_RestoresDefaults()
        {
            yield return OpenFromMenu();
            Panel.ShowTab(2);
            Panel.Row("Gameplay/Ready").StartRebind();
            yield return Tap(Key.T);
            Panel.Row("Gameplay/Move", "up").StartRebind();
            yield return Tap(Key.I);
            Assert.AreEqual("<Keyboard>/i", Panel.Row("Gameplay/Move", "up").Binding.effectivePath);
            Panel.ResetBindings();
            Assert.AreEqual("<Keyboard>/r", Action("Gameplay/Ready").bindings[0].effectivePath);
            Assert.AreEqual("<Keyboard>/w", Panel.Row("Gameplay/Move", "up").Binding.effectivePath);
            Panel.Close();
            Assert.AreEqual("", JsonUtility.FromJson<SettingsData>(File.ReadAllText(SettingsFile)).bindingOverrides);
        }

        [UnityTest]
        public IEnumerator Rebind_SurvivesRestart()
        {
            yield return OpenFromMenu();
            Panel.ShowTab(2);
            Panel.Row("Gameplay/Ready").StartRebind();
            yield return Tap(Key.T);
            Panel.Close();
            SettingsApplier.Actions.RemoveAllBindingOverrides();
            GameData.Load();
            SettingsApplier.ApplyAll();
            Assert.AreEqual("<Keyboard>/t", Action("Gameplay/Ready").bindings[0].effectivePath);
        }
    }
}
