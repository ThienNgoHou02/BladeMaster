using System.Collections.Generic;
using UnityEngine;

namespace NeonPulse
{
    /// <summary>Composition root and allocation-free gameplay loop for the rhythm fitness prototype.</summary>
    public sealed class NeonPulseGameController : MonoBehaviour
    {
        private const float JudgementStepSurfaceY = 0.34f;

        private readonly List<BeatTraveller> activePunchTargets = new List<BeatTraveller>(24);
        private readonly List<BeatTraveller> activeObstacles = new List<BeatTraveller>(12);
        private readonly List<RhythmLaneTile> activeRhythmTiles = new List<RhythmLaneTile>(32);
        private IPlayerInputProvider inputProvider;
        private NeonPulseGameConfig config;
        private bool ownsConfig;
        private RuntimeMaterialLibrary materials;
        private BeatTravellerPool punchTargetPool;
        private BeatTravellerPool obstaclePool;
        private RhythmLaneTilePool rhythmTilePool;
        private HitBurstPool hitBursts;
        private PlayerActionVisuals playerVisuals;
        private JudgementLineFeedback judgementLineFeedback;
        private NeonHud hud;
        private RhythmScore score;
        private AudioSource audioSource;
        private AudioClip proceduralClip;
        private double dspStartTime;
        private float songDuration;
        private int nextPunchEventIndex;
        private int nextObstacleEventIndex;
        private int nextRhythmTileIndex;
        private uint tileLaneRandomState;
        private bool runFinished;
        private Vector3 judgementPosition;
        private Color rhythmTileFeedbackColor;

        private void Awake()
        {
            ConfigureApplication();

            config = NeonPulseGameConfig.LoadRuntime(out ownsConfig);
            materials = new RuntimeMaterialLibrary(config.Visuals);
            inputProvider = new KeyboardInputProvider(config.Input);
            score = new RhythmScore(config.Rhythm, config.Scoring);
            score.Changed += OnScoreChanged;
            score.Judged += OnJudged;

            Camera gameplayCamera = NeonWorldBuilder.Build(transform, materials, config, out judgementLineFeedback);
            playerVisuals = new PlayerActionVisuals(gameplayCamera, materials, config);
            int punchPoolCapacity = Mathf.Max(8, config.Visuals.TravellerPoolCapacity * 2 / 3);
            int obstaclePoolCapacity = Mathf.Max(8, config.Visuals.TravellerPoolCapacity - punchPoolCapacity);
            punchTargetPool = new BeatTravellerPool(punchPoolCapacity, transform, materials, config, "Punch Target Pool");
            obstaclePool = new BeatTravellerPool(obstaclePoolCapacity, transform, materials, config, "Obstacle Door Pool");
            rhythmTilePool = new RhythmLaneTilePool(config.Visuals.RhythmTilePoolCapacity, transform, materials, config);
            hitBursts = new HitBurstPool(config.Visuals.HitVfxPoolCapacity, transform, materials.White, config.Visuals.HitParticleCount);

            GameObject hudObject = new GameObject("Neon Pulse HUD");
            hudObject.transform.SetParent(transform, false);
            hud = hudObject.AddComponent<NeonHud>();
            hud.Build(materials, config);

            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.loop = false;
            audioSource.volume = config.Visuals.AudioVolume;
            audioSource.spatialBlend = 0f;
            proceduralClip = RhythmAudioSynth.Create(config.Rhythm.Bpm, config.Rhythm.SongBeats);
            audioSource.clip = proceduralClip;
            songDuration = config.Rhythm.SongBeats * config.Rhythm.SecondsPerBeat;

            StartRun();
        }

        private void Update()
        {
            if (inputProvider == null || config == null)
            {
                return;
            }

            PlayerInputFrame manualInput = inputProvider.ReadInput();
            if (manualInput.Restart)
            {
                StartRun();
                return;
            }

            if (runFinished)
            {
                playerVisuals?.SetHeldInput(false, false, false, false);
                playerVisuals?.Tick(Time.deltaTime);
                return;
            }

            float songTime = (float)(AudioSettings.dspTime - dspStartTime);
            float beatPhase = Mathf.Repeat(songTime / config.Rhythm.SecondsPerBeat, 1f);
            materials?.SetBeatPulse(Mathf.Pow(1f - beatPhase, 4f));
            judgementLineFeedback?.Tick(Time.deltaTime);
            SpawnDuePunchTargets(songTime);
            SpawnDueObstacles(songTime);
            SpawnDueRhythmTiles(songTime);
            TickPunchTargets(songTime);
            TickObstacles(songTime);
            TickRhythmTiles(songTime);

            PlayerInputFrame gameplayInput = config.AutoPlay
                ? CreateAutoPlayInput(songTime)
                : manualInput;
            playerVisuals?.SetHeldInput(gameplayInput.Duck, gameplayInput.Jump, gameplayInput.DodgeLeft, gameplayInput.DodgeRight);
            playerVisuals?.Tick(Time.deltaTime);

            if (songTime >= 0f)
            {
                ProcessHeldObstacles(gameplayInput, songTime);
                ProcessInput(gameplayInput, songTime);
                hud.SetProgress(songTime / songDuration);
            }

            UpdateUpcomingCue(songTime);
            hud.SetCountdown(-songTime);

            if (songTime >= songDuration + config.Rhythm.ResultDelay)
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

            if (ownsConfig && config != null)
            {
                Destroy(config);
            }

            materials?.Dispose();
        }

