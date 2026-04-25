using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public class CubemapFaceExporter : EditorWindow
{
    private enum OutputFormat
    {
        Png8,
        Exr16,
        Exr32
    }

    private struct FaceInfo
    {
        public CubemapFace Face;
        public string FileName;
        public string ShaderProperty;
        public string DisplayName;

        public FaceInfo(CubemapFace face, string fileName, string shaderProperty, string displayName)
        {
            Face = face;
            FileName = fileName;
            ShaderProperty = shaderProperty;
            DisplayName = displayName;
        }
    }

    private static readonly FaceInfo[] Faces =
    {
        new FaceInfo(CubemapFace.NegativeX, "+X_Right", "_RightTex", "Right (+X)"),
        new FaceInfo(CubemapFace.PositiveX, "-X_Left", "_LeftTex", "Left (-X)"),
        new FaceInfo(CubemapFace.PositiveY, "+Y_Up", "_UpTex", "Up (+Y)"),
        new FaceInfo(CubemapFace.NegativeY, "-Y_Down", "_DownTex", "Down (-Y)"),
        new FaceInfo(CubemapFace.PositiveZ, "+Z_Front", "_FrontTex", "Front (+Z)"),
        new FaceInfo(CubemapFace.NegativeZ, "-Z_Back", "_BackTex", "Back (-Z)"),
    };

    private const int CustomResolutionValue = -1;
    private static readonly int[] ResolutionValues = { 0, 128, 256, 512, 1024, CustomResolutionValue };
    private static readonly string[] ResolutionLabels = { "源尺寸", "128", "256", "512", "1024", "自定义" };

    private Cubemap sourceCubemap;
    private DefaultAsset outputFolder;
    private OutputFormat outputFormat = OutputFormat.Exr16;
    private int outputResolution = 0;
    private int customOutputResolution = 512;
    private bool createSkyboxMaterial = true;
    private bool createSphereMaterial = true;
    private bool overwriteExisting = true;
    private Vector2 scrollPosition;

    [MenuItem("BjTools/贴图工具/Cubemap导出6面", false, 100)]
    public static void OpenWindow()
    {
        CubemapFaceExporter window = GetWindow<CubemapFaceExporter>();
        window.titleContent = new GUIContent("Cubemap导出6面");
        window.minSize = new Vector2(520f, 360f);
        window.Show();
    }

    [MenuItem("Assets/导出Cubemap为Skybox 6 Sided贴图", true)]
    private static bool ValidateExportSelectedCubemap()
    {
        return Selection.activeObject is Cubemap;
    }

    [MenuItem("Assets/导出Cubemap为Skybox 6 Sided贴图", false, 2100)]
    private static void ExportSelectedCubemap()
    {
        CubemapFaceExporter window = GetWindow<CubemapFaceExporter>();
        window.sourceCubemap = Selection.activeObject as Cubemap;
        window.titleContent = new GUIContent("Cubemap导出6面");
        window.Show();
    }

    private void OnGUI()
    {
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        EditorGUILayout.LabelField("从Unity Cubemap导出Skybox/6 Sided六面贴图", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "请先把原始HDRI按Unity Cubemap方式导入，再用本工具导出六面。这样导出的六张图来自Unity自己的Cubemap面数据，通常会比外部转换工具更接近Skybox/Cubemap效果。",
            MessageType.Info);

        sourceCubemap = (Cubemap)EditorGUILayout.ObjectField("源Cubemap", sourceCubemap, typeof(Cubemap), false);
        outputFolder = (DefaultAsset)EditorGUILayout.ObjectField("输出目录", outputFolder, typeof(DefaultAsset), false);
        outputFormat = (OutputFormat)EditorGUILayout.EnumPopup("输出格式", outputFormat);
        outputResolution = EditorGUILayout.IntPopup("导出分辨率", outputResolution, ResolutionLabels, ResolutionValues);
        if (outputResolution == CustomResolutionValue)
        {
            customOutputResolution = Mathf.Max(1, EditorGUILayout.IntField("自定义尺寸", customOutputResolution));
        }
        createSkyboxMaterial = EditorGUILayout.Toggle("创建6 Sided材质", createSkyboxMaterial);
        createSphereMaterial = EditorGUILayout.Toggle("创建球体方向采样材质", createSphereMaterial);
        overwriteExisting = EditorGUILayout.Toggle("覆盖已有文件", overwriteExisting);

        EditorGUILayout.Space(8f);
        DrawFormatHelp();

        using (new EditorGUI.DisabledScope(sourceCubemap == null))
        {
            if (GUILayout.Button("导出六面贴图", GUILayout.Height(34f)))
            {
                Export();
            }
        }

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("导出面对应关系", EditorStyles.boldLabel);
        foreach (FaceInfo face in Faces)
        {
            EditorGUILayout.LabelField(face.DisplayName, face.FileName);
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawFormatHelp()
    {
        switch (outputFormat)
        {
            case OutputFormat.Png8:
                EditorGUILayout.HelpBox("PNG会被限制到0~1的8位颜色范围，不适合保留HDR亮度。仅建议普通LDR天空图使用。", MessageType.Warning);
                break;
            case OutputFormat.Exr16:
                EditorGUILayout.HelpBox("EXR 16-bit Float推荐用于HDR天空图，体积比32位小，并能保留高亮信息。", MessageType.Info);
                break;
            case OutputFormat.Exr32:
                EditorGUILayout.HelpBox("EXR 32-bit Float保留精度最高，但文件体积更大。", MessageType.Info);
                break;
        }
    }

    private void Export()
    {
        if (sourceCubemap == null)
        {
            EditorUtility.DisplayDialog("导出失败", "请先指定源Cubemap。", "确定");
            return;
        }

        string folderAssetPath = GetOutputFolderAssetPath();
        if (string.IsNullOrEmpty(folderAssetPath))
        {
            EditorUtility.DisplayDialog("导出失败", "输出目录必须位于当前Unity项目的Assets目录下。", "确定");
            return;
        }

        Directory.CreateDirectory(ToAbsolutePath(folderAssetPath));

        string cubemapPath = AssetDatabase.GetAssetPath(sourceCubemap);
        TextureImporter importer = AssetImporter.GetAtPath(cubemapPath) as TextureImporter;
        bool restoreReadable = false;

        try
        {
            if (importer != null && !importer.isReadable)
            {
                restoreReadable = true;
                importer.isReadable = true;
                importer.SaveAndReimport();
            }

            Dictionary<string, Texture2D> exportedTextures = ExportFaces(folderAssetPath);

            if (createSkyboxMaterial)
            {
                CreateSkyboxMaterial(folderAssetPath, exportedTextures);
            }

            if (createSphereMaterial)
            {
                CreateSphereMaterial(folderAssetPath, exportedTextures);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("导出完成", $"已导出到:\n{folderAssetPath}", "确定");
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            EditorUtility.DisplayDialog("导出失败", e.Message, "确定");
        }
        finally
        {
            if (restoreReadable && importer != null)
            {
                importer.isReadable = false;
                importer.SaveAndReimport();
            }
        }
    }

    private Dictionary<string, Texture2D> ExportFaces(string folderAssetPath)
    {
        Dictionary<string, Texture2D> exportedTextures = new Dictionary<string, Texture2D>();
        string extension = outputFormat == OutputFormat.Png8 ? ".png" : ".exr";

        try
        {
            for (int i = 0; i < Faces.Length; i++)
            {
                FaceInfo face = Faces[i];
                EditorUtility.DisplayProgressBar("导出Cubemap六面", face.DisplayName, i / (float)Faces.Length);

                string assetPath = $"{folderAssetPath}/{sourceCubemap.name}_{face.FileName}{extension}";
                string absolutePath = ToAbsolutePath(assetPath);

                if (!overwriteExisting && File.Exists(absolutePath))
                {
                    throw new IOException($"文件已存在: {assetPath}");
                }

                Texture2D faceTexture = CreateFaceTexture(face.Face);
                byte[] bytes = EncodeTexture(faceTexture);
                if (bytes == null || bytes.Length == 0)
                {
                    throw new InvalidOperationException($"贴图编码失败: {face.DisplayName}");
                }

                File.WriteAllBytes(absolutePath, bytes);
                DestroyImmediate(faceTexture);

                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
                ConfigureExportedTexture(assetPath);
                exportedTextures[face.ShaderProperty] = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            }

            return exportedTextures;
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    private Texture2D CreateFaceTexture(CubemapFace face)
    {
        TextureFormat textureFormat = outputFormat == OutputFormat.Png8 ? TextureFormat.RGBA32 : TextureFormat.RGBAFloat;
        int sourceSize = sourceCubemap.width;
        int targetSize = GetOutputResolution();
        Color[] pixels = sourceCubemap.GetPixels(face);

        // Cubemap face readback and 2D skybox textures use opposite vertical pixel order.
        pixels = FlipPixelsVertically(pixels, sourceSize, sourceSize);
        if (targetSize != sourceSize)
        {
            pixels = ResizePixelsBilinear(pixels, sourceSize, sourceSize, targetSize, targetSize);
        }

        Texture2D texture = new Texture2D(targetSize, targetSize, textureFormat, false, true);
        texture.SetPixels(pixels);
        texture.Apply(false, false);
        return texture;
    }

    private int GetOutputResolution()
    {
        if (outputResolution == CustomResolutionValue)
        {
            return customOutputResolution;
        }

        return outputResolution <= 0 ? sourceCubemap.width : outputResolution;
    }

    private static Color[] FlipPixelsVertically(Color[] source, int width, int height)
    {
        Color[] flipped = new Color[source.Length];
        for (int y = 0; y < height; y++)
        {
            int sourceRow = y * width;
            int targetRow = (height - 1 - y) * width;
            Array.Copy(source, sourceRow, flipped, targetRow, width);
        }

        return flipped;
    }

    private static Color[] ResizePixelsBilinear(Color[] source, int sourceWidth, int sourceHeight, int targetWidth, int targetHeight)
    {
        Color[] resized = new Color[targetWidth * targetHeight];
        float scaleX = sourceWidth / (float)targetWidth;
        float scaleY = sourceHeight / (float)targetHeight;

        for (int y = 0; y < targetHeight; y++)
        {
            float sourceY = (y + 0.5f) * scaleY - 0.5f;
            int y0 = Mathf.Clamp(Mathf.FloorToInt(sourceY), 0, sourceHeight - 1);
            int y1 = Mathf.Clamp(y0 + 1, 0, sourceHeight - 1);
            float yLerp = Mathf.Clamp01(sourceY - y0);

            for (int x = 0; x < targetWidth; x++)
            {
                float sourceX = (x + 0.5f) * scaleX - 0.5f;
                int x0 = Mathf.Clamp(Mathf.FloorToInt(sourceX), 0, sourceWidth - 1);
                int x1 = Mathf.Clamp(x0 + 1, 0, sourceWidth - 1);
                float xLerp = Mathf.Clamp01(sourceX - x0);

                Color bottom = Color.Lerp(source[y0 * sourceWidth + x0], source[y0 * sourceWidth + x1], xLerp);
                Color top = Color.Lerp(source[y1 * sourceWidth + x0], source[y1 * sourceWidth + x1], xLerp);
                resized[y * targetWidth + x] = Color.Lerp(bottom, top, yLerp);
            }
        }

        return resized;
    }

    private byte[] EncodeTexture(Texture2D texture)
    {
        if (outputFormat == OutputFormat.Png8)
        {
            return texture.EncodeToPNG();
        }

        Texture2D.EXRFlags flags = Texture2D.EXRFlags.CompressZIP;
        if (outputFormat == OutputFormat.Exr32)
        {
            flags |= Texture2D.EXRFlags.OutputAsFloat;
        }

        return texture.EncodeToEXR(flags);
    }

    private void ConfigureExportedTexture(string assetPath)
    {
        TextureImporter faceImporter = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (faceImporter == null)
        {
            return;
        }

        faceImporter.textureType = TextureImporterType.Default;
        faceImporter.textureShape = TextureImporterShape.Texture2D;
        faceImporter.wrapMode = TextureWrapMode.Clamp;
        faceImporter.filterMode = FilterMode.Trilinear;
        faceImporter.mipmapEnabled = true;
        faceImporter.textureCompression = TextureImporterCompression.Uncompressed;
        faceImporter.sRGBTexture = outputFormat == OutputFormat.Png8;
        faceImporter.SaveAndReimport();
    }

    private void CreateSkyboxMaterial(string folderAssetPath, Dictionary<string, Texture2D> textures)
    {
        Shader skyboxShader = Shader.Find("Skybox/6 Sided");
        if (skyboxShader == null)
        {
            throw new InvalidOperationException("找不到内置Shader: Skybox/6 Sided");
        }

        string materialPath = AssetDatabase.GenerateUniqueAssetPath($"{folderAssetPath}/{sourceCubemap.name}_Skybox6Sided.mat");
        Material material = new Material(skyboxShader)
        {
            name = $"{sourceCubemap.name}_Skybox6Sided"
        };

        foreach (KeyValuePair<string, Texture2D> texturePair in textures)
        {
            if (material.HasProperty(texturePair.Key))
            {
                material.SetTexture(texturePair.Key, texturePair.Value);
            }
        }

        if (material.HasProperty("_Exposure"))
        {
            material.SetFloat("_Exposure", 1f);
        }

        if (material.HasProperty("_Rotation"))
        {
            material.SetFloat("_Rotation", 0f);
        }

        if (material.HasProperty("_SampleSpace"))
        {
            material.SetFloat("_SampleSpace", 1f);
        }

        if (material.HasProperty("_Pitch"))
        {
            material.SetFloat("_Pitch", 0f);
        }

        AssetDatabase.CreateAsset(material, materialPath);
        Selection.activeObject = material;
    }

    private void CreateSphereMaterial(string folderAssetPath, Dictionary<string, Texture2D> textures)
    {
        Shader sphereShader = Shader.Find("Bj/Skybox6SidedSphere");
        if (sphereShader == null)
        {
            throw new InvalidOperationException("找不到Shader: Bj/Skybox6SidedSphere");
        }

        string materialPath = AssetDatabase.GenerateUniqueAssetPath($"{folderAssetPath}/{sourceCubemap.name}_SkySphere6Sided.mat");
        Material material = new Material(sphereShader)
        {
            name = $"{sourceCubemap.name}_SkySphere6Sided"
        };

        foreach (KeyValuePair<string, Texture2D> texturePair in textures)
        {
            if (material.HasProperty(texturePair.Key))
            {
                material.SetTexture(texturePair.Key, texturePair.Value);
            }
        }

        if (material.HasProperty("_Tint"))
        {
            material.SetColor("_Tint", Color.white);
        }

        if (material.HasProperty("_Exposure"))
        {
            material.SetFloat("_Exposure", 1f);
        }

        if (material.HasProperty("_Rotation"))
        {
            material.SetFloat("_Rotation", 0f);
        }

        if (material.HasProperty("_SampleSpace"))
        {
            material.SetFloat("_SampleSpace", 1f);
        }

        if (material.HasProperty("_Pitch"))
        {
            material.SetFloat("_Pitch", 0f);
        }

        AssetDatabase.CreateAsset(material, materialPath);
        Selection.activeObject = material;
    }

    private string GetOutputFolderAssetPath()
    {
        if (outputFolder == null)
        {
            string sourcePath = AssetDatabase.GetAssetPath(sourceCubemap);
            string sourceFolder = Path.GetDirectoryName(sourcePath);
            return string.IsNullOrEmpty(sourceFolder) ? "Assets" : sourceFolder.Replace("\\", "/");
        }

        string folderPath = AssetDatabase.GetAssetPath(outputFolder);
        if (!AssetDatabase.IsValidFolder(folderPath))
        {
            return null;
        }

        return folderPath.Replace("\\", "/");
    }

    private static string ToAbsolutePath(string assetPath)
    {
        string relativePath = assetPath.Substring("Assets".Length).TrimStart('/', '\\');
        return Path.Combine(Application.dataPath, relativePath);
    }
}
