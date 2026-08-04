# Neon Pulse Fitness

Playable desktop rhythm-fitness prototype for Unity 2022.3 LTS. Open
`Assets/NeonPulse/Scenes/NeonPulseGameplay.unity` and press Play. The gameplay
controller is already present in the scene; no manual references, prefabs, music
assets, or tracking plugins are required.

## Controls

- `Q` / Left Arrow: left punch (cyan)
- `E` / Right Arrow: right punch (magenta)
- `F` or Q+E together: both-hands punch
- Hold `S` / Down Arrow until the overhead obstacle passes: duck
- Hold `Space` / `W` until the low obstacle passes: jump
- Hold `A` until the wall passes: dodge left
- Hold `D` until the wall passes: dodge right
- `R` / Enter: restart

## Architecture

- `IPlayerInputProvider` isolates gameplay from keyboard/webcam implementations.
- `BeatmapConfig` is a ScriptableObject data boundary with a runtime sample chart.
- Spawning and movement use `AudioSettings.dspTime` to avoid cumulative beat drift.
- A fixed-capacity pool owns all travelling notes and obstacles.
- `RhythmScore` publishes score/judgement events to the HUD and feedback systems.
- All visuals, TextMeshPro UI, particles, materials, and fallback audio are generated
  at runtime from original code and Unity primitives.

The current implementation intentionally has no Odin Inspector or DOTween references
because neither dependency is installed in this repository. This keeps the prototype
compilable without paid or external packages as required by `AI_CONTEXT.md`.
