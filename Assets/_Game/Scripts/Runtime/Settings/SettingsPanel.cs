using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using SpiritMovers.Core.Save;
using SpiritMovers.Job;
using SpiritMovers.Player;
using SpiritMovers.Save;
using SpiritMovers.Session;
using SpiritMovers.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SpiritMovers.Settings
{
    /// <summary>
    /// The settings page (GDD 10/16/18/19), opened from the main menu or the Esc menu: General (language, mouse, field
    /// of view), Audio (six volumes, push-to-talk, the ghost hearing my mic) and Keys (rebinding). Changes apply at
    /// once and are saved when the page closes.
    /// </summary>
    public sealed class SettingsPanel : MonoBehaviour
    {
        /// <summary>The keys a player may change (M4B plan): the first binding of each, Move by its four parts.</summary>
        private static readonly (string Path, string Part, string Label)[] s_Keys =
        {
            ("Gameplay/Move", "up", "action.move_up"),
            ("Gameplay/Move", "down", "action.move_down"),
            ("Gameplay/Move", "left", "action.move_left"),
            ("Gameplay/Move", "right", "action.move_right"),
            ("Gameplay/Sprint", null, "action.sprint"),
            ("Gameplay/Crouch", null, "action.crouch"),
            ("Gameplay/Jump", null, "action.jump"),
            ("Gameplay/Interact", null, "action.interact"),
            ("Gameplay/Throw", null, "action.throw"),
            ("Gameplay/JobSheet", null, "action.jobsheet"),
            ("Gameplay/Wai", null, "action.wai"),
            ("Gameplay/Use", null, "action.use"),
            ("Gameplay/Ready", null, "action.ready"),
            ("Gameplay/Confirm", null, "action.confirm"),
            ("Voice/PushToTalk", null, "action.push_to_talk"),
            ("Gameplay/Chat", null, "action.chat"),
            ("Gameplay/Ping", null, "action.ping"),
            ("Gameplay/QuickWheel", null, "action.quick_wheel"),
        };

        [SerializeField] private TMP_FontAsset m_Font;
        [SerializeField] private ThaiGlyphMap m_GlyphMap;

        private readonly List<RebindRow> m_Rows = new();
        private readonly GameObject[] m_Tabs = new GameObject[3];
        private Transform m_Canvas;
        private GameObject m_Root;
        private int m_Tab;
        private int m_ClosedFrame = -1;
        private int m_IgnoreEscUntil = -1;

        public bool IsOpen { get; private set; }
        /// <summary>The Esc that closed the page must not also close the pause menu under it.</summary>
        public bool ClosedThisFrame => m_ClosedFrame == Time.frameCount;

        /// <summary>Every visible text on the page (shaped), for tests.</summary>
        internal string LastText => m_Canvas == null ? ""
            : string.Join("\n", m_Canvas.GetComponentsInChildren<TMP_Text>(false).Select(t => t.text));

        private void Start()
        {
            ThaiFontSupport.Register(m_Font, m_GlyphMap);
            SettingsApplier.LanguageChanged += Relabel;
        }

        private void OnDestroy() => SettingsApplier.LanguageChanged -= Relabel;

        public void Open()
        {
            IsOpen = true;
            Build();
            GameCursor.Capture(false);
        }

        /// <summary>Save what changed and go back to the menu that opened the page.</summary>
        public void Close()
        {
            if (!IsOpen)
                return;
            IsOpen = false;
            m_ClosedFrame = Time.frameCount;
            if (m_Canvas != null)
                Destroy(m_Canvas.gameObject);
            m_Canvas = null;
            m_Rows.Clear();
            if (SettingsApplier.Actions != null)
                GameData.Settings.bindingOverrides = SettingsApplier.CaptureBindings(SettingsApplier.Actions);
            GameData.SaveSettingsNow();
            var local = PlayerMotor.Local;
            GameCursor.Capture(local != null && local.InputEnabled);
        }

        private void Update()
        {
            if (!IsOpen || m_Rows.Any(r => r.Waiting) || Time.frameCount <= m_IgnoreEscUntil)
                return;
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                Close();
        }

        internal void ShowTab(int tab)
        {
            m_Tab = tab;
            for (var i = 0; i < m_Tabs.Length; i++)
                if (m_Tabs[i] != null)
                    m_Tabs[i].SetActive(i == tab);
        }

        internal RebindRow Row(string actionPath, string part = null) =>
            m_Rows.First(r => r.ActionPath == actionPath && r.Part == part);

        internal void SetLanguage(string code)
        {
            GameData.Settings.language = code;
            SettingsApplier.ApplyLanguage();
        }

        internal void SetVolume(AudioGroup group, float value)
        {
            GameData.Settings.SetVolume(group, value);
            SettingsApplier.ApplyAudio();
        }

        internal void SetSensitivity(float value) =>
            GameData.Settings.mouseSensitivity = Mathf.Clamp(value, SettingsData.MinSensitivity, SettingsData.MaxSensitivity);

        internal void SetInvertY(bool on) => GameData.Settings.invertY = on;

        internal void SetFov(float value) => GameData.Settings.fov = Mathf.Clamp(value, SettingsData.MinFov, SettingsData.MaxFov);

        internal void SetPushToTalk(bool on) => GameData.Settings.pushToTalk = on;

        internal void SetGhostHearsMic(bool on) => GameData.Settings.ghostHearsMic = on;

        internal void ResetBindings()
        {
            SettingsApplier.Actions?.RemoveAllBindingOverrides();
            RefreshRows();
        }

        private void Relabel()
        {
            if (IsOpen)
                Build();
        }

        private void Build()
        {
            if (m_Canvas != null)
                Destroy(m_Canvas.gameObject);
            m_Rows.Clear();
            m_Canvas = SessionUi.CreateCanvas(transform, "SettingsCanvas");
            m_Canvas.GetComponent<Canvas>().sortingOrder = 60;
            // Dim whatever menu is underneath: the page reads as its own screen.
            var backdrop = new GameObject("Backdrop", typeof(RectTransform), typeof(Image));
            backdrop.transform.SetParent(m_Canvas, false);
            backdrop.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.7f);
            var backdropRect = backdrop.GetComponent<RectTransform>();
            backdropRect.anchorMin = Vector2.zero;
            backdropRect.anchorMax = Vector2.one;
            backdropRect.offsetMin = backdropRect.offsetMax = Vector2.zero;
            var column = SessionUi.CreateColumn(m_Canvas, new Vector2(0.5f, 0.5f), new Vector2(1000, 1000));
            column.GetComponent<VerticalLayoutGroup>().spacing = 10;
            column.GetComponent<Image>().color = new Color(0.12f, 0.09f, 0.07f, 1f);
            m_Root = column.gameObject;

            var title = SessionUi.CreateLabel(column, m_Font, 50, 36);
            title.text = SessionUi.Text("settings.title");
            var tabs = SessionUi.CreateRow(column, 56);
            var keys = new[] { "settings.tab_general", "settings.tab_audio", "settings.tab_keys" };
            for (var i = 0; i < keys.Length; i++)
            {
                var index = i;
                SessionUi.CreateButton(tabs, m_Font, keys[i], () => ShowTab(index));
            }

            var settings = GameData.Settings;
            var general = Tab(column, 0);
            var language = SessionUi.CreateRow(general, 48);
            SessionUi.CreateLabel(language, m_Font, 48, 26).text = SessionUi.Text("settings.language");
            SessionUi.CreateButton(language, m_Font, "settings.lang_th", () => SetLanguage("th"));
            SessionUi.CreateButton(language, m_Font, "settings.lang_en", () => SetLanguage("en"));
            Slider(general, "settings.sensitivity", SettingsData.MinSensitivity, SettingsData.MaxSensitivity,
                settings.mouseSensitivity, SetSensitivity, v => v.ToString("0.0", CultureInfo.InvariantCulture));
            Toggle(general, "settings.invert_y", settings.invertY, SetInvertY);
            Slider(general, "settings.fov", SettingsData.MinFov, SettingsData.MaxFov, settings.fov, SetFov, v => Mathf.RoundToInt(v).ToString());

            var audio = Tab(column, 1);
            foreach (AudioGroup group in Enum.GetValues(typeof(AudioGroup)))
                Slider(audio, $"settings.vol_{group.ToString().ToLowerInvariant()}", 0f, 1f, settings.Volume(group),
                    v => SetVolume(group, v), v => Mathf.RoundToInt(v * 100f).ToString());
            Toggle(audio, "settings.push_to_talk", settings.pushToTalk, SetPushToTalk);
            Toggle(audio, "settings.ghost_hears_mic", settings.ghostHearsMic, SetGhostHearsMic);
            Device(audio, "settings.mic", () => VoiceDevices(input: true), () => GameData.Settings.micDevice,
                name => GameData.Settings.micDevice = name);
            Device(audio, "settings.speaker", () => VoiceDevices(input: false), () => GameData.Settings.speakerDevice,
                name => GameData.Settings.speakerDevice = name);
            var help = SessionUi.CreateLabel(audio, m_Font, 60, 20);
            help.text = SessionUi.Text("settings.mic_help");

            var keysTab = Tab(column, 2);
            keysTab.GetComponent<VerticalLayoutGroup>().spacing = 4;
            foreach (var (path, part, label) in s_Keys)
            {
                var action = SettingsApplier.Actions != null ? SettingsApplier.Actions.FindAction(path) : null;
                if (action == null)
                    continue;
                var row = new GameObject($"Row_{path}_{part}", typeof(RectTransform)).AddComponent<RebindRow>();
                row.transform.SetParent(keysTab, false);
                row.Init(action, path, part, label, m_Font, OnRowFinished);
                m_Rows.Add(row);
            }
            SessionUi.CreateButton(keysTab, m_Font, "settings.reset_keys", ResetBindings);
            RefreshRows();

            SessionUi.CreateButton(column, m_Font, "settings.close", Close);
            ShowTab(m_Tab);
        }

        private Transform Tab(Transform column, int index)
        {
            var tab = new GameObject($"Tab{index}", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
            tab.transform.SetParent(column, false);
            var layout = tab.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 10;
            layout.childForceExpandHeight = false;
            tab.GetComponent<LayoutElement>().flexibleHeight = 1;
            m_Tabs[index] = tab;
            return tab.transform;
        }

        private void Slider(Transform parent, string key, float min, float max, float value, Action<float> set, Func<float, string> show)
        {
            var row = SessionUi.CreateRow(parent, 48);
            var label = SessionUi.CreateLabel(row, m_Font, 48, 26);
            label.GetComponent<LayoutElement>().preferredWidth = 520;
            label.text = SessionUi.Text(key, show(value));
            SessionUi.CreateSlider(row, min, max, value, v =>
            {
                set(v);
                label.text = SessionUi.Text(key, show(v));
            });
        }

        /// <summary>A device line: its name (or "Windows default") and a button that steps to the next one.</summary>
        private void Device(Transform parent, string key, Func<IReadOnlyList<string>> devices, Func<string> get, Action<string> set)
        {
            var row = SessionUi.CreateRow(parent, 48);
            var label = SessionUi.CreateLabel(row, m_Font, 48, 24);
            label.GetComponent<LayoutElement>().preferredWidth = 620;
            void Show() => label.text = SessionUi.Text(key, string.IsNullOrEmpty(get()) ? JobUi.Text("settings.device_default") : get());
            Show();
            SessionUi.CreateButton(row, m_Font, "settings.change_key", () =>
            {
                // "" (the Windows default) first, then every device voice reports, round and round.
                var choices = new List<string> { "" };
                choices.AddRange(devices());
                set(choices[(choices.IndexOf(get()) + 1) % choices.Count]);
                Show();
                Voice.VoiceDirector.Instance?.ApplyDevices();
            });
        }

        /// <summary>The devices voice knows about (none until voice has connected once).</summary>
        private static IReadOnlyList<string> VoiceDevices(bool input)
        {
            var voice = Voice.VoiceDirector.Instance;
            if (voice == null || voice.State != Voice.VoiceState.On)
                return Array.Empty<string>();
            return input ? voice.Service.InputDevices : voice.Service.OutputDevices;
        }

        private void Toggle(Transform parent, string key, bool value, Action<bool> set)
        {
            var row = SessionUi.CreateRow(parent, 48);
            SessionUi.CreateToggle(row, value, v => set(v));
            var label = SessionUi.CreateLabel(row, m_Font, 48, 26);
            label.text = SessionUi.Text(key);
        }

        private void OnRowFinished(RebindRow row, bool changed)
        {
            // The Esc that cancelled a key change must not close the page in the same frame.
            m_IgnoreEscUntil = Time.frameCount + 1;
            RefreshRows();
        }

        /// <summary>
        /// Show each key and flag both actions when two share one (Throw/Use excepted, BindingRules). The keys that are
        /// not on the page take part too — Crouch also answers to C, Interact to the left button, Esc opens the menu.
        /// </summary>
        private void RefreshRows()
        {
            var labels = m_Rows.ToDictionary(r => r.ConflictName, r => r.LabelKey);
            labels["Pause"] = "action.pause";
            var keys = m_Rows.Select(r => (r.ConflictName, r.Binding.effectivePath)).ToList();
            keys.AddRange(HiddenKeys());
            var conflicts = BindingRules.Conflicts(keys);
            foreach (var row in m_Rows)
            {
                var other = conflicts.Where(c => c.A == row.ConflictName || c.B == row.ConflictName)
                    .Select(c => c.A == row.ConflictName ? c.B : c.A)
                    .FirstOrDefault();
                row.Refresh(other == null ? null : JobUi.Text(labels.TryGetValue(other, out var key) ? key : "action.pause"));
            }
        }

        /// <summary>Every keyboard or mouse-button binding the page does not show, by action name.</summary>
        private IEnumerable<(string, string)> HiddenKeys()
        {
            if (SettingsApplier.Actions == null)
                yield break;
            foreach (var action in SettingsApplier.Actions)
            {
                for (var i = 0; i < action.bindings.Count; i++)
                {
                    var binding = action.bindings[i];
                    var path = binding.effectivePath ?? "";
                    var shown = binding.isPartOfComposite || (i == 0 && m_Rows.Any(r => r.ActionPath == $"{action.actionMap.name}/{action.name}"));
                    var pressable = path.StartsWith("<Keyboard>/") || path.EndsWith("Button");
                    if (!binding.isComposite && !shown && pressable)
                        yield return (action.name, path);
                }
            }
        }
    }
}
