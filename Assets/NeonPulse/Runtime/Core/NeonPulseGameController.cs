using System.Collections.Generic;
using UnityEngine;

namespace NeonPulse
{
    /// <summary>Composition root and allocation-free gameplay loop for the rhythm fitness prototype.</summary>
    public sealed class NeonPulseGameController : MonoBehaviour
    {
        private const float JudgementStepSurfaceY = 0.34f;
        private const float RhythmTileVfxSurfaceY = 0.42f;
        private const float CombatVfxFrontOffset = 0.65f;
        private const float DualTargetOffsetX = 1.25f;

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
        private SlashDebrisPool slashDebris;
        private PrefabParticleVfxPool punchHitVfx;
        private PrefabParticleVfxPool overheadClapHitVfx;
        private PrefabParticleVfxPool slashHitVfx;
        private PrefabParticleVfxPool rhythmTileHitVfx;
        private ScreenFlashFeedback screenFlash;
        private PlayerActionVisuals playerVisuals;
        private NeonMotionFeedback motionFeedback;
        private JudgementLineFeedback judgementLineFeedback;
        private NeonHud hud;
        private RhythmScore score;
        private AudioSource audioSource;
        private AudioClip proceduralClip;
        private double dspStartTime;
        private float songDuration;
        private NeonPulseLevelRunPlan runPlan;
        private int nextPunchEventIndex;
        private int nextObstacleEventIndex;
        private int nextRhythmTileIndex;
        private uint spawnRandomState;
        private uint vfxRandomState;
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
            motionFeedback = new NeonMotionFeedback(transform, gameplayCamera, materials);
            int punchPoolCapacity = Mathf.Max(8, config.Visuals.TravellerPoolCapacity * 2 / 3);
            int obstaclePoolCapacity = Mathf.Max(8, config.Visuals.TravellerPoolCapacity - punchPoolCapacity);
            punchTargetPool = new BeatTravellerPool(punchPoolCapacity, transform, materials, config, "Punch Target Pool");
            obstaclePool = new BeatTravellerPool(obstaclePoolCapacity, transform, materials, config, "Obstacle Door Pool");
            rhythmTilePool = new RhythmLaneTilePool(config.Visuals.RhythmTilePoolCapacity, transform, materials, config);
            hitBursts = new HitBurstPool(config.Visuals.HitVfxPoolCapacity, transform, materials.White, config.Visuals.HitParticleCount);
            slashDebris = new SlashDebrisPool(config.Visuals.HitVfxPoolCapacity, transform, materials);
            punchHitVfx = new PrefabParticleVfxPool(
                config.Visuals.HitVfxPoolCapacity,
                transform,
                config.Visuals.PunchHitVfxPrefab,
                "Punch Hit VFX Pool");
            overheadClapHitVfx = new PrefabParticleVfxPool(
                config.Visuals.HitVfxPoolCapacity,
                transform,
                config.Visuals.OverheadClapHitVfxPrefab,
                "Overhead Clap Hit VFX Pool");
            slashHitVfx = new PrefabParticleVfxPool(
                config.Visuals.HitVfxPoolCapacity,
                transform,
                config.Visuals.SlashHitVfxPrefab,
                "Slash Hit VFX Pool");
            rhythmTileHitVfx = new PrefabParticleVfxPool(
                config.Visuals.HitVfxPoolCapacity,
                transform,
                config.Visuals.RhythmTileHitVfxPrefab,
                "Rhythm Tile Hit VFX Pool");
            screenFlash = new ScreenFlashFeedback(
                transform,
                config.Visuals.ScreenFlashDuration,
                config.Visuals.ScreenFlashIntensity);

            GameObject hudObject = new GameObject("Neon Pulse HUD");
            hudObject.transform.SetParent(transform, false);
            hud = hudObject.AddComponent<NeonHud>();
            hud.Build(materials, config);

            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.loop = false;
            audioSource.volume = config.Visuals.AudioVolume;
            audioSource.spatialBlend = 0f;
            proceduralClip = RhythmAudioSynth.Create(config.Rhythm.Bpm, GetAudioBeatCount());
            audioSource.clip = proceduralClip;

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

            slashDebris?.Tick(Time.deltaTime);
            screenFlash?.Tick(Time.unscaledDeltaTime);

            float songTime = (float)(AudioSettings.dspTime - dspStartTime);
            float flySpeed = runFinished || runPlan == null ? 0f : runPlan.GetFlySpeed(songTime);
            motionFeedback?.Tick(Time.deltaTime, flySpeed);