        private void StartRun()
        {
            ReturnAllTravellers();
            nextPunchEventIndex = 0;
            nextObstacleEventIndex = 0;
            nextRhythmTileIndex = 0;
            tileLaneRandomState = unchecked((uint)System.Environment.TickCount) ^ unchecked((uint)GetInstanceID());
            tileLaneRandomState |= 1u;
            runFinished = false;
            score.Reset();
            hud.ResetRun();

            audioSource.Stop();
            audioSource.time = 0f;
            dspStartTime = AudioSettings.dspTime + config.Rhythm.CountdownDuration;
            audioSource.PlayScheduled(dspStartTime);
        }

        private void SpawnDuePunchTargets(float songTime)
        {
            nextPunchEventIndex = SpawnDueGameplayEvents(
                config.PunchEvents,
                nextPunchEventIndex,
                punchTargetPool,
                activePunchTargets,
                songTime,
                "Punch Target Pool exhausted. Increase Traveller Pool Capacity.");
        }

        private void SpawnDueObstacles(float songTime)
        {
            nextObstacleEventIndex = SpawnDueGameplayEvents(
                config.ObstacleEvents,
                nextObstacleEventIndex,
                obstaclePool,
                activeObstacles,
                songTime,
                "Obstacle Door Pool exhausted. Increase Traveller Pool Capacity.");
        }

        private int SpawnDueGameplayEvents(
            IReadOnlyList<BeatmapEvent> chart,
            int nextIndex,
            BeatTravellerPool pool,
            List<BeatTraveller> activeList,
            float songTime,
            string exhaustedWarning)
        {
            float currentBeat = songTime / config.Rhythm.SecondsPerBeat;
            float visibleThroughBeat = currentBeat + config.Rhythm.TravelBeats;
            while (nextIndex < chart.Count && chart[nextIndex].Beat <= visibleThroughBeat)
            {
                BeatmapEvent chartEvent = chart[nextIndex];
                if (config.Rhythm.GameplayEventOverlapsTileWave(chartEvent.Beat))
                {
                    nextIndex++;
                    continue;
                }

                BeatTraveller traveller = pool.Rent();
                if (traveller == null)
                {
                    Debug.LogWarning(exhaustedWarning);
                    return nextIndex;
                }

                float targetTime = chartEvent.Beat * config.Rhythm.SecondsPerBeat;
                float spawnTime = targetTime - config.Rhythm.TravelBeats * config.Rhythm.SecondsPerBeat;
                traveller.Spawn(chartEvent, targetTime, spawnTime, config.Rhythm.SpawnZ);
                traveller.Tick(songTime);
                activeList.Add(traveller);
                nextIndex++;
            }

            return nextIndex;
        }

        private void SpawnDueRhythmTiles(float songTime)
        {
            IReadOnlyList<RhythmTileEvent> chart = config.RhythmTileEvents;
            float currentBeat = songTime / config.Rhythm.SecondsPerBeat;
            float visibleThroughBeat = currentBeat + config.Rhythm.TravelBeats;
            while (nextRhythmTileIndex < chart.Count && chart[nextRhythmTileIndex].Beat <= visibleThroughBeat)
            {
                int secondTileIndex = nextRhythmTileIndex + 1;
                RhythmTileEvent firstTile = chart[nextRhythmTileIndex];
                if (secondTileIndex >= chart.Count || !Mathf.Approximately(firstTile.Beat, chart[secondTileIndex].Beat))
                {
                    Debug.LogWarning("Rhythm tiles must be authored as left/right pairs on the same beat.");
                    nextRhythmTileIndex++;
                    continue;
                }

                RhythmTileEvent secondTile = chart[secondTileIndex];
                if (!config.Rhythm.ContainsTileBeat(firstTile.Beat) || !config.Rhythm.ContainsTileBeat(secondTile.Beat))
                {
                    nextRhythmTileIndex += 2;
                    continue;
                }

                if (rhythmTilePool.AvailableCount < 2)
                {
                    Debug.LogWarning("Rhythm tile pool exhausted. A foot pair was delayed to avoid spawning only one foot.");
                    return;
                }

                firstTile.Lane = NextRandomTileLane(0);
                secondTile.Lane = NextRandomTileLane(2);
                SpawnRhythmTile(firstTile, songTime);
                SpawnRhythmTile(secondTile, songTime);
                nextRhythmTileIndex += 2;
            }
        }

