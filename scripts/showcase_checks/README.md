# Selection preview and transformation audio checks

Run from the repository root:

```powershell
dotnet run --project scripts/showcase_checks/ShowcaseChecks.csproj -c Release
```

The harness compiles the production selection showcase, display rules, audio
dispatcher, PCM decoder/mixer, output-session recovery and dialogue gate. It embeds
the actual deploy/pack WAV assets under the runtime logical resource names. Unity
scene objects and the platform audio output are replaced by small stand-ins; it
does not launch Risk of Rain 2 or open a sound device.

It checks:

- Selection audio works without a local survivor body, uses the effects bus and
  respects master mute, mod volume, effects volume, game gain and headless mode.
- The shared audio session retires a failed output and recovers after its retry
  delay without requiring a spawned body.
- A selection plays its mechanical cue once at unfolding, replays on reselection
  and stops its preview voice when disabled.
- The preview's screen-left offset follows camera orientation and its original
  parent anchor without accumulating drift, including across 1,000 camera culls.
- Disabling restores the original local position and removes the camera callback;
  gameplay-owned models remain untouched.
- The real mechanical WAVs decode as finite 48 kHz PCM with sample headroom, and
  the deployed preview scales down smoothly.

These checks cover managed behavior and asset loading. They do not establish
in-game composition, Unity camera ordering, perceived sound quality or live audio
device compatibility.