            if (runFinished)
            {
                playerVisuals?.SetHeldInput(false, false, false, false);
                playerVisuals?.Tick(Time.deltaTime);
                return;
            }

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
            bool jumpInMotionWindow = IsJumpInMotionWindow(gameplayInput.Jump, songTime);
            playerVisuals?.SetHeldInput(gameplayInput.Duck, jumpInMotionWindow, gameplayInput.DodgeLeft, gameplayInput.DodgeRight);
            playerVisuals?.Tick(Time.deltaTime);

            if (songTime >= 0f)
            {
                ProcessHeldObstacles(gameplayInput, songTime);
                ProcessInput(gameplayInput, songTime);
            }

            UpdateUpcomingCue(songTime);
            hud.SetCountdown(-songTime);
            UpdatePhasePresentation(songTime);

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
            slashDebris?.Clear();
            punchHitVfx?.Clear();
            overheadClapHitVfx?.Clear();
            slashHitVfx?.Clear();
            rhythmTileHitVfx?.Clear();
            screenFlash?.Clear();
            nextPunchEventIndex = 0;
            nextObstacleEventIndex = 0;
            nextRhythmTileIndex = 0;
            spawnRandomState = unchecked((uint)System.Environment.TickCount) ^ unchecked((uint)GetInstanceID());
            spawnRandomState |= 1u;
            vfxRandomState = spawnRandomState ^ 0xA511E9B3u;
            vfxRandomState |= 1u;
            runFinished = false;
            score.Reset();
            hud.ResetRun();

            runPlan = NeonPulseLevelRunPlan.Build(config.LevelDefinition, config, ref spawnRandomState);
            songDuration = runPlan.Duration;

            audioSource.Stop();
            audioSource.time = 0f;
            dspStartTime = AudioSettings.dspTime + config.Rhythm.CountdownDuration;
            audioSource.PlayScheduled(dspStartTime);
        }

        private void SpawnDuePunchTargets(float songTime)
        {
            nextPunchEventIndex = SpawnDueGameplayEvents(runPlan.TargetEvents, nextPunchEventIndex, punchTargetPool,
                activePunchTargets, songTime, "Punch Target Pool exhausted. Increase Traveller Pool Capacity.");
        }

        private void SpawnDueObstacles(float songTime)
        {
            nextObstacleEventIndex = SpawnDueGameplayEvents(runPlan.ObstacleEvents, nextObstacleEventIndex, obstaclePool,
                activeObstacles, songTime, "Obstacle Door Pool exhausted. Increase Traveller Pool Capacity.");
        }

        private int SpawnDueGameplayEvents(
            IReadOnlyList<PlannedGameplayEvent> chart,
            int nextIndex,
            BeatTravellerPool pool,
            List<BeatTraveller> activeList,
            float songTime,
            string exhaustedWarning)
        {
            while (nextIndex < chart.Count && chart[nextIndex].SpawnTime <= songTime)
            {
                BeatTraveller traveller = pool.Rent();
                if (traveller == null)
                {
                    Debug.LogWarning(exhaustedWarning);
                    return nextIndex;
                }

                PlannedGameplayEvent plannedEvent = chart[nextIndex];
                traveller.Spawn(plannedEvent.Event, plannedEvent.TargetTime, plannedEvent.SpawnTime,
                    config.Rhythm.SpawnZ, plannedEvent.UseSlashVisual);
                traveller.Tick(songTime);
                activeList.Add(traveller);
                nextIndex++;
            }

            return nextIndex;
        }

        private void SpawnDueRhythmTiles(float songTime)
        {
            IReadOnlyList<PlannedRhythmTileEvent> chart = runPlan.RhythmTileEvents;
            while (nextRhythmTileIndex < chart.Count && chart[nextRhythmTileIndex].SpawnTime <= songTime)
            {
                int secondTileIndex = nextRhythmTileIndex + 1;
                if (secondTileIndex >= chart.Count ||
                    !Mathf.Approximately(chart[nextRhythmTileIndex].TargetTime, chart[secondTileIndex].TargetTime))
                {
                    Debug.LogWarning("Rhythm tiles must be authored as left/right pairs on the same beat.");
                    nextRhythmTileIndex++;
                    continue;
                }

                if (rhythmTilePool.AvailableCount < 2)
                {
                    Debug.LogWarning("Rhythm tile pool exhausted. A foot pair was delayed to avoid spawning only one foot.");
                    return;
                }

                SpawnRhythmTile(chart[nextRhythmTileIndex], songTime);
                SpawnRhythmTile(chart[secondTileIndex], songTime);
                nextRhythmTileIndex += 2;
            }
        }

