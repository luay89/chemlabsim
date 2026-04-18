using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Attach to any Canvas root. Overrides CanvasScaler at Awake
/// to ScaleWithScreenSize with a 1080×1920 reference and 0.5 match.
/// </summary>
[RequireComponent(typeof(CanvasScaler))]
public class CanvasScalerFixer : MonoBehaviour
{
    private void Awake()
    {
        var scaler = GetComponent<CanvasScaler>();
        if (scaler == null) return;

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
    }
}
