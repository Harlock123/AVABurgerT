# AGENTS.md

Notes for anyone, human or AI agent, changing this code. `README.md` covers playing the game, its controls and
what the tests check; this file covers how the code is put together and the rules that keep it working.

## What this is

A burger-building platform game in the style of Data East's 1982 arcade game BurgerTime, in C# on .NET 10 and
Avalonia 12, drawn through Avalonia's `DrawingContext`. One self-contained executable per platform (Windows, macOS and Linux, x64 and
ARM). Everything in it is original: the pixel art is drawn in code, and the sounds and tunes are synthesised by
`tools/make_sounds.py`.

## Commands

```bash
dotnet build AVABurgerT.slnx                      # the SDK version is in global.json (10.0.400 or later)
dotnet test AVABurgerT.slnx                       # every test runs headless: no window opens
dotnet run --project src/AVABurgerT               # play it; add -- --no-audio for silence
BURGER_SHOTS=$PWD/Images dotnet test tests/AVABurgerT.Tests --filter ScreenshotTests   # refresh the README's pictures
python3 tools/make_sounds.py                      # rebuild src/AVABurgerT/Sounds (standard-library Python only)
```

`Directory.Build.props` sets `TreatWarningsAsErrors`, nullable reference types and implicit usings for every
project: a warning fails the build, so keep it at zero.

## Layout

- `src/AVABurgerT/Engine/`: the game, with no Avalonia drawing in it.
  - `GameEngine.cs` runs the states (title, attract, play, pause, game over, initials, high scores) and the
    fixed-step loop; `GameEngine.Play.cs` has the levels, the chef, the enemies and the dropping layers.
  - `Entities.cs` has the chef, the enemies (sausage, egg, pickle) and `Controls`.
  - `AttractPilot.cs` is the computer player, used by the attract mode and the tests.
  - `InputHandler.cs` (keys) and `ControllerManager.cs` (SDL2 gamepad) produce `Controls`.
  - `HighScoreTable.cs`, `InitialsEntry.cs` and `SoundManager.cs` do what their names say.
- `src/AVABurgerT/Models/`: `Field.cs` (the 224x256 portrait screen, its floors and `Step`), `Kitchen.cs` (the
  burger layers, ladders and plates of each level) and `Sprites.cs` (the pixel art).
- `src/AVABurgerT/Views/`: `GameCanvas` (split over partial files) draws a fixed 1024x768 picture scaled to the
  window and runs the 60 Hz timer.
- `src/AVABurgerT/Audio/`: the OpenAL Soft audio engine, shared with the author's other AVA games.
- `src/AVABurgerT/Sounds/`: the generated WAV files.
- `tests/AVABurgerT.Tests/`: xUnit v3 with `Avalonia.Headless.XUnit`: `GameplayTests` and `ScreenshotTests`.

## Rules that keep it working

- **Fixed steps.** `GameEngine` advances in fixed 1/60 s steps (`Field.Step`), independent of the frame rate, so
  tests can play whole levels deterministically from a seed. Draw random numbers from the engine's seeded
  `Random` (`new GameEngine(seed, table)`), never from `Random.Shared`. `Kitchen.Build` seeds its own `Random`
  from the level number, so a level always has the same layout.
- **Engine first, drawing second.** Gameplay belongs in `Engine/` and `Models/`, where tests can reach it without a
  window. `Views/` only reads the engine and draws it.
- **Input goes through `InputHandler`.** Movement is a `Dir` (`Up`, `Down`, `Left`, `Right`), not a signed `dy`:
  the arrows or WASD and the gamepad's D-pad or stick (negative stick Y is up) must give the same `Dir`. Test new
  controls through `InputHandler`, not by building `Controls` directly.
- **SDL under one lock.** `ControllerManager` starts, polls and stops SDL only inside `lock (Sdl)`. The tests
  create many handlers in parallel, and unlocked SDL calls corrupt its heap.
- **Audio.** OpenAL Soft through Silk.NET. `Directory.Build.targets` swaps in the official Windows `soft_oal.dll`
  from `Audio/native/`, which needs no Visual C++ runtime. In a virtual machine `OpenAlConfig` writes a larger
  mixing period to `ALSOFT_CONF` so the sound doesn't break up.
- **Tests never touch the player's files.** High scores live in the app-data folder (`AVABurgerT`). A
  `GameEngine` built without a table loads that file, so tests always pass a `HighScoreTable` in a temporary
  folder, and the screenshot tests use a silent `SoundManager`.

## Making a change

- Add or update tests with every gameplay change. `dotnet test` must pass, with no warnings.
- If the change shows on screen, refresh the screenshots with the command above, and look at them.
- Update `README.md` (features, controls, the test list) to match.
- New sounds go into `tools/make_sounds.py`, not as hand-edited WAV files.

## CI and releases

`.github/workflows/build.yml` builds and tests every push and pull request. It then publishes self-contained
single-file builds for win-x64/x86/arm64, linux-x64/arm64/arm and osx-x64/arm64. Every run keeps the builds as
workflow artifacts. Only pushes to `main` replace the assets of the `latest` pre-release, and a `v*` tag makes a
proper release. After a release it asks the portfolio site to rebuild, if the `SITE_REFRESH_TOKEN` secret is set.
