using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

namespace SpiritMovers.Prototypes.ThaiText.Editor
{
    /// <summary>
    /// Throwaway M2 prototype (PROJECT_PLAN M2 item 5): renders Thai text with TextMeshPro and the Mali font
    /// to a PNG so vowel/tone-mark placement and line wrapping can be inspected.
    /// Batch mode (needs graphics, so no -nographics):
    ///   1) -executeMethod SpiritMovers.Prototypes.ThaiText.Editor.ThaiTextProto.ImportTmpEssentials
    ///   2) -executeMethod SpiritMovers.Prototypes.ThaiText.Editor.ThaiTextProto.RenderTest
    /// </summary>
    public static class ThaiTextProto
    {
        private const string FontFolder = "Assets/_Game/Fonts/Mali";
        private const string OutputPng = "docs/screenshots/m2/thai-text-test.png";
        private const char Zwsp = '​';

        private static readonly string[] WrapWords =
            { "จัด", "บ้าน", "คืน", "ให้", "ผี", "ก่อน", "ที่", "ผี", "จะ", "ไล่", "คุณ", "ออก", "จาก", "บ้าน", "เพราะ", "ความ", "โกรธ" };

        [MenuItem("Spirit Movers/Prototypes/Thai Text: Import TMP Essentials")]
        public static void ImportTmpEssentials()
        {
            var package = Path.GetFullPath("Packages/com.unity.ugui/Package Resources/TMP Essential Resources.unitypackage");
            AssetDatabase.ImportPackage(package, false);
            Debug.Log($"[ThaiText] Imported {package}");
        }

        [MenuItem("Spirit Movers/Prototypes/Thai Text: Render Test")]
        public static void RenderTest()
        {
            var regular = GetOrCreateFontAsset("Mali-Regular");
            var bold = GetOrCreateFontAsset("Mali-Bold");

            var cameraObject = new GameObject("ThaiTextCamera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.95f, 0.91f, 0.82f);
            var target = new RenderTexture(1600, 900, 24);
            camera.targetTexture = target;

            var canvasObject = new GameObject("Canvas");
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 5f;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);

            AddText(canvas, bold, 54, new Rect(40, 30, 1520, 80), "ทดสอบภาษาไทย — Spirit Movers (Mali Bold)");
            AddText(canvas, regular, 46, new Rect(40, 120, 1520, 140),
                "สระ/วรรณยุกต์: ปั้น ผู้ใหญ่ กี่ ญี่ปุ่น ฤๅษี ปี๊บ ฟุ้ง น้ำ ฝั่ง ฎีกา ฐาน ปู่ ตั๋ว ปู้ ญุ่น ชั้น ปิ่น");
            AddText(canvas, regular, 40, new Rect(40, 280, 700, 260),
                "ไม่มีช่องว่าง: " + string.Concat(WrapWords));
            AddText(canvas, regular, 40, new Rect(820, 280, 700, 260),
                "แทรก ZWSP: " + string.Join(Zwsp.ToString(), WrapWords));
            AddText(canvas, bold, 40, new Rect(40, 580, 1520, 80), "ค่าจ้าง 1,250 บาท · ห้องที่ผีรับ 4/5 · Rest In Place");
            AddText(canvas, regular, 36, new Rect(40, 680, 1520, 160),
                "English check: The quick brown fox jumps over the lazy dog. 0123456789");

            Canvas.ForceUpdateCanvases();
            foreach (var text in canvasObject.GetComponentsInChildren<TextMeshProUGUI>())
                text.ForceMeshUpdate(true, true);
            camera.Render();

            RenderTexture.active = target;
            var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            image.Apply();
            RenderTexture.active = null;

            Directory.CreateDirectory(Path.GetDirectoryName(OutputPng));
            File.WriteAllBytes(OutputPng, image.EncodeToPNG());
            Debug.Log($"[ThaiText] Wrote {Path.GetFullPath(OutputPng)} " +
                      $"(markToBase={regular.fontFeatureTable.MarkToBaseAdjustmentRecords.Count}, markToMark={regular.fontFeatureTable.MarkToMarkAdjustmentRecords.Count})");

            Object.DestroyImmediate(canvasObject);
            Object.DestroyImmediate(cameraObject);
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(image);
        }

