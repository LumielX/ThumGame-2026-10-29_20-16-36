using System.Collections;
using System.Linq;
using NUnit.Framework;
using SpiritMovers.Core.Save;
using SpiritMovers.Core.Text;
using SpiritMovers.Job;
using SpiritMovers.Player;
using SpiritMovers.Save;
using SpiritMovers.Session;
using SpiritMovers.Settings;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace SpiritMovers.Tests
{
    public class SettingsApplyTests : GameFlowFixture
    {
        private static MainMenuPanel Menu => Object.FindAnyObjectByType<MainMenuPanel>();
        private static InputAction Ready => SettingsApplier.Actions.FindAction("Gameplay/Ready", true);

        [UnityTest]
        public IEnumerator Language_SwitchesTablesAndMenuText()
        {
            GameData.Settings.language = "en";
            SettingsApplier.ApplyLanguage();
            yield return WaitFor(() => Menu.ButtonTexts.Contains("Host a room"), 3f, "English menu");
            GameData.Settings.language = "th";
            SettingsApplier.ApplyLanguage();
            yield return WaitFor(() => ThaiShaper.Unshape(Menu.ButtonTexts).Contains("สร้างห้อง"), 3f, "Thai menu");
        }

        [UnityTest]
        public IEnumerator Look_UsesSensitivityAndInvertY()
        {
            yield return SoloIntoLobby();
            var motor = PlayerMotor.Local;

            float Turn(float sensitivity)
            {
                GameData.Settings.mouseSensitivity = sensitivity;
                var before = motor.transform.eulerAngles.y;
                LocalInput.Next.Look = new Vector2(10f, 0f);
                return before;
            }

            var start = Turn(1f);
            yield return null;
            yield return null;
            var once = Mathf.DeltaAngle(start, motor.transform.eulerAngles.y);
            start = Turn(2f);
            yield return null;
            yield return null;
            var twice = Mathf.DeltaAngle(start, motor.transform.eulerAngles.y);
            Assert.Greater(once, 0f);
            Assert.AreEqual(once * 2f, twice, 0.01f);

            GameData.Settings.mouseSensitivity = 1f;
            var pitch = motor.Pitch;
            LocalInput.Next.Look = new Vector2(0f, 10f);
            yield return null;
            yield return null;
            var normal = motor.Pitch - pitch;
            GameData.Settings.invertY = true;
            pitch = motor.Pitch;
            LocalInput.Next.Look = new Vector2(0f, 10f);
            yield return null;
            yield return null;
            Assert.AreEqual(-normal, motor.Pitch - pitch, 0.01f, "inverted");
            Assert.AreNotEqual(0f, normal);
        }

        [UnityTest]
        public IEnumerator Fov_AppliedToHeadCamera()
        {
            yield return SoloIntoLobby();
            GameData.Settings.fov = 90f;
            yield return null;
            Assert.AreEqual(90f, PlayerMotor.Local.Fov);
        }

        [UnityTest]
        public IEnumerator Master_SetsListenerVolume()
        {
            GameData.Settings.SetVolume(AudioGroup.Master, 0.25f);
            GameData.Settings.SetVolume(AudioGroup.Music, 0.5f);
            SettingsApplier.ApplyAudio();
            Assert.AreEqual(0.25f, AudioListener.volume, 0.001f);
            Assert.AreEqual(0.125f, GameAudio.Gain(AudioGroup.Music), 0.001f);
            Assert.AreEqual(0.25f, GameAudio.Gain(AudioGroup.Master), 0.001f);
            yield break;
        }

        [UnityTest]
        public IEnumerator Bindings_OverrideSurvivesReload()
        {
            Ready.ApplyBindingOverride(0, "<Keyboard>/t");
            GameData.Settings.bindingOverrides = SettingsApplier.CaptureBindings(SettingsApplier.Actions);
            GameData.SaveSettingsNow();
            SettingsApplier.Actions.RemoveAllBindingOverrides();
            Assert.AreEqual("<Keyboard>/r", Ready.bindings[0].effectivePath);
            GameData.Load();
            SettingsApplier.ApplyBindings(SettingsApplier.Actions);
            Assert.AreEqual("<Keyboard>/t", Ready.bindings[0].effectivePath);
            yield break;
        }

        [UnityTest]
        public IEnumerator Bindings_CorruptJsonFallsBackToDefaults()
        {
            Ready.ApplyBindingOverride(0, "<Keyboard>/y");
            GameData.Settings.bindingOverrides = "{oops";
            SettingsApplier.ApplyBindings(SettingsApplier.Actions);
            Assert.AreEqual("<Keyboard>/r", Ready.bindings[0].effectivePath);
            yield break;
        }

        [UnityTest]
        public IEnumerator Hints_ShowCurrentKey()
        {
            Ready.ApplyBindingOverride(0, "<Keyboard>/t");
            yield return SoloIntoLobby();
            var board = Object.FindAnyObjectByType<LobbyPanel>();
            yield return WaitFor(() => ThaiShaper.Unshape(board.LastText).Contains("กด T เมื่อพร้อม"), 3f, "the new key in the hint");
            Assert.AreEqual(JobUi.Text("key.mouse_right"), KeyNames.For("Gameplay/Use"));
            Assert.AreEqual(JobUi.Text("key.mouse_right"), KeyNames.ArgsFor("camera.use")[0]);
        }

        [UnityTest]
        public IEnumerator NumpadEnter_Confirms()
        {
            var confirm = SettingsApplier.Actions.FindAction("Gameplay/Confirm", true);
            Assert.IsTrue(confirm.bindings.Any(b => b.path == "<Keyboard>/numpadEnter"));
            Assert.AreEqual("<Keyboard>/v", SettingsApplier.Actions.FindAction("Voice/PushToTalk", true).bindings[0].path);
            yield break;
        }
    }
}
