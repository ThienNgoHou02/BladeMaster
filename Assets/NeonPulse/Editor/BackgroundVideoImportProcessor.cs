#if UNITY_EDITOR
using UnityEditor;

namespace NeonPulse.Editor
{
    /// <summary>Ensures the gameplay background video is decoded consistently across target platforms.</summary>
    internal sealed class BackgroundVideoImportProcessor : AssetPostprocessor
    {
        private const string BackgroundVideoPath = "Assets/_IMPORT/Background/background-4k.mp4";

        [InitializeOnLoadMethod]
        private static void ScheduleImportConfiguration()
        {
            EditorApplication.delayCall += ConfigureExistingAsset;
        }

        private void OnPreprocessAsset()
        {
            if (assetPath != BackgroundVideoPath || assetImporter is not VideoClipImporter importer)
            {
                return;
            }

            ApplySettings(importer);
        }

        private static void ConfigureExistingAsset()
        {
            if (AssetImporter.GetAtPath(BackgroundVideoPath) is not VideoClipImporter importer ||
                !ApplySettings(importer))
            {
                return;
            }

            importer.SaveAndReimport();
        }

        private static bool ApplySettings(VideoClipImporter importer)
        {
            VideoImporterTargetSettings settings = importer.defaultTargetSettings;
            bool needsUpdate = importer.importAudio ||
                               !settings.enableTranscoding ||
                               settings.codec != VideoCodec.H264 ||
                               settings.resizeMode != VideoResizeMode.HalfRes ||
                               settings.aspectRatio != VideoEncodeAspectRatio.NoScaling ||
                               settings.bitrateMode != VideoBitrateMode.Medium;
            if (!needsUpdate)
            {
                return false;
            }

            importer.importAudio = false;
            settings.enableTranscoding = true;
            settings.codec = VideoCodec.H264;
            settings.resizeMode = VideoResizeMode.HalfRes;
            settings.aspectRatio = VideoEncodeAspectRatio.NoScaling;
            settings.bitrateMode = VideoBitrateMode.Medium;
            importer.defaultTargetSettings = settings;
            return true;
        }
    }
}
#endif