        private int NextRandomTileLane(int firstLane)
        {
            uint value = tileLaneRandomState;
            value ^= value << 13;
            value ^= value >> 17;
            value ^= value << 5;
            tileLaneRandomState = value;
            return firstLane + (int)(value & 1u);
        }

        private void SpawnRhythmTile(RhythmTileEvent tileEvent, float songTime)
        {
            RhythmLaneTile rhythmTile = rhythmTilePool.Rent();
            float targetTime = tileEvent.Beat * config.Rhythm.SecondsPerBeat;
            float spawnTime = targetTime - config.Rhythm.TravelBeats * config.Rhythm.SecondsPerBeat;
            rhythmTile.Spawn(tileEvent, targetTime, spawnTime, config.Rhythm.SpawnZ);
            rhythmTile.Tick(songTime);
            activeRhythmTiles.Add(rhythmTile);
        }

        private void TickRhythmTiles(float songTime)
        {
            for (int index = activeRhythmTiles.Count - 1; index >= 0; index--)
            {
                RhythmLaneTile tile = activeRhythmTiles[index];
                tile.Tick(songTime);
                if (!tile.ReachedJudgementLine)
                {
                    continue;
                }

                judgementPosition = new Vector3(tile.transform.position.x, JudgementStepSurfaceY, config.Rhythm.HitZ);
                rhythmTileFeedbackColor = tile.FeedbackColor;
                score.RegisterHit(0f, GameplayAction.RhythmTile);
                activeRhythmTiles.RemoveAt(index);
                rhythmTilePool.Return(tile);
            }
        }

        private void TickPunchTargets(float songTime)
        {
            for (int index = activePunchTargets.Count - 1; index >= 0; index--)
            {
                BeatTraveller traveller = activePunchTargets[index];
                traveller.Tick(songTime);

                if (!config.AutoPlay && songTime > traveller.TargetTime + score.GoodWindow)
                {
                    judgementPosition = CreateJudgementPosition(traveller);
                    score.RegisterMiss(traveller.Action);
                    activePunchTargets.RemoveAt(index);
                    punchTargetPool.Return(traveller);
                }
            }
        }