        private const string CompareLine = "กี่ ญี่ปุ่น ปู่ ฝั่ง ปิ่น ที่ ปี๊บ ตั๋ว | ผู้ใหญ่ ไม่ ปั้น น้ำ";

        /// <summary>Same tricky words in several fonts / feature settings to locate the tone-mark problem.</summary>
        [MenuItem("Spirit Movers/Prototypes/Thai Text: Render Font Comparison")]
        public static void RenderComparison()
        {
            var rows = new (string label, TMP_FontAsset font)[]
            {
                ("Mali, font features ON", CreateInMemory($"{FontFolder}/Mali-Regular.ttf", true)),
                ("Mali, font features OFF", CreateInMemory($"{FontFolder}/Mali-Regular.ttf", false)),
                ("Kanit, font features ON", CreateInMemory("Assets/_Game/Prototypes/ThaiText/TestFonts/Kanit-Regular.ttf", true)),
                ("Sarabun, font features ON", CreateInMemory("Assets/_Game/Prototypes/ThaiText/TestFonts/Sarabun-Regular.ttf", true)),
            };

            RenderRows(rows, "docs/screenshots/m2/thai-font-comparison.png");
        }

        /// <summary>Logs TMP's per-character layout for a few words to see where the tone mark goes.</summary>
        [MenuItem("Spirit Movers/Prototypes/Thai Text: Dump Layout")]
        public static void DumpLayout()
        {
            var font = GetOrCreateFontAsset("Mali-Regular");
            var go = new GameObject("Probe", typeof(RectTransform));
            var canvas = new GameObject("Canvas").AddComponent<Canvas>();
            go.transform.SetParent(canvas.transform, false);
            var text = go.AddComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSize = 64;

            foreach (var word in new[] { "ไม่", "กี่", "ปู่", "ปู้", "ที่" })
            {
                text.text = word;
                text.ForceMeshUpdate(true, true);
                var info = text.textInfo;
                var line = new System.Text.StringBuilder($"[ThaiText] '{word}':");
                for (var i = 0; i < info.characterCount; i++)
                {
                    var c = info.characterInfo[i];
                    line.Append($" U+{(int)c.character:X4}(glyph={c.textElement?.glyph?.index} visible={c.isVisible} " +
                                $"bl=({c.bottomLeft.x:F1},{c.bottomLeft.y:F1}) tr=({c.topRight.x:F1},{c.topRight.y:F1}))");
                }
                Debug.Log(line.ToString());
            }

            var table = font.fontFeatureTable;
            Debug.Log($"[ThaiText] features: markToBase={table.MarkToBaseAdjustmentRecords.Count} markToMark={table.MarkToMarkAdjustmentRecords.Count} " +
                      $"ligatures={table.ligatureRecords.Count} glyphPairs={table.glyphPairAdjustmentRecords.Count}");
            Object.DestroyImmediate(canvas.gameObject);
        }

        /// <summary>Builds a tiny player that renders Thai text with UI Toolkit's Standard and Advanced generators.</summary>
        [MenuItem("Spirit Movers/Prototypes/Thai Text: Build UI Toolkit Probe")]
        public static void BuildUitkProbe()
        {
            const string root = "Assets/_Game/Prototypes/ThaiText";
            EnableAdvancedTextGenerator();

            var panel = AssetDatabase.LoadAssetAtPath<UnityEngine.UIElements.PanelSettings>($"{root}/ThaiProbePanel.asset");
            if (panel == null)
            {
                panel = ScriptableObject.CreateInstance<UnityEngine.UIElements.PanelSettings>();
                AssetDatabase.CreateAsset(panel, $"{root}/ThaiProbePanel.asset");
            }

            var scene = UnityEditor.SceneManagement.EditorSceneManager.NewScene(
                UnityEditor.SceneManagement.NewSceneSetup.DefaultGameObjects, UnityEditor.SceneManagement.NewSceneMode.Single);
            var probe = new GameObject("ThaiTextUitkProbe").AddComponent<ThaiTextUitkProbe>();
            var serialized = new SerializedObject(probe);
            serialized.FindProperty("m_Regular").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Font>($"{FontFolder}/Mali-Regular.ttf");
            serialized.FindProperty("m_Bold").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Font>($"{FontFolder}/Mali-Bold.ttf");
            serialized.FindProperty("m_Panel").objectReferenceValue = panel;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var scenePath = $"{root}/Proto_ThaiUitk.unity";
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene, scenePath);