        private void SpawnRhythmTile(PlannedRhythmTileEvent tileEvent, float songTime)
        {
            RhythmLaneTile rhythmTile = rhythmTilePool.Rent();
            rhythmTile.Spawn(tileEvent.Event, tileEvent.TargetTime, tileEvent.SpawnTime, config.Rhythm.SpawnZ);
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

            if (input.OverheadClap)
            {
                hadInput = true;
                bool hitClap = TryJudge(GameplayAction.OverheadClap, songTime, out _);
                playerVisuals?.Trigger(GameplayAction.OverheadClap);
                anyHit |= hitClap;
            }

            if (input.BothPunch)
            {
                hadInput = true;
                hitBoth = TryJudge(GameplayAction.BothPunch, songTime, out float slashDirection);
                playerVisuals?.Trigger(GameplayAction.BothPunch, slashDirection);
                anyHit |= hitBoth;
            }

            if (!hitBoth)
            {
                if (input.LeftPunch)
                {
                    hadInput = true;
                    bool hitLeft = TryJudge(GameplayAction.LeftPunch, songTime, out float slashDirection);
                    playerVisuals?.Trigger(GameplayAction.LeftPunch, slashDirection);
                    anyHit |= hitLeft;
                }

                if (input.RightPunch)
                {
                    hadInput = true;
                    bool hitRight = TryJudge(GameplayAction.RightPunch, songTime, out float slashDirection);
                    playerVisuals?.Trigger(GameplayAction.RightPunch, slashDirection);
                    anyHit |= hitRight;
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

        private bool IsJumpInMotionWindow(bool jumpHeld, float songTime)
        {
            if (!jumpHeld || playerVisuals == null)
            {
                return false;
            }

            float jumpApexOffset = CalculateObstacleCameraCrossingDelay();
            float jumpLeadTime = playerVisuals.JumpLeadTime;
            for (int index = 0; index < activeObstacles.Count; index++)
            {
                BeatTraveller traveller = activeObstacles[index];
                if (traveller.Action != GameplayAction.Jump)
                {
                    continue;
                }

                float relativeTime = songTime - traveller.TargetTime;
                if (relativeTime >= jumpApexOffset - jumpLeadTime &&
                    relativeTime <= jumpApexOffset + jumpLeadTime)
                {
                    return true;
                }
            }

            return false;
        }

        private float CalculateObstacleCameraCrossingDelay()
        {
            float postTargetDistance = config.Rhythm.HitZ - config.Rhythm.DespawnZ;
            if (postTargetDistance <= 0.001f)
            {
                return 0f;
            }

            float distanceFromHitLineToCamera = Mathf.Min(
                config.CameraFeel.DistanceToJudgementLine,
                postTargetDistance);
            return distanceFromHitLineToCamera / postTargetDistance * BeatTraveller.PostTargetTravelDuration;
        }

        private PlayerInputFrame CreateAutoPlayInput(float songTime)
        {
            bool leftPunch = false;
            bool rightPunch = false;
            bool bothPunch = false;
            bool overheadClap = false;
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
                    case GameplayAction.OverheadClap: overheadClap = true; break;
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

            return new PlayerInputFrame(leftPunch, rightPunch, bothPunch, overheadClap, duck, jump, dodgeLeft, dodgeRight, false);
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

            playerVisuals?.SetCombatMode(closest.UsesSlashVisual ? CombatGameplayMode.Slash : CombatGameplayMode.Punch);
            playerVisuals?.SetHandsVisible(true);
            hud.SetActionMode(closest.UsesSlashVisual ? CombatGameplayMode.Slash : CombatGameplayMode.Punch);
            hud.SetUpcomingAction(closest.Action, closest.TargetTime - songTime,
                Mathf.Max(0.01f, closest.TargetTime - closest.SpawnTime), materials);
        }

        private bool TryJudge(GameplayAction requestedAction, float songTime, out float slashDirection)
        {
            slashDirection = 0f;
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
            slashDirection = candidate.SlashDirection;
            playerVisuals?.SetCombatMode(candidate.UsesSlashVisual ? CombatGameplayMode.Slash : CombatGameplayMode.Punch);
            playerVisuals?.SetHandsVisible(true);
            if (candidate.UsesSlashVisual)
            {
                slashDebris?.PlaySlash(judgementPosition, requestedAction, slashDirection);
            }
            else
            {
                slashDebris?.PlayPunch(judgementPosition, requestedAction);
            }
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

        private void UpdatePhasePresentation(float songTime)
        {
            if (runPlan == null || runPlan.PhaseCount == 0)
            {
                return;
            }

            if (runPlan.TryGetPhase(songTime, out NeonPulseLevelPhase phase, out _))
            {
                // Mixed phases switch punch/slash presentation from the closest spawned cue.
                if (phase.Action == LevelPhaseAction.RandomMixed)
                {
                    return;
                }

                CombatGameplayMode mode = phase.Action == LevelPhaseAction.SlashObjects
                    ? CombatGameplayMode.Slash
                    : CombatGameplayMode.Punch;
                playerVisuals?.SetHandsVisible(phase.Action != LevelPhaseAction.RhythmTiles);
                playerVisuals?.SetCombatMode(mode);
                hud.SetActionMode(mode);
                return;
            }
        }

        private int GetAudioBeatCount()
        {
            NeonPulseLevelDefinition level = config != null ? config.LevelDefinition : null;
            if (level == null)
            {
                return config != null ? config.Rhythm.SongBeats : 64;
            }

            float duration = 0f;
            for (int index = 0; index < level.Phases.Count; index++)
            {
                NeonPulseLevelPhase phase = level.Phases[index];
                if (phase != null)
                {
                    duration += phase.DurationSeconds;
                }

                if (index < level.Phases.Count - 1)
                {
                    duration += level.PhaseTransitionRestSeconds;
                }
            }

            return Mathf.Max(4, Mathf.CeilToInt(duration / config.Rhythm.SecondsPerBeat));
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
                Vector3 rhythmVfxPosition = new Vector3(
                    judgementPosition.x,
                    RhythmTileVfxSurfaceY,
                    judgementPosition.z);
                rhythmTileHitVfx?.Play(rhythmVfxPosition, Quaternion.identity);
                playerVisuals?.TriggerRhythmTileImpactShake();
                screenFlash?.Play(color, 0.6f);
                judgementLineFeedback?.Highlight(judgementPosition.x, color);
                return;
            }

            if (grade != AccuracyGrade.Miss)
            {
                if (action == GameplayAction.LeftPunch || action == GameplayAction.RightPunch ||
                    action == GameplayAction.BothPunch || action == GameplayAction.OverheadClap)
                {
                    PlayCombatHitVfx(action);
                    screenFlash?.Play(color);
                }

                hitBursts.Play(new Vector3(judgementPosition.x, JudgementStepSurfaceY, config.Rhythm.HitZ), color);
            }
        }

        private void PlayCombatHitVfx(GameplayAction action)
        {
            if (action == GameplayAction.OverheadClap)
            {
                PlayCombatHitVfx(overheadClapHitVfx, 0f, false);
                return;
            }

            bool usesSlashVfx = playerVisuals != null && playerVisuals.UsesSlashVisual;
            PrefabParticleVfxPool pool = usesSlashVfx ? slashHitVfx : punchHitVfx;
            if (pool == null)
            {
                return;
            }

            if (action == GameplayAction.BothPunch)
            {
                PlayCombatHitVfx(pool, -DualTargetOffsetX, usesSlashVfx);
                PlayCombatHitVfx(pool, DualTargetOffsetX, usesSlashVfx);
                return;
            }

            PlayCombatHitVfx(pool, 0f, usesSlashVfx);
        }

        private void PlayCombatHitVfx(PrefabParticleVfxPool pool, float xOffset, bool randomizeRotation)
        {
            Vector3 position = new Vector3(
                judgementPosition.x + xOffset,
                judgementPosition.y,
                judgementPosition.z - CombatVfxFrontOffset);
            Quaternion rotation = randomizeRotation ? NextSlashVfxRotation() : Quaternion.identity;
            pool.Play(position, rotation);
        }

        private Quaternion NextSlashVfxRotation()
        {
            uint state = vfxRandomState;
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            vfxRandomState = state;

            const float DegreesPerState = 360f / 16777216f;
            float angle = (state & 0x00FFFFFFu) * DegreesPerState;
            return Quaternion.Euler(0f, 0f, angle);
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
            return new Vector3(traveller.transform.position.x, traveller.VisualCenterY, config.Rhythm.HitZ);
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
