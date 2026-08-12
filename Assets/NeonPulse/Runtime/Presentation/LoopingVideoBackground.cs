using UnityEngine;
using UnityEngine.Video;

namespace NeonPulse
{
    /// <summary>Plays a muted looping video behind the gameplay world.</summary>
    [RequireComponent(typeof(VideoPlayer))]
    public sealed class LoopingVideoBackground : MonoBehaviour
    {
        private VideoPlayer videoPlayer;
        private bool initialized;

        /// <summary>Creates a camera background only when both dependencies are valid.</summary>
        public static LoopingVideoBackground Create(Transform parent, Camera targetCamera, VideoClip clip)
        {
            if (targetCamera == null || clip == null)
            {
                return null;
            }

            GameObject backgroundObject = new GameObject("Looping Video Background");
            backgroundObject.transform.SetParent(parent, false);

            LoopingVideoBackground background = backgroundObject.AddComponent<LoopingVideoBackground>();
            background.Initialize(targetCamera, clip);
            return background;
        }

        private void Awake()
        {
            videoPlayer = GetComponent<VideoPlayer>();
        }

        private void Initialize(Camera targetCamera, VideoClip clip)
        {
            videoPlayer.playOnAwake = false;
            videoPlayer.waitForFirstFrame = true;
            videoPlayer.isLooping = true;
            videoPlayer.skipOnDrop = true;
            videoPlayer.source = VideoSource.VideoClip;
            videoPlayer.clip = clip;
            videoPlayer.renderMode = VideoRenderMode.CameraFarPlane;
            videoPlayer.targetCamera = targetCamera;
            videoPlayer.aspectRatio = VideoAspectRatio.FitOutside;
            videoPlayer.targetCameraAlpha = 1f;
            videoPlayer.audioOutputMode = VideoAudioOutputMode.None;
            videoPlayer.prepareCompleted += OnPrepareCompleted;
            videoPlayer.errorReceived += OnErrorReceived;
            initialized = true;
            videoPlayer.Prepare();
        }

        private void OnEnable()
        {
            if (!initialized || videoPlayer == null)
            {
                return;
            }

            if (videoPlayer.isPrepared)
            {
                videoPlayer.Play();
                return;
            }

            videoPlayer.Prepare();
        }

        private void OnDisable()
        {
            if (videoPlayer != null)
            {
                videoPlayer.Stop();
            }
        }

        private void OnDestroy()
        {
            if (videoPlayer == null)
            {
                return;
            }

            videoPlayer.prepareCompleted -= OnPrepareCompleted;
            videoPlayer.errorReceived -= OnErrorReceived;
        }

        private void OnPrepareCompleted(VideoPlayer preparedPlayer)
        {
            if (isActiveAndEnabled)
            {
                preparedPlayer.Play();
            }
        }

        private void OnErrorReceived(VideoPlayer failedPlayer, string message)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError($"Background video failed: {message}", failedPlayer);
#endif
        }
    }
}
