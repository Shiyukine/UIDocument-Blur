# UI Document Blur

A performant, easy-to-use backdrop blur effect for Unity UI Toolkit. Simply add a CSS class to any UI element to give it a beautiful blur background.

![Unity](https://img.shields.io/badge/Unity-6.0+-black?logo=unity)
![URP](https://img.shields.io/badge/URP-17.0.3+-blue)
![License](https://img.shields.io/badge/License-MIT-green)

## Features

- **Easy to use** - Just add `blur` class to any UXML element
- **Multiple blur algorithms** - Kawase, Dual Kawase, and Gaussian
- **Using camera** - Captures camera output before UI rendering
- **Highly configurable** - Adjust blur strength, iterations, and performance settings
- **Performance optimized** - Only updates when visible, configurable update interval

## Requirements

- Unity 6.0 or higher (not tested on older versions)
- Universal Render Pipeline (URP) 17.0.3 or higher (not tested on Built-in or HDRP pipelines and or older URP versions)
- UI Toolkit

## Installation

1. Copy the following files to your Unity project:

```
Assets/
├── Scripts/
│   └── UIDocumentBlur.cs
└── Shaders/
    ├── KawaseBlur.shader
    ├── DualKawaseBlur.shader
    └── GaussianBlur.shader
```

2. Make sure the shaders are included in your build (they should be automatically since they use `Shader.Find`)

## Quick Start

### 1. Add the component

Add `UI Document Blur` component to the same GameObject that has your `UIDocument`:

```
UI Document GameObject
├── UI Document
└── UI Document Blur (add this)
```

### 2. Add blur class to your UXML

```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements">
    <!-- Add 'blur' class to any element you want to blur -->
    <ui:VisualElement name="my-panel" class="blur">
        <ui:Label text="This panel has a blur background!" />
    </ui:VisualElement>
</ui:UXML>
```

### 3. Launch the scene

The blur effect will automatically appear behind your element.

## Blur Algorithms

- **Kawase (Simple)**: Fast single-pass blur. Good for subtle effects.
- **Dual Kawase (Recommended)**: Best balance of quality and performance.  Used by professional game engines.
- **Gaussian**: Mathematically accurate blur. Use lower blur sizes to avoid artifacts.

## Configuration

| Property | Description | Default |
|----------|-------------|---------|
| `Target Camera` | Camera to capture (auto-detects Main Camera) | `null` |
| `Blur Type` | Algorithm to use | `DualKawase` |
| `Blur Class Name` | CSS class to search for | `blur` |
| `Downsample` | Resolution divider (higher = faster) | `2` |
| `Update Interval` | Update every N frames | `2` |
| `Only Update When Visible` | Skip updates for hidden panels | `true` |

## Examples

### Pause Menu

```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements">
    <ui:VisualElement name="pause-overlay" class="blur pause-overlay">
        <ui:VisualElement name="pause-menu" class="pause-menu">
            <ui:Label text="PAUSED" class="title" />
            <ui:Button text="Resume" name="resume-btn" />
            <ui:Button text="Settings" name="settings-btn" />
            <ui:Button text="Quit" name="quit-btn" />
        </ui:VisualElement>
    </ui:VisualElement>
</ui:UXML>
```

```css
.pause-overlay {
    position: absolute;
    width: 100%;
    height: 100%;
}

.pause-menu {
    position: absolute;
    left: 50%;
    top: 50%;
    translate: -50% -50%;
    padding: 20px;
    background-color: rgba(0, 0, 0, 0.3);
    border-radius: 16px;
}
```

### Dialog Box

```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements">
    <ui:VisualElement name="dialog" class="blur dialog">
        <ui:Label text="Are you sure?" />
        <ui:Button text="Yes" />
        <ui:Button text="No" />
    </ui:VisualElement>
</ui:UXML>
```

```css
.dialog {
    position: absolute;
    left: 50%;
    top: 50%;
    translate: -50% -50%;
    width: 300px;
    padding: 20px;
    background-color: rgba(255, 255, 255, 0.1);
    border-radius: 12px;
}
```

### Multiple Blur Panels

```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements">
    <!-- All panels with 'blur' class will have blur effect -->
    <ui:VisualElement name="sidebar" class="blur sidebar" />
    <ui:VisualElement name="header" class="blur header" />
    <ui:VisualElement name="tooltip" class="blur tooltip" />
</ui:UXML>
```

## Styling the Blur Background

Because the blur is applied as a background image, you must create a VisualElement inside your blur element to style it.

```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements">
    <ui:VisualElement name="my-panel" class="blur">
        <!-- Background element for styling -->
        <ui:VisualElement class="blur-background" />
        <ui:Label text="This panel has a blur background!" />
    </ui:VisualElement>
</ui:UXML>
```

```css
.blur-background {
    position: absolute;
    width: 100%;
    height: 100%;
    border-radius: 16px;
    /* Optional overlay color */
    background-color: rgba(255, 255, 255, 0.2);
    z-index: -1; /* Ensure it's behind other content */
}
```

## API

### Public Methods

```csharp
// Refresh panels (call after dynamically adding new blur elements)
GetComponent<UIDocumentBlur>().RefreshPanels();
```

### Showing/Hiding Blur Panels

Control visibility through USS or C#:

```csharp
// Hide panel (blur stops updating automatically)
blurPanel.style.display = DisplayStyle.None;

// Show panel
blurPanel.style.display = DisplayStyle.Flex;
```

## Performance Tips

1. **Use Dual Kawase** - Best performance for strong blur
2. **Increase Downsample** - Set to 3-4 for better performance
3. **Increase Update Interval** - Set to 3-4 if blur doesn't need to be real-time
4. **Enable Only Update When Visible** - Skips hidden panels
5. **Use fewer blur panels** - Each panel requires a texture crop operation

### Recommended Settings by Use Case

**Real-time blur (moving background):**

```
Blur Type: DualKawase
Passes: 3
Downsample: 2
Update Interval: 1
```

**Static blur (pause menu):**

```
Blur Type: DualKawase
Passes: 5
Downsample: 2
Update Interval: 3
```

**Mobile/Low-end:**

```
Blur Type: Kawase
Iterations: 3
Downsample: 4
Update Interval: 3
```

## Troubleshooting

### Blur not appearing

1. Check that shaders are in your project:
   - `Hidden/KawaseBlur`
   - `Hidden/DualKawaseBlur`
   - `Hidden/GaussianBlur`

2. Check Console for errors

3. Make sure the element has the `blur` class (or your custom class name)

### Black screen

- Make sure `Target Camera` is assigned or Camera.main exists
- Check that UI is rendered after the camera

### Artifacts with Gaussian blur

- Use lower `Blur Size` (1-5)
- Or switch to `Dual Kawase` which has no artifacts

### Performance issues

- Increase `Downsample` to 3 or 4
- Increase `Update Interval` to 3 or higher
- Use `Kawase` or `Dual Kawase` instead of `Gaussian`
- Enable `Only Update When Visible`

## How It Works

1. **Capture** - Renders the camera to a RenderTexture (without UI)
2. **Blur** - Applies blur algorithm to the captured image
3. **Crop** - For each blur panel, crops the portion of the blur that's behind it
4. **Apply** - Sets the cropped texture as the panel's background

## License

MIT License - Feel free to use in personal and commercial projects.

## Credits

- Kawase blur algorithm by [Masaki Kawase](https://web.archive.org/web/20190324074351/https://genderi.org/frame-buffer-postprocessing-effects-in-double-s-t-e-a-l-wreckl.html)
- Dual Kawase blur based on [SIGGRAPH 2015 presentation by Marius Bjørge](https://community.arm.com/cfs-file/__key/communityserver-blogs-components-weblogfiles/00-00-00-20-66/siggraph2015_2D00_mmg_2D00_marius_2D00_notes.pdf)
