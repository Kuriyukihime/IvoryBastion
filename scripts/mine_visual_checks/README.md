# Attached mine visual checks

```powershell
dotnet run --project scripts/mine_visual_checks/MineVisualChecks.csproj -c Release
```

Compiles the production spider visual, shared target marker, mine presentation
and mine rules with small Unity scene/network stand-ins. No game or sound device
is launched. The visual sources cannot call damage/explosion APIs in this harness.

Checks cover exact body-local attachment tracking through movement, rotation,
nonuniform scaling and teleportation; stale snapshots/world positions; late body
resolution; shared markers for multiple mines; expiry, lost streams, dead targets,
cap/terminal cleanup and duplicate terminal suppression. A directly primed mine
retains its last valid world anchor if the weapon killed its carrier.

`--export <path.json>` exports the production clamp bone rotations and marker
line geometry for actual-mesh proof renders. Stand-ins establish managed tracking
and cleanup behavior; they do not replace live Unity terrain, depth or networking
testing.
