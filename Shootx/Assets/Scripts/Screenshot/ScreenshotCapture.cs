using UnityEngine;
using System.Collections;
using System.IO;

public class ScreenshotCapture : MonoBehaviour
{
    [SerializeField] private float captureInterval = 2f;
    [SerializeField] private int superSize = 4;

    private string folder;

    private void Start()
    {
        folder = Path.Combine(Application.dataPath, "../Screenshots");

        if (!Directory.Exists(folder))
            Directory.CreateDirectory(folder);

        StartCoroutine(CaptureRoutine());
    }

    private IEnumerator CaptureRoutine()
    {
        while (true)
        {
            yield return new WaitForEndOfFrame();

            string file = Path.Combine(
                folder,
                $"Screenshot_{System.DateTime.Now:yyyyMMdd_HHmmss_fff}.png");

            ScreenCapture.CaptureScreenshot(file, superSize);

            Debug.Log($"Screenshot saved: {file}");

            yield return new WaitForSeconds(captureInterval);
        }
    }
}