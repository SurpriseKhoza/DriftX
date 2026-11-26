# DriftX

Realistic drift racing game for Android, built with Unity (URP, Vulkan with OpenGL ES 3 fallback).

**Status: first prototype, never built or run yet.** Expect compile/build errors on the first CI run; fix them from the build log.

## What's in it
- Custom tire physics (`DriftCar.cs`): raycast suspension, slip-angle lateral force, friction circle, RWD, handbrake, counter-steer assist
- Drift scoring with chain multiplier (`DriftScore.cs`)
- Multi-touch controls (`TouchControls.cs`): steer left/right, gas, brake/reverse, handbrake
- Runtime quality tiers (`QualityTier.cs`): Low / Medium / High picked from system RAM
- Scene, materials and URP asset are generated in code at build time (`Assets/Editor/CIBuild.cs`)

## Building the APK (GitHub Actions)
Add these repository secrets (Settings > Secrets and variables > Actions):
- `UNITY_EMAIL`: your Unity ID email
- `UNITY_PASSWORD`: your Unity ID password
- `UNITY_LICENSE`: optional, only if you have a `.ulf` license file

Create `.github/workflows/build-android.yml` (see the workflow in the chat), then run it from the Actions tab. The APK is attached to the run as the `DriftX-Android` artifact.

## Tuning
All car values are public fields on `DriftCar` (grip, power, suspension, steering). Start with `rearGrip`, `maxDriveForce` and `handbrakeRearGrip` to change how easily it slides.
