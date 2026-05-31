# Audio Manager

A lightweight, singleton-based Audio Manager for Unity 6 with BGM crossfade, 2D/3D SFX pooling, AudioMixer volume control, and editor tooling for `AudioConstants` code generation.

---

## Features

- **Singleton** — persists across scenes via `DontDestroyOnLoad`; auto-instantiates from `Resources/AudioManager` if not present in the scene
- **BGM** — crossfade between tracks, fade-out stop, and immediate stop
- **SFX** — 2D and 3D positional playback with optional `Transform` follow
- **Object Pooling** — pre-warmed BGM and SFX source pools, configurable at inspector level
- **AudioMixer Integration** — per-group volume control via exposed parameters
- **AudioConstants Generator** — one-click editor button generates a static constants class from all registered clips
- **Odin Inspector** — searchable dropdown in `AudioClipData` for quick clip assignment

---

## Requirements

| Dependency | Version |
| --- | --- |
| Unity | 6000.0+ |
| [UniTask](https://github.com/Cysharp/UniTask) | 2.5.10+ |
| [Odin Inspector](https://odininspector.com) | 3.x |

---

### How to add submodule by command line
```bash
git submodule add  <url> <relative_path>
```
Example
```bash
git submodule add git@github.com:draftsama/audio-manager.git Assets/audio-manager
```

---

## AudioMixer Setup

The AudioManager requires a **NewAudioMixer** asset with three groups and three exposed parameters:

```text
Master
├── BGM
└── SFX
```

**Expose the following parameters** (right-click the Volume knob in each group → *Expose to script*), then rename them exactly:

| Group | Exposed Parameter Name |
| --- | --- |
| Master | `MasterVolume` |
| BGM | `BGMVolume` |
| SFX | `SFXVolume` |

Assign the mixer to the **AudioManager** component's `Audio Mixer` field.

---

## Setup

1. Add the `AudioManager` prefab (or `MonoBehaviour`) to your scene — or place it in `Resources/AudioManager` for auto-instantiation.
2. Assign your **AudioMixer** asset.
3. In the **Audio Clips** list, add entries and select clip names from the searchable dropdown.
4. Click **Generate AudioConstants.cs** to create type-safe name constants.
5. Set pool sizes and BGM fade duration as needed.

---

## Usage

### BGM

```csharp
// Play (loops by default, crossfades from any current BGM)
AudioInstance bgm = AudioManager.PlayBGM(AudioConstants.my_bgm_track);

// Stop with fade-out
AudioManager.StopBGM();

// Stop immediately
AudioManager.StopBGMImmediate();

// Change BGM mixer volume (0–1 linear → converted to dB internally)
AudioManager.SetBGMVolume(0.8f);
```

### SFX — 2D

```csharp
// One-shot
AudioManager.PlaySFX(AudioConstants.item_acquired_01);

// Looping (returns handle to stop later)
AudioInstance loop = AudioManager.PlaySFX(AudioConstants.my_loop, volume: 0.6f, loop: true);
loop.Stop();
```

### SFX — 3D Positional

```csharp
// At a world position
AudioManager.PlaySFX(AudioConstants.item_drop_07, transform.position, volume: 1f);

// Following a transform
AudioManager.PlaySFX(
    AudioConstants.heavy_mechanical_button_04,
    transform.position,
    volume: 1f,
    loop: true,
    minDistance: 1f,
    maxDistance: 20f,
    followTransform: transform
);
```

### AudioInstance

`AudioInstance` is the handle returned by every play call. Use it to control the sound after it starts:

```csharp
AudioInstance inst = AudioManager.PlaySFX(AudioConstants.magic_button_04);
inst.Pause();
inst.Resume();
inst.Stop();
inst.SetFollowTransform(someTransform);
inst.StopFollowing();

bool playing = inst.IsPlaying();
float vol    = inst.GetVolume();
inst.SetVolume(0.5f);
```

### Volume Control

```csharp
// Master / SFX mixer volume (0–1)
AudioManager.SetMasterVolume(1f);
AudioManager.SetSFXVolume(0.7f);
AudioManager.SetBGMVolume(0.5f);
```

---

## AudioConstants

After registering clips in the inspector, click **Generate AudioConstants.cs** in the editor. This creates/updates `AudioConstants.cs` with one `const string` per clip:

```csharp
// Auto-generated — do not edit manually
public partial class AudioConstants
{
    public const string item_acquired_01 = "item_acquired_01";
    public const string magic_button_04  = "magic_button_04";
    // ...
}
```

Use these constants everywhere instead of raw strings to avoid typos.

---

## Inspector Reference

| Field | Description |
| --- | --- |
| `Audio Mixer` | The Unity AudioMixer asset with BGM / SFX / Master groups |
| `Initial BGM Pool Size` | Number of pre-warmed BGM `AudioSource` slots (min 2) |
| `Initial SFX Pool Size` | Number of pre-warmed SFX `AudioSource` slots |
| `BGM Fade Duration` | Duration (seconds) of BGM crossfade and fade-out |
| `Audio Clips` | List of name → `AudioClip` mappings used by all play calls |

---

## Assembly Definitions

| Assembly | Path | Purpose |
| --- | --- | --- |
| `AM.Runtime` | `Runtime/AM.Runtime.asmdef` | Runtime code — reference this in your game assemblies |
| `AM.Editor` | `Editor/AM.Editor.asmdef` | Editor-only tooling — not included in builds |
