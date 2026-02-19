# Timer

![Version](https://img.shields.io/badge/version-4.2.4-blue?logo=V) ![MIT](https://img.shields.io/badge/License-MIT-brightgreen?logo=Baserow) ![Godot Plugin](https://img.shields.io/badge/Godot-Plugin-cyan?logo=godotengine)

A performant and comprehensive timer system for Godot 4.x C#

# Install

1. Clone [the repo](https://github.com/ItsCrist1/Timer-Godot-Plugin)

2. Make sure to enable the puglin inside Godot's UI at `Project -> Project Settings -> Plugins`

## Dependencies

- [Godot](https://godotengine.org/) 4.x
- [Gamebridge](https://github.com/ItsCrist1/GameBridge-Godot-Plugin) Plugin
- [FastEvent](https://github.com/ItsCrist1/FastEvent) Package

# Examples

## Common Use

### Simple
```cs
Timer timer = TimerManager.Create(new() { AutoStart = true }, 4f);
timer.Timeout += () => { GD.Print("Timed out!"); }
```

### Looping
```cs
Timer timer = TimerManager.CreateLooping(new(), 3f);
timer.Timeout += () => { GD.Print("Looped!"); }
```

### Arbitrary


# Attributions

Made by [ItsCrist1](https://github.com/ItsCrist1)

# License

Uses the [MIT](LICENSE.txt) license. Can be found online at https://mit-license.org/