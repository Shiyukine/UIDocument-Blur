using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;

[RequireComponent(typeof(UIDocument))]
public class UIDocumentBlur : MonoBehaviour
{
    public enum BlurType
    {
        Kawase,
        DualKawase,
        Gaussian
    }

    [Header("References")]
    public Camera targetCamera;

    [Header("Blur Settings")]
    public BlurType blurType = BlurType.DualKawase;

    [Tooltip("Class name to search for (without the dot)")]
    public string blurClassName = "blur";

    [Header("Kawase Settings")]
    [Range(1, 8)] public int kawaseIterations = 4;

    [Header("Dual Kawase Settings")]
    [Range(1, 6)] public int dualKawasePasses = 4;

    [Header("Gaussian Settings")]
    [Range(1, 8)] public int gaussianIterations = 3;
    [Range(0.1f, 10)] public float gaussianBlurSize = 3f;

    [Header("Common Settings")]
    [Range(1, 8)] public int downsample = 2;

    [Header("Performance")]
    public int updateInterval = 2;
    public bool onlyUpdateWhenVisible = true;

    private UIDocument uiDocument;
    private List<BlurPanelData> panels = new List<BlurPanelData>();
    private RenderTexture cameraRT;
    private RenderTexture blurredFullRT;
    private Material kawaseMaterial;
    private Material dualKawaseMaterial;
    private Material gaussianMaterial;
    private int frameCounter = 0;
    private bool isInitialized = false;
    private bool needsRefresh = false;

    private static readonly int BlurSizeId = Shader.PropertyToID("_BlurSize");
    private static readonly int OffsetId = Shader.PropertyToID("_Offset");

    private class BlurPanelData
    {
        public VisualElement panel;
        public VisualElement background;
        public Texture2D croppedTexture;
    }

    void Awake()
    {
        uiDocument = GetComponent<UIDocument>();
        CreateMaterials();
    }

    void CreateMaterials()
    {
        var kawaseShader = Shader.Find("Hidden/KawaseBlur");
        if (kawaseShader != null)
        {
            kawaseMaterial = new Material(kawaseShader);
            kawaseMaterial.hideFlags = HideFlags.HideAndDontSave;
        }

        var dualKawaseShader = Shader.Find("Hidden/DualKawaseBlur");
        if (dualKawaseShader != null)
        {
            dualKawaseMaterial = new Material(dualKawaseShader);
            dualKawaseMaterial.hideFlags = HideFlags.HideAndDontSave;
        }

        var gaussianShader = Shader.Find("Hidden/GaussianBlur");
        if (gaussianShader != null)
        {
            gaussianMaterial = new Material(gaussianShader);
            gaussianMaterial.hideFlags = HideFlags.HideAndDontSave;
        }
    }

    RenderTexture CreateRT(int width, int height, int depth = 0)
    {
        var rt = new RenderTexture(width, height, depth, RenderTextureFormat.ARGB32);
        rt.wrapMode = TextureWrapMode.Clamp;
        rt.filterMode = FilterMode.Bilinear;
        rt.Create();
        return rt;
    }

    void Start()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;

