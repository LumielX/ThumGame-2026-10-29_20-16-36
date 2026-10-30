using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UIElements;

namespace SpiritMovers.Prototypes.ThaiText
{
    /// <summary>
    /// Throwaway M2 prototype: renders Thai text with UI Toolkit using the Standard and the Advanced
    /// (HarfBuzz/ICU) text generators into a PNG, then quits. Output path: -probe-out PATH.
    /// </summary>
    public sealed class ThaiTextUitkProbe : MonoBehaviour
    {
        [SerializeField] private Font m_Regular;
        [SerializeField] private Font m_Bold;
        [SerializeField] private PanelSettings m_Panel;

        private const string Marks = "กี่ ญี่ปุ่น ปู่ ฝั่ง ปิ่น ที่ ปี๊บ ตั๋ว ผู้ใหญ่ ไม่ ปั้น น้ำ ฤๅษี";
        private const string NoSpaces = "จัดบ้านคืนให้ผีก่อนที่ผีจะไล่คุณออกจากบ้านเพราะความโกรธ";

        private IEnumerator Start()
        {
            var target = new RenderTexture(1600, 900, 24);
            if (m_Panel == null)
                m_Panel = ScriptableObject.CreateInstance<PanelSettings>();
            m_Panel.targetTexture = target;
            m_Panel.clearColor = true;
            m_Panel.colorClearValue = new Color(0.95f, 0.91f, 0.82f);

            var document = gameObject.AddComponent<UIDocument>();
            document.panelSettings = m_Panel;
            var root = document.rootVisualElement;
            root.style.paddingLeft = 40;
            root.style.paddingTop = 24;

            AddRow(root, "Standard generator", TextGeneratorType.Standard);
            AddRow(root, "Advanced generator (HarfBuzz/ICU)", TextGeneratorType.Advanced);

            for (var i = 0; i < 20; i++)
                yield return null;
            yield return new WaitForEndOfFrame();

            RenderTexture.active = target;
            var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            image.Apply();
            RenderTexture.active = null;

            var output = Arg("-probe-out") ?? "thai-uitk.png";
            File.WriteAllBytes(output, image.EncodeToPNG());
            Debug.Log($"[ThaiText] UITK probe wrote {Path.GetFullPath(output)}");
            Application.Quit();
        }

        private void AddRow(VisualElement root, string title, TextGeneratorType generator)
        {
            root.Add(MakeLabel(title, m_Bold, 26, generator, 1520));
            root.Add(MakeLabel(Marks, m_Regular, 54, generator, 1520));
            root.Add(MakeLabel("ไม่มีช่องว่าง: " + NoSpaces, m_Regular, 40, generator, 640));
        }

        private static Label MakeLabel(string text, Font font, float size, TextGeneratorType generator, float width)
        {
            var label = new Label(text);
            // The Advanced generator supports only dynamic (TextCore) font assets, not legacy Font definitions.
            label.style.unityFontDefinition = FontDefinition.FromSDFFont(UnityEngine.TextCore.Text.FontAsset.CreateFontAsset(font));
            label.style.fontSize = size;
            label.style.color = new Color(0.11f, 0.08f, 0.13f);
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.width = width;
            label.style.marginBottom = 10;
            label.style.unityTextGenerator = generator;
            return label;
        }

        private static string Arg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }
    }
}
