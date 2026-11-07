using AncorRA.AR;
using NUnit.Framework;
using Unity.Collections;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.Rendering;
using System.IO;

namespace AncorRA.Tests
{
    public class PaseoOcclusionTests
    {
        [Test]
        public void RawFilterRespectsStridesConfidenceRangeAndMissingPixels()
        {
            // Two rows, three pixels, padded rows and confidence pixels. Depth millimetres, little endian.
            using var depth = new NativeArray<byte>(new byte[] {
                232, 3, 208, 7, 112, 23, 99, 99, // 1 m, 2 m, 6 m
                0, 0, 100, 0, 136, 19, 99, 99   // missing, 0.1 m, 5 m
            }, Allocator.Temp);
            using var confidence = new NativeArray<byte>(new byte[] {
                255, 99, 127, 99, 255, 99, 99, 99,
                255, 99, 255, 99, 128, 99, 99, 99
            }, Allocator.Temp);
            var output = new float[6];
            PaseoOcclusion.Filter(depth, 8, 2, confidence, 8, 2, 3, 2, 5f, output);
            CollectionAssert.AreEqual(new[] { 1f, 0f, 0f, 0f, 0f, 5f }, output);
            PaseoOcclusion.Filter(depth, 8, 2, confidence, 8, 2, 3, 2, 8f, output);
            Assert.AreEqual(6f, output[2], 1e-5f);
            Assert.AreEqual(0f, output[1], "uncertain pixels stay empty even at a larger range");
        }

        [Test]
        public void SheetExposesModesRangeAndKeepsNormalIndependentOfRange()
        {
            var sheet = new PaseoTeamSheet();
            sheet.AttachOcclusion();
            sheet.SetOcclusion(PaseoOcclusionMode.Near, 5f, "Cercana activa");
            Assert.IsTrue(sheet.Root.Q<Button>("oclusion-1").ClassListContains(PaseoRecordPanel.OnClass));
            Assert.IsTrue(sheet.Root.Q<Button>("oclusion-distancia-5").enabledInHierarchy);
            sheet.SetOcclusion(PaseoOcclusionMode.Normal, 5f, "Normal activa");
            Assert.IsFalse(sheet.Root.Q<Button>("oclusion-1").ClassListContains(PaseoRecordPanel.OnClass));
            Assert.IsFalse(sheet.Root.Q<Button>("oclusion-distancia-5").enabledInHierarchy);
        }

        [Test]
        public void DiagnosticEdgesFollowModelResizeAndNeverBypassTrackingOrHiddenBuilding()
        {
            var host = new GameObject("Caja de prueba");
            try
            {
                var frame = host.AddComponent<EdificioFacadeFrame>();
                var content = host.AddComponent<PaseoMapContent>();
                content.Configure(new[] { 152693 }, 0, "Teologia", "Teología", "", frame);
                var model = new GameObject("Modelo").transform;
                model.SetParent(host.transform, false);
                content.SetBuildingModel(model, "clasico");
                content.SetDiagnosticBox(true);
                Assert.IsFalse(frame.IsVisible);
                content.SetVisible(true);
                Assert.IsTrue(frame.IsVisible);
                Assert.IsTrue(model.gameObject.activeSelf);
                Assert.IsTrue(frame.OutlineOnly);
                Assert.AreEqual(0, host.GetComponent<MeshFilter>().sharedMesh.GetIndexCount(1));
                content.SetSize(new Vector3(20, 8, 10), false);
                Assert.AreEqual(new Vector3(20, 8, 10), model.localScale);
                Assert.AreEqual(0, host.GetComponent<MeshFilter>().sharedMesh.GetIndexCount(1));
                content.SetDiagnosticBox(false);
                Assert.IsFalse(frame.IsVisible);
                Assert.IsTrue(model.gameObject.activeSelf);
                content.SetDiagnosticBox(true);
                content.SetVisible(false);
                Assert.IsFalse(frame.IsVisible);
                content.SetBoxHidden(true);
                content.SetVisible(true);
                Assert.IsFalse(frame.IsVisible);
                Assert.IsFalse(model.gameObject.activeSelf);
            }
            finally { Object.DestroyImmediate(host); }
        }

        [Test]
        public void ShaderCutsOnlyMeasuredForegroundAndOffRestoresTheWholeModel()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("Run with graphics to verify actual fragment clipping.");
            var host = new GameObject("Vista de oclusión");
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            var material = new Material(Shader.Find("AncoRA/Paseo/Bloques"));
            var depth = new Texture2D(2, 2, TextureFormat.RFloat, false, true) { filterMode = FilterMode.Point };
            var result = new Texture2D(64, 64, TextureFormat.RGB24, false);
            var target = RenderTexture.GetTemporary(64, 64, 24);
            var previous = RenderTexture.active;
            try
            {
                var camera = host.AddComponent<Camera>();
                camera.orthographic = true;
                camera.orthographicSize = 0.5f;
                camera.aspect = 1f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.allowHDR = camera.allowMSAA = false;
                camera.cullingMask = 1 << 31;
                camera.targetTexture = target;
                quad.layer = 31;
                quad.transform.position = new Vector3(0, 0, 3);
                quad.GetComponent<Renderer>().sharedMaterial = material;
                material.SetColor("_BaseColor", Color.green);
                Shader.SetGlobalMatrix("_PaseoOcclusionDisplay", Matrix4x4.identity);
                depth.SetPixelData(new[] { 1f, 0f, 1f, 0f }, 0);
                depth.Apply();
                Shader.SetGlobalTexture("_PaseoOcclusionDepth", depth);
                Color Sample(int x, int y)
                {
                    camera.Render();
                    RenderTexture.active = target;
                    result.ReadPixels(new Rect(0, 0, 64, 64), 0, 0);
                    result.Apply();
                    return result.GetPixel(x, y);
                }
                Shader.SetGlobalFloat("_PaseoOcclusionOn", 1f);
                Assert.Less(Sample(16, 32).g, 0.05f, "measured foreground reveals camera background");
                Assert.Greater(Sample(48, 32).g, 0.2f, "missing depth must keep the model");
                Directory.CreateDirectory("Builds/Oclusion");
                File.WriteAllBytes("Builds/Oclusion/Shader-cercana.png", result.EncodeToPNG());
                // Rotate the sensor mapping: the measured half must follow the display transform.
                var rotated = Matrix4x4.zero;
                rotated.m10 = 1f;
                rotated.m01 = -1f;
                rotated.m21 = 1f;
                Shader.SetGlobalMatrix("_PaseoOcclusionDisplay", rotated);
                Assert.Less(Sample(32, 16).g, 0.05f);
                Assert.Greater(Sample(32, 48).g, 0.2f);
                Shader.SetGlobalFloat("_PaseoOcclusionOn", 0f);
                Assert.Greater(Sample(16, 32).g, 0.2f, "off restores every pixel");
                File.WriteAllBytes("Builds/Oclusion/Shader-sin.png", result.EncodeToPNG());
            }
            finally
            {
                Shader.SetGlobalFloat("_PaseoOcclusionOn", 0f);
                RenderTexture.active = previous;
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(quad);
                Object.DestroyImmediate(material);
                Object.DestroyImmediate(depth);
                Object.DestroyImmediate(result);
                RenderTexture.ReleaseTemporary(target);
            }
        }
    }
}