        private void TickObstacles(float songTime)
        {
            for (int index = 0; index < activeObstacles.Count; index++)
            {
                activeObstacles[index].Tick(songTime);
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
            for (int index = activeObstacles.Count - 1; index >= 0; index--)
            {
                BeatTraveller traveller = activeObstacles[index];

                float relativeTime = songTime - traveller.TargetTime;
                if (relativeTime < -config.Rhythm.HoldWindowLead)
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
                    else if (relativeTime >= -config.Rhythm.HoldInputGrace)
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

                if (relativeTime >= config.Rhythm.HoldWindowTrail)
                {
                    judgementPosition = CreateJudgementPosition(traveller);
                    score.RegisterHit(0f, traveller.Action);
                    activeObstacles.RemoveAt(index);
                    obstaclePool.Return(traveller);
                }
            }
        }

        private void RegisterObstacleMiss(int index, BeatTraveller traveller)
        {
            judgementPosition = CreateJudgementPosition(traveller);
            score.RegisterMiss(traveller.Action);
            activeObstacles.RemoveAt(index);
            obstaclePool.Return(traveller);
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

        private PlayerInputFrame CreateAutoPlayInput(float songTime)
        {
            bool leftPunch = false;
            bool rightPunch = false;
            bool bothPunch = false;
            bool duck = false;
            bool jump = false;
            bool dodgeLeft = false;
            bool dodgeRight = false;

            for (int index = 0; index < activePunchTargets.Count; index++)
            {
                BeatTraveller traveller = activePunchTargets[index];
                float relativeTime = songTime - traveller.TargetTime;
                if (relativeTime < 0f)
                {
                    continue;
                }

                switch (traveller.Action)
                {
                    case GameplayAction.LeftPunch: leftPunch = true; break;
                    case GameplayAction.RightPunch: rightPunch = true; break;
                    case GameplayAction.BothPunch: bothPunch = true; break;
                }
            }

            for (int index = 0; index < activeObstacles.Count; index++)
            {
                BeatTraveller traveller = activeObstacles[index];
                float relativeTime = songTime - traveller.TargetTime;
                if (relativeTime < -config.Rhythm.HoldWindowLead)
                {
                    continue;
                }

                switch (traveller.Action)
                {
                    case GameplayAction.Duck: duck = true; break;
                    case GameplayAction.Jump: jump = true; break;
                    case GameplayAction.DodgeLeft: dodgeLeft = true; break;
                    case GameplayAction.DodgeRight: dodgeRight = true; break;
                }
            }

            return new PlayerInputFrame(leftPunch, rightPunch, bothPunch, duck, jump, dodgeLeft, dodgeRight, false);
        }

        private void UpdateUpcomingCue(float songTime)
        {
            BeatTraveller closest = null;
            float closestTime = float.MaxValue;
            for (int index = 0; index < activePunchTargets.Count; index++)
            {
                BeatTraveller traveller = activePunchTargets[index];
                if (traveller.TargetTime < closestTime)
                {
                    closest = traveller;
                    closestTime = traveller.TargetTime;
                }
            }

            for (int index = 0; index < activeObstacles.Count; index++)
            {
                BeatTraveller traveller = activeObstacles[index];
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

            float approachDuration = config.Rhythm.TravelBeats * config.Rhythm.SecondsPerBeat;
            hud.SetUpcomingAction(closest.Action, closest.TargetTime - songTime, approachDuration, materials);
        }

        private bool TryJudge(GameplayAction requestedAction, float songTime)
        {
            int candidateIndex = -1;
            float bestError = float.MaxValue;

            for (int index = 0; index < activePunchTargets.Count; index++)
            {
                BeatTraveller traveller = activePunchTargets[index];
                if (traveller.Action != requestedAction)
                {
                    continue;
                }

                float error = Mathf.Abs(songTime - traveller.TargetTime);
                bool insideTimingWindow = error <= score.GoodWindow;
                bool autoPlayReachedTarget = config.AutoPlay && songTime >= traveller.TargetTime;
                if ((insideTimingWindow || autoPlayReachedTarget) && error < bestError)
                {
                    bestError = error;
                    candidateIndex = index;
                }
            }

            if (candidateIndex < 0)
            {
                return false;
            }

            BeatTraveller candidate = activePunchTargets[candidateIndex];
            judgementPosition = CreateJudgementPosition(candidate);
            score.RegisterHit(config.AutoPlay ? 0f : bestError, requestedAction);
            activePunchTargets.RemoveAt(candidateIndex);
            punchTargetPool.Return(candidate);
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
            if (punchTargetPool != null)
            {
                for (int index = activePunchTargets.Count - 1; index >= 0; index--)
                {
                    punchTargetPool.Return(activePunchTargets[index]);
                }
            }

            activePunchTargets.Clear();

            if (obstaclePool != null)
            {
                for (int index = activeObstacles.Count - 1; index >= 0; index--)
                {
                    obstaclePool.Return(activeObstacles[index]);
                }
            }

            activeObstacles.Clear();

            if (rhythmTilePool != null)
            {
                for (int index = activeRhythmTiles.Count - 1; index >= 0; index--)
                {
                    rhythmTilePool.Return(activeRhythmTiles[index]);
                }
            }

            activeRhythmTiles.Clear();
        }

        private void OnScoreChanged(ScoreSnapshot snapshot)
        {
            hud?.SetScore(snapshot);
        }

        private void OnJudged(AccuracyGrade grade, GameplayAction action)
        {
            hud?.ShowJudgement(grade, action, score.Snapshot, materials);
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
            if (action == GameplayAction.RhythmTile)
            {
                judgementLineFeedback?.Pulse(judgementPosition.x);
                return;
            }

            if (grade != AccuracyGrade.Miss)
            {
                judgementLineFeedback?.Pulse(judgementPosition.x);
                hitBursts.Play(new Vector3(judgementPosition.x, JudgementStepSurfaceY, config.Rhythm.HitZ), color);
            }
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

            if (action == GameplayAction.RhythmTile)
            {
                return rhythmTileFeedbackColor;
            }

            return materials.YellowColor;
        }

        private Vector3 CreateJudgementPosition(BeatTraveller traveller)
        {
            return new Vector3(traveller.transform.position.x, 1.7f, config.Rhythm.HitZ);
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
