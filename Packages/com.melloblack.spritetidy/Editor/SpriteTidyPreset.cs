using MelloBlack.SpriteTidy.Core;
using UnityEngine;

namespace MelloBlack.SpriteTidy
{
    /// <summary>Reusable Sprite Tidy settings, saved as an asset.</summary>
    [CreateAssetMenu(fileName = "SpriteTidyPreset", menuName = "Sprite Tidy/Preset", order = 400)]
    public sealed class SpriteTidyPreset : ScriptableObject
    {
        [Header("Background")]
        [Tooltip("Remove solid-color and fake checkerboard backgrounds. Images that are already transparent are left as they are.")]
        public bool removeBackground = true;

        [Tooltip("How far a pixel's color may be from the background color (RGB distance) and still count as background.")]
        [Range(0, 150)]
        public int backgroundTolerance = 40;

        [Tooltip("Also remove background that shows through enclosed holes, like the middle of a ring. Auto does this for checkerboards only, because an enclosed area of a solid background color is often part of the art.")]
        public HoleFilling fillHoles = HoleFilling.Auto;

        [Tooltip("Smallest enclosed hole to remove, as a percentage of the image area.")]
        [Range(0f, 5f)]
        public float minHoleArea = 0.05f;

        [Tooltip("Width in pixels of the band along content edges whose colors are un-blended from the background, so no light or dark halo is left.")]
        [Range(0, 4)]
        public int edgeBand = 2;

        [Header("Alpha")]
        [Tooltip("Snap nearly opaque pixels to fully opaque and clear nearly invisible haze.")]
        public bool cleanAlpha = true;

        [Tooltip("Alpha at or above this becomes fully opaque. Generated images often leave their solid parts at 250–253, which makes sprites faintly see-through.")]
        [Range(128, 255)]
        public int opaqueThreshold = 240;

        [Tooltip("Alpha below this becomes fully transparent. Raise it to remove soft glows; 0 keeps everything.")]
        [Range(0, 128)]
        public int clearThreshold = 8;

        [Header("Objects")]
        [Tooltip("Auto: every separate object in the image becomes its own sprite. Off: the whole image is one sprite.")]
        public SplitMode splitObjects = SplitMode.Auto;

        [Tooltip("Pixels at least this opaque form the solid part of objects. Fainter pixels (glows, soft shadows) are attached to the nearest object afterwards.")]
        [Range(1, 255)]
        public int coreAlpha = 128;

        [Tooltip("Pieces smaller than this percentage of the largest piece are fragments (sparkles, detached bits) that join the nearest object instead of becoming sprites.")]
        [Range(0f, 50f)]
        public float fragmentSize = 3f;

        [Tooltip("How far a fragment may be from an object and still join it, as a percentage of the image's longer side. Farther fragments are dropped as noise.")]
        [Range(0f, 20f)]
        public float fragmentMergeDistance = 4f;

        [Tooltip("Objects whose bounds overlap by at least this percentage of the smaller one are merged into one.")]
        [Range(0f, 100f)]
        public float overlapMerge = 25f;

        [Tooltip("Faint pixels within this distance of an object belong to it, as a percentage of the image's longer side. Farther ones are dropped.")]
        [Range(0f, 20f)]
        public float haloDistance = 3f;

        [Header("Canvas")]
        [Tooltip("Fixed: scale every sprite to fit a canvas of the size below. Trim Only: keep the original pixel size and just trim, pad and align.")]
        public CanvasMode canvasMode = CanvasMode.Fixed;

        [Min(1)]
        public int width = 512;

        [Min(1)]
        public int height = 512;

        [Tooltip("Empty space between the content and each edge of the canvas, in output pixels.")]
        [Min(0)]
        public int padding = 16;

        public HorizontalAnchor horizontalAnchor = HorizontalAnchor.Center;

        [Tooltip("Bottom keeps the base of every sprite on the same line, which suits characters and objects that stand on the ground.")]
        public VerticalAnchor verticalAnchor = VerticalAnchor.Center;

        [Tooltip("Fit Each: every sprite fills the canvas. Keep Relative Size: sprites from the same image share one scale, so a small item stays smaller than a big one.")]
        public FitMode fitMode = FitMode.FitEach;

        [Tooltip("Allow content smaller than the canvas to be scaled up.")]
        public bool allowUpscale = true;

        public ResampleFilter filter = ResampleFilter.Bicubic;

        [Header("Import")]
        [Min(0.01f)]
        public float pixelsPerUnit = 100f;

        [Tooltip("Set each sprite's pivot to the anchor point, e.g. bottom-center when the vertical anchor is Bottom.")]
        public bool pivotFromAnchor = true;

        public TidySettings ToSettings()
        {
            return new TidySettings
            {
                Background = new BackgroundSettings
                {
                    Enabled = removeBackground,
                    Tolerance = backgroundTolerance,
                    Holes = fillHoles,
                    MinHoleAreaPercent = minHoleArea,
                    EdgeBand = edgeBand,
                },
                Alpha = new AlphaCleanupSettings
                {
                    Enabled = cleanAlpha,
                    OpaqueThreshold = opaqueThreshold,
                    ClearThreshold = clearThreshold,
                },
                Split = new SplitSettings
                {
                    Mode = splitObjects,
                    CoreAlpha = coreAlpha,
                    FragmentAreaPercent = fragmentSize,
                    MergeDistancePercent = fragmentMergeDistance,
                    OverlapMergePercent = overlapMerge,
                    HaloDistancePercent = haloDistance,
                },
                Canvas = new CanvasSettings
                {
                    Mode = canvasMode,
                    Width = width,
                    Height = height,
                    Padding = padding,
                    HorizontalAnchor = horizontalAnchor,
                    VerticalAnchor = verticalAnchor,
                    FitMode = fitMode,
                    AllowUpscale = allowUpscale,
                    Filter = filter,
                },
            };
        }
    }
}