        StartCoroutine(Initialize());
    }

    System.Collections.IEnumerator Initialize()
    {
        yield return null;
        yield return null;
        yield return null;

        if (uiDocument == null)
        {
            Debug.LogError("UIDocumentBlur: UIDocument not found!");
            yield break;
        }

        if (targetCamera == null)
        {
            Debug.LogError("UIDocumentBlur: No camera found!");
            yield break;
        }

        needsRefresh = true;
    }

    void LateUpdate()
    {
        if (needsRefresh)
        {
            needsRefresh = false;
            FindAndSetupBlurPanels();
            isInitialized = panels.Count > 0;
            Debug.Log($"UIDocumentBlur: Found {panels.Count} panels with class '{blurClassName}'");
            return;
        }

        if (!isInitialized) return;

        frameCounter++;
        if (frameCounter % updateInterval != 0) return;

        UpdateBlur();
    }

    void FindAndSetupBlurPanels()
    {
        foreach (var panel in panels)
        {
            if (panel.croppedTexture != null)
                Destroy(panel.croppedTexture);

            if (panel.background != null && panel.background.parent != null)
                panel.background.RemoveFromHierarchy();
        }
        panels.Clear();

        var root = uiDocument.rootVisualElement;
        var blurElements = root.Query<VisualElement>(className: blurClassName).ToList();

        foreach (var panel in blurElements)
        {
            var existingBg = panel.Q<VisualElement>("blur-background-auto");
            if (existingBg != null)
                existingBg.RemoveFromHierarchy();

            var background = new VisualElement();
            background.name = "blur-background-auto";
            background.pickingMode = PickingMode.Ignore;
            background.style.position = Position.Absolute;
            background.style.left = 0;
            background.style.top = 0;
            background.style.right = 0;
            background.style.bottom = 0;
            background.style.unityBackgroundScaleMode = ScaleMode.StretchToFill;

            panel.Insert(0, background);
            panel.style.overflow = Overflow.Hidden;

            panels.Add(new BlurPanelData
            {
                panel = panel,
                background = background,
                croppedTexture = null
            });
        }
    }

    void UpdateBlur()
    {
        bool anyVisible = false;
        foreach (var panelData in panels)
        {
            if (IsPanelVisible(panelData.panel))
            {
                anyVisible = true;
                break;
            }
        }

        if (onlyUpdateWhenVisible && !anyVisible) return;

        CaptureAndBlur();

        foreach (var panelData in panels)
        {
            if (!IsPanelVisible(panelData.panel)) continue;

            Rect panelRect = panelData.panel.worldBound;

            if (float.IsNaN(panelRect.x) || float.IsNaN(panelRect.y) ||
                float.IsNaN(panelRect.width) || float.IsNaN(panelRect.height))
                continue;

            if (panelRect.width <= 0 || panelRect.height <= 0) continue;

            CropBlurToPanel(panelData, panelRect);
        }
    }

    bool IsPanelVisible(VisualElement panel)
    {
        if (panel == null) return false;
        if (panel.resolvedStyle.display == DisplayStyle.None) return false;
        if (panel.resolvedStyle.visibility == Visibility.Hidden) return false;
        if (panel.resolvedStyle.opacity < 0.01f) return false;

        var parent = panel.parent;
        while (parent != null)
        {
            if (parent.resolvedStyle.display == DisplayStyle.None) return false;
            if (parent.resolvedStyle.visibility == Visibility.Hidden) return false;
            parent = parent.parent;
        }

        return true;
    }

    void CaptureAndBlur()
    {
        int fullWidth = targetCamera.pixelWidth;
        int fullHeight = targetCamera.pixelHeight;

        if (fullWidth <= 0 || fullHeight <= 0) return;

        int blurWidth = Mathf.Max(1, fullWidth / downsample);
        int blurHeight = Mathf.Max(1, fullHeight / downsample);

        if (cameraRT == null || cameraRT.width != fullWidth || cameraRT.height != fullHeight)
        {
            if (cameraRT != null)
            {
                cameraRT.Release();
                Destroy(cameraRT);
            }
            cameraRT = CreateRT(fullWidth, fullHeight, 24);
        }

        if (blurredFullRT == null || blurredFullRT.width != blurWidth || blurredFullRT.height != blurHeight)
        {
            if (blurredFullRT != null)
            {
                blurredFullRT.Release();
                Destroy(blurredFullRT);
            }
            blurredFullRT = CreateRT(blurWidth, blurHeight);
        }

        RenderTexture originalTarget = targetCamera.targetTexture;
        targetCamera.targetTexture = cameraRT;
        targetCamera.Render();
        targetCamera.targetTexture = originalTarget;

        switch (blurType)
        {
            case BlurType.Kawase:
                ApplyKawaseBlur(blurWidth, blurHeight);
                break;
            case BlurType.DualKawase:
                ApplyDualKawaseBlur(blurWidth, blurHeight);
                break;
            case BlurType.Gaussian:
                ApplyGaussianBlur(blurWidth, blurHeight);
                break;
        }
    }

    void ApplyKawaseBlur(int blurWidth, int blurHeight)
    {
        if (kawaseMaterial == null) return;

        RenderTexture temp1 = RenderTexture.GetTemporary(blurWidth, blurHeight, 0, RenderTextureFormat.ARGB32);
        RenderTexture temp2 = RenderTexture.GetTemporary(blurWidth, blurHeight, 0, RenderTextureFormat.ARGB32);
        temp1.wrapMode = TextureWrapMode.Clamp;
        temp2.wrapMode = TextureWrapMode.Clamp;

        Graphics.Blit(cameraRT, temp1);

        RenderTexture currentSource = temp1;
        RenderTexture currentDest = temp2;

        for (int i = 0; i < kawaseIterations; i++)
        {
            kawaseMaterial.SetFloat(OffsetId, i + 0.5f);
            Graphics.Blit(currentSource, currentDest, kawaseMaterial);
            (currentSource, currentDest) = (currentDest, currentSource);
        }

        Graphics.Blit(currentSource, blurredFullRT);

        RenderTexture.ReleaseTemporary(temp1);
        RenderTexture.ReleaseTemporary(temp2);
    }

    void ApplyDualKawaseBlur(int blurWidth, int blurHeight)
    {
        if (dualKawaseMaterial == null) return;

        // Create pyramid of render textures
        RenderTexture[] pyramid = new RenderTexture[dualKawasePasses];

        int width = blurWidth;
        int height = blurHeight;

        // Downsample
        RenderTexture currentSource = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
        currentSource.wrapMode = TextureWrapMode.Clamp;
        currentSource.filterMode = FilterMode.Bilinear;

        Graphics.Blit(cameraRT, currentSource);

        for (int i = 0; i < dualKawasePasses; i++)
        {
            width = Mathf.Max(1, width / 2);
            height = Mathf.Max(1, height / 2);

            pyramid[i] = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
            pyramid[i].wrapMode = TextureWrapMode.Clamp;
            pyramid[i].filterMode = FilterMode.Bilinear;

            dualKawaseMaterial.SetFloat(OffsetId, 1f);
            Graphics.Blit(currentSource, pyramid[i], dualKawaseMaterial, 0); // Downsample pass

            RenderTexture.ReleaseTemporary(currentSource);
            currentSource = pyramid[i];
        }

        // Upsample
        for (int i = dualKawasePasses - 2; i >= 0; i--)
        {
            RenderTexture dest = RenderTexture.GetTemporary(
                pyramid[i].width, pyramid[i].height, 0, RenderTextureFormat.ARGB32);
            dest.wrapMode = TextureWrapMode.Clamp;
            dest.filterMode = FilterMode.Bilinear;

            dualKawaseMaterial.SetFloat(OffsetId, 0.5f);
            Graphics.Blit(currentSource, dest, dualKawaseMaterial, 1); // Upsample pass

            RenderTexture.ReleaseTemporary(currentSource);
            currentSource = dest;
        }

        // Final blit to output
        Graphics.Blit(currentSource, blurredFullRT);

        // Cleanup
        RenderTexture.ReleaseTemporary(currentSource);
    }

    void ApplyGaussianBlur(int blurWidth, int blurHeight)
    {
        if (gaussianMaterial == null) return;

        RenderTexture temp1 = RenderTexture.GetTemporary(blurWidth, blurHeight, 0, RenderTextureFormat.ARGB32);
        RenderTexture temp2 = RenderTexture.GetTemporary(blurWidth, blurHeight, 0, RenderTextureFormat.ARGB32);
        temp1.wrapMode = TextureWrapMode.Clamp;
        temp2.wrapMode = TextureWrapMode.Clamp;

        Graphics.Blit(cameraRT, temp1);

        gaussianMaterial.SetFloat(BlurSizeId, gaussianBlurSize);

        RenderTexture currentSource = temp1;
        RenderTexture currentDest = temp2;

        for (int i = 0; i < gaussianIterations; i++)
        {
            Graphics.Blit(currentSource, currentDest, gaussianMaterial, 0);
            (currentSource, currentDest) = (currentDest, currentSource);

            Graphics.Blit(currentSource, currentDest, gaussianMaterial, 1);
            (currentSource, currentDest) = (currentDest, currentSource);
        }

        Graphics.Blit(currentSource, blurredFullRT);

        RenderTexture.ReleaseTemporary(temp1);
        RenderTexture.ReleaseTemporary(temp2);
    }

    void CropBlurToPanel(BlurPanelData panelData, Rect panelRect)
    {
        int screenWidth = targetCamera.pixelWidth;
        int screenHeight = targetCamera.pixelHeight;

        if (screenWidth <= 0 || screenHeight <= 0) return;
        if (blurredFullRT == null) return;

        float panelX = Mathf.Max(0, panelRect.x);
        float panelY = Mathf.Max(0, panelRect.y);
        float panelRight = Mathf.Min(screenWidth, panelRect.xMax);
        float panelBottom = Mathf.Min(screenHeight, panelRect.yMax);

        float panelWidth = panelRight - panelX;
        float panelHeight = panelBottom - panelY;

        if (panelWidth <= 0 || panelHeight <= 0) return;

        int cropWidth = Mathf.Clamp(Mathf.RoundToInt(panelWidth / downsample), 1, blurredFullRT.width);
        int cropHeight = Mathf.Clamp(Mathf.RoundToInt(panelHeight / downsample), 1, blurredFullRT.height);

        if (panelData.croppedTexture == null ||
            panelData.croppedTexture.width != cropWidth ||
            panelData.croppedTexture.height != cropHeight)
        {
            if (panelData.croppedTexture != null)
                Destroy(panelData.croppedTexture);

            panelData.croppedTexture = new Texture2D(cropWidth, cropHeight, TextureFormat.RGBA32, false);
            panelData.croppedTexture.filterMode = FilterMode.Bilinear;
            panelData.croppedTexture.wrapMode = TextureWrapMode.Clamp;
        }

        float uvX = panelX / screenWidth;
        float uvY = panelY / screenHeight;
        float uvYFlipped = 1f - uvY - (panelHeight / screenHeight);

        int srcX = Mathf.FloorToInt(uvX * blurredFullRT.width);
        int srcY = Mathf.FloorToInt(uvYFlipped * blurredFullRT.height);

        srcX = Mathf.Clamp(srcX, 0, Mathf.Max(0, blurredFullRT.width - cropWidth));
        srcY = Mathf.Clamp(srcY, 0, Mathf.Max(0, blurredFullRT.height - cropHeight));

        if (srcX < 0 || srcY < 0 ||
            srcX + cropWidth > blurredFullRT.width ||
            srcY + cropHeight > blurredFullRT.height)
        {
            return;
        }

        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = blurredFullRT;

        try
        {
            panelData.croppedTexture.ReadPixels(new Rect(srcX, srcY, cropWidth, cropHeight), 0, 0, false);
            panelData.croppedTexture.Apply(false);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"UIDocumentBlur: ReadPixels failed - {e.Message}");
        }
        finally
        {
            RenderTexture.active = previous;
        }

        panelData.background.style.backgroundImage = new StyleBackground(panelData.croppedTexture);
    }

    public void RefreshPanels()
    {
        needsRefresh = true;
    }

    void OnDestroy()
    {
        if (cameraRT != null) { cameraRT.Release(); Destroy(cameraRT); }
        if (blurredFullRT != null) { blurredFullRT.Release(); Destroy(blurredFullRT); }
        if (kawaseMaterial != null) Destroy(kawaseMaterial);
        if (dualKawaseMaterial != null) Destroy(dualKawaseMaterial);
        if (gaussianMaterial != null) Destroy(gaussianMaterial);

        foreach (var panel in panels)
        {
            if (panel.croppedTexture != null)
                Destroy(panel.croppedTexture);
        }
    }
}