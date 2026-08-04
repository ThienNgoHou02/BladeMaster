using System.Collections.Generic;
using UnityEngine;

namespace NeonPulse
{
    /// <summary>Composition root and allocation-free gameplay loop for the rhythm fitness prototype.</summary>
    public sealed class NeonPulseGameController : MonoBehaviour
    {
        private const float TravelBeats = 6f;
        private const float SpawnZ = 50f;
        private const float ResultDelay = 0.6f;
        private const double CountdownDuration = 3d;

        private readonly List<BeatTraveller> activeTravellers = new List<BeatTraveller>(32);
        private IPlayerInputProvider inputProvider;
        private BeatmapConfig beatmap;
        private RuntimeMaterialLibrary materials;
        private BeatTravellerPool travellerPool;
        private HitBurstPool hitBursts;
        private PlayerActionVisuals playerVisuals;
        private NeonHud hud;
        private RhythmScore score;
        private AudioSource audioSource;
        private AudioClip proceduralClip;
        private double dspStartTime;
        private float songDuration;
        private int nextEventIndex;
        private bool runFinished;
        private Vector3 judgementPosition;

        private void Awake()
        {
            ConfigureApplication();

            materials = new RuntimeMaterialLibrary();
            beatmap = BeatmapConfig.CreateRuntimeSample();
            inputProvider = new KeyboardInputProvider();
            score = new RhythmScore();
            score.Changed += OnScoreChanged;
            score.Judged += OnJudged;

            Camera gameplayCamera = NeonWorldBuilder.Build(transform, materials);
            playerVisuals = new PlayerActionVisuals(gameplayCamera, materials);
            travellerPool = new BeatTravellerPool(24, transform, materials);
            hitBursts = new HitBurstPool(8, transform, materials.White);

            GameObject hudObject = new GameObject("Neon Pulse HUD");
            hudObject.transform.SetParent(transform, false);
            hud = hudObject.AddComponent<NeonHud>();
            hud.Build(materials);

            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.loop = false;
            audioSource.volume = 0.58f;
            audioSource.spatialBlend = 0f;
            proceduralClip = RhythmAudioSynth.Create(beatmap.Bpm, beatmap.SongBeats);
            audioSource.clip = proceduralClip;
            songDuration = beatmap.SongBeats * beatmap.SecondsPerBeat;

            StartRun();
        }

        private void Update()
        {
            if (inputProvider == null || beatmap == null)
            {
                return;
            }

            PlayerInputFrame input = inputProvider.ReadInput();
            playerVisuals?.SetHeldInput(input.Duck, input.Jump, input.DodgeLeft, input.DodgeRight);
            playerVisuals?.Tick(Time.deltaTime);
            if (input.Restart)
            {
                StartRun();
                return;
            }

            if (runFinished)
            {
                return;
            }

            float songTime = (float)(AudioSettings.dspTime - dspStartTime);
            SpawnDueEvents(songTime);
            TickTravellers(songTime);

            if (songTime >= 0f)
            {
                ProcessHeldObstacles(input, songTime);
                ProcessInput(input, songTime);
                hud.SetProgress(songTime / songDuration);
            }

            UpdateUpcomingCue(songTime);
            hud.SetCountdown(-songTime);

            if (songTime >= songDuration + ResultDelay)
            {
                FinishRun();
            }
        }

        private void OnDestroy()
        {
            if (score != null)
            {
                score.Changed -= OnScoreChanged;
                score.Judged -= OnJudged;
            }

            if (audioSource != null)
            {
                audioSource.Stop();
            }

            if (proceduralClip != null)
            {
                Destroy(proceduralClip);
            }

            if (beatmap != null)
            {
                Destroy(beatmap);
            }

            materials?.Dispose();
        }

        private void StartRun()
        {
            ReturnAllTravellers();
            nextEventIndex = 0;
            runFinished = false;
            score.Reset();
            hud.ResetRun();

            audioSource.Stop();
            audioSource.time = 0f;
            dspStartTime = AudioSettings.dspTime + CountdownDuration;
            audioSource.PlayScheduled(dspStartTime);
        }

        private void SpawnDueEvents(float songTime)
        {
            IReadOnlyList<BeatmapEvent> chart = beatmap.Events;
            float currentBeat = songTime / beatmap.SecondsPerBeat;
            float visibleThroughBeat = currentBeat + TravelBeats;

            while (nextEventIndex < chart.Count && chart[nextEventIndex].Beat <= visibleThroughBeat)
            {
                BeatTraveller traveller = travellerPool.Rent();
                if (traveller == null)
                {
                    Debug.LogWarning("Neon Pulse traveller pool exhausted. Increase its fixed capacity for denser authored charts.");
                    return;
                }

                BeatmapEvent chartEvent = chart[nextEventIndex];
                float targetTime = chartEvent.Beat * beatmap.SecondsPerBeat;
                float spawnTime = targetTime - TravelBeats * beatmap.SecondsPerBeat;
                traveller.Spawn(chartEvent, targetTime, spawnTime, SpawnZ);
                traveller.Tick(songTime);
                activeTravellers.Add(traveller);
                nextEventIndex++;
            }
        }

        private void TickTravellers(float songTime)
        {
            for (int index = activeTravellers.Count - 1; index >= 0; index--)
            {
                BeatTraveller traveller = activeTravellers[index];
                traveller.Tick(songTime);

                if (!traveller.RequiresHold && songTime > traveller.TargetTime + RhythmScore.GoodWindow)
                {
                    judgementPosition = new Vector3(traveller.transform.position.x, 1.7f, 1.5f);
                    score.RegisterMiss(traveller.Action);
                    activeTravellers.RemoveAt(index);
                    travellerPool.Return(traveller);
                }
            }
        }

        private void ProcessInput(PlayerInputFrame input, float songTime)
        {
            bool hadInput = false;
            bool anyHit = false;
            bool hitBoth = false;

            if (input.BothPunch)
            {
                hadInput = true;
                playerVisuals?.Trigger(GameplayAction.BothPunch);
                hitBoth = TryJudge(GameplayAction.BothPunch, songTime);
                anyHit |= hitBoth;
            }

            if (!hitBoth)
            {
                if (input.LeftPunch)
                {
                    hadInput = true;
                    playerVisuals?.Trigger(GameplayAction.LeftPunch);
                    anyHit |= TryJudge(GameplayAction.LeftPunch, songTime);
                }

                if (input.RightPunch)
                {
                    hadInput = true;
                    playerVisuals?.Trigger(GameplayAction.RightPunch);
                    anyHit |= TryJudge(GameplayAction.RightPunch, songTime);
                }
            }

            if (hadInput && !anyHit)
            {
                hud.ShowMistimedInput(materials);
                playerVisuals?.TriggerFailShake();
            }
        }

        private void ProcessHeldObstacles(PlayerInputFrame input, float songTime)
        {
            for (int index = activeTravellers.Count - 1; index >= 0; index--)
            {
                BeatTraveller traveller = activeTravellers[index];
                if (!traveller.RequiresHold)
                {
                    continue;
                }

                float relativeTime = songTime - traveller.TargetTime;
                if (relativeTime < -GameplayTiming.HoldWindowLead)
                {
                    continue;
                }

                if (!traveller.HoldEvaluationStarted)
                {
                    traveller.BeginHoldEvaluation();
                }

                bool isHeld = IsActionHeld(input, traveller.Action);
                if (!traveller.HoldInputConfirmed)
                {
                    if (isHeld)
                    {
                        traveller.ConfirmHoldInput();
                        hud.ShowHoldConfirmed(traveller.Action, materials);
                    }
                    else if (relativeTime >= -GameplayTiming.HoldInputGrace)
                    {
                        RegisterObstacleMiss(index, traveller);
                    }

                    continue;
                }

                if (!isHeld)
                {
                    RegisterObstacleMiss(index, traveller);
                    continue;
                }

                if (relativeTime >= GameplayTiming.HoldWindowTrail)
                {
                    judgementPosition = new Vector3(traveller.transform.position.x, 1.7f, 1.5f);
                    score.RegisterHit(0f, traveller.Action);
                    activeTravellers.RemoveAt(index);
                    travellerPool.Return(traveller);
                }
            }
        }

        private void RegisterObstacleMiss(int index, BeatTraveller traveller)
        {
            judgementPosition = new Vector3(traveller.transform.position.x, 1.7f, 1.5f);
            score.RegisterMiss(traveller.Action);
            activeTravellers.RemoveAt(index);
            travellerPool.Return(traveller);
        }

        private static bool IsActionHeld(PlayerInputFrame input, GameplayAction action)
        {
            switch (action)
            {
                case GameplayAction.Duck: return input.Duck;
                case GameplayAction.Jump: return input.Jump;
                case GameplayAction.DodgeLeft: return input.DodgeLeft;
                case GameplayAction.DodgeRight: return input.DodgeRight;
                default: return false;
            }
        }

        private void UpdateUpcomingCue(float songTime)
        {
            BeatTraveller closest = null;
            float closestTime = float.MaxValue;
            for (int index = 0; index < activeTravellers.Count; index++)
            {
                BeatTraveller traveller = activeTravellers[index];
                if (traveller.TargetTime < closestTime)
                {
                    closest = traveller;
                    closestTime = traveller.TargetTime;
                }
            }

            if (closest == null)
            {
                hud.HideUpcomingAction();
                return;
            }

            float approachDuration = TravelBeats * beatmap.SecondsPerBeat;
            hud.SetUpcomingAction(closest.Action, closest.TargetTime - songTime, approachDuration, materials);
        }

        private bool TryJudge(GameplayAction requestedAction, float songTime)
        {
            int candidateIndex = -1;
            float bestError = float.MaxValue;

            for (int index = 0; index < activeTravellers.Count; index++)
            {
                BeatTraveller traveller = activeTravellers[index];
                if (traveller.Action != requestedAction)
                {
                    continue;
                }

                float error = Mathf.Abs(songTime - traveller.TargetTime);
                if (error <= RhythmScore.GoodWindow && error < bestError)
                {
                    bestError = error;
                    candidateIndex = index;
                }
            }

            if (candidateIndex < 0)
            {
                return false;
            }

            BeatTraveller candidate = activeTravellers[candidateIndex];
            judgementPosition = new Vector3(candidate.transform.position.x, 1.7f, 1.5f);
            score.RegisterHit(bestError, requestedAction);
            activeTravellers.RemoveAt(candidateIndex);
            travellerPool.Return(candidate);
            return true;
        }

        private void FinishRun()
        {
            if (runFinished)
            {
                return;
            }

            runFinished = true;
            ReturnAllTravellers();
            hud.HideUpcomingAction();
            hud.SetProgress(1f);
            hud.ShowResults(score.Snapshot);
        }

        private void ReturnAllTravellers()
        {
            if (travellerPool == null)
            {
                activeTravellers.Clear();
                return;
            }

            for (int index = activeTravellers.Count - 1; index >= 0; index--)
            {
                travellerPool.Return(activeTravellers[index]);
            }

            activeTravellers.Clear();
        }

        private void OnScoreChanged(ScoreSnapshot snapshot)
        {
            hud?.SetScore(snapshot);
        }

        private void OnJudged(AccuracyGrade grade, GameplayAction action)
        {
            hud?.ShowJudgement(grade, action, materials);
            if (grade == AccuracyGrade.Miss)
            {
                playerVisuals?.TriggerFailShake();
            }

            if (hitBursts == null || materials == null)
            {
                return;
            }

            Color color = GetFeedbackColor(grade, action);
            hitBursts.Play(judgementPosition, color);
        }

        private Color GetFeedbackColor(AccuracyGrade grade, GameplayAction action)
        {
            if (grade == AccuracyGrade.Miss)
            {
                return new Color(1f, 0.08f, 0.18f, 1f);
            }

            if (action == GameplayAction.LeftPunch)
            {
                return materials.CyanColor;
            }

            if (action == GameplayAction.RightPunch)
            {
                return materials.MagentaColor;
            }

            return materials.YellowColor;
        }

        private static void ConfigureApplication()
        {
            Application.targetFrameRate = 60;
            Application.runInBackground = true;
            QualitySettings.vSyncCount = 0;
#if !UNITY_EDITOR
            Screen.SetResolution(1280, 720, FullScreenMode.Windowed);
#endif
        }
    }

    public static class NeonPulseBootstrap
    {
        private static bool created;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            created = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreatePrototype()
        {
            if (created || Object.FindObjectOfType<NeonPulseGameController>() != null)
            {
                return;
            }

            created = true;
            GameObject root = new GameObject("Neon Pulse Fitness");
            root.AddComponent<NeonPulseGameController>();
        }
    }
}