            var summary = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { scenePath },
                locationPathName = "Builds/Proto-ThaiUitk/ThaiProbe.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development,
            }).summary;
            Debug.Log($"[ThaiText] UITK probe build result={summary.result} errors={summary.totalErrors}");
            if (summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded && Application.isBatchMode)
                EditorApplication.Exit(1);
        }

        /// <summary>Project Settings > UI Toolkit > Enable Advanced Text Generator (internal API, set via reflection).</summary>
        private static void EnableAdvancedTextGenerator()
        {
            foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType("UnityEditor.UIElements.UIToolkitProjectSettings");
                var property = type?.GetProperty("enableAdvancedText",
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                if (property == null)
                    continue;
                property.SetValue(null, true);
                Debug.Log($"[ThaiText] enableAdvancedText = {property.GetValue(null)}");
                return;
            }
            Debug.LogWarning("[ThaiText] UIToolkitProjectSettings.enableAdvancedText not found");
        }

        private static TMP_FontAsset CreateInMemory(string ttfPath, bool fontFeatures)
        {
            var font = AssetDatabase.LoadAssetAtPath<Font>(ttfPath);
            var asset = TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024);
            asset.getFontFeatures = fontFeatures;
            return asset;
        }

        private static void RenderRows((string label, TMP_FontAsset font)[] rows, string outputPng)
        {
            var labelFont = GetOrCreateFontAsset("Mali-Regular");
            var cameraObject = new GameObject("ThaiTextCamera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.95f, 0.91f, 0.82f);
            var target = new RenderTexture(1600, 900, 24);
            camera.targetTexture = target;

            var canvasObject = new GameObject("Canvas");
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 5f;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);

            for (var i = 0; i < rows.Length; i++)
            {
                AddText(canvas, labelFont, 26, new Rect(40, 30 + i * 215, 1520, 40), rows[i].label);
                AddText(canvas, rows[i].font, 64, new Rect(40, 70 + i * 215, 1520, 140), CompareLine);
            }

            Canvas.ForceUpdateCanvases();
            foreach (var text in canvasObject.GetComponentsInChildren<TextMeshProUGUI>())
                text.ForceMeshUpdate(true, true);
            camera.Render();

            RenderTexture.active = target;
            var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            image.Apply();
            RenderTexture.active = null;

            Directory.CreateDirectory(Path.GetDirectoryName(outputPng));
            File.WriteAllBytes(outputPng, image.EncodeToPNG());
            Debug.Log($"[ThaiText] Wrote {Path.GetFullPath(outputPng)}");

            Object.DestroyImmediate(canvasObject);
            Object.DestroyImmediate(cameraObject);
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(image);
        }

        private static TMP_FontAsset GetOrCreateFontAsset(string fontName)
        {
            var assetPath = $"{FontFolder}/{fontName} SDF.asset";
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
            if (existing != null)
                return existing;

            var font = AssetDatabase.LoadAssetAtPath<Font>($"{FontFolder}/{fontName}.ttf");
            var asset = TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024);
            asset.name = $"{fontName} SDF";
            AssetDatabase.CreateAsset(asset, assetPath);
            asset.atlasTexture.name = $"{fontName} Atlas";
            asset.material.name = $"{fontName} Material";
            AssetDatabase.AddObjectToAsset(asset.atlasTexture, asset);
            AssetDatabase.AddObjectToAsset(asset.material, asset);
            AssetDatabase.SaveAssets();
            return asset;
        }

        private static void AddText(Canvas canvas, TMP_FontAsset font, float size, Rect rect, string content)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(canvas.transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(rect.x, -rect.y);
            rt.sizeDelta = new Vector2(rect.width, rect.height);

            var text = go.AddComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSize = size;
            text.color = new Color(0.11f, 0.08f, 0.13f);
            text.textWrappingMode = TextWrappingModes.Normal;
            text.text = content;
        }
    }
}
