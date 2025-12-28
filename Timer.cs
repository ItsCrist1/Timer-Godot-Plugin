#if TOOLS
using Godot;

[Tool]
public partial class Timer : EditorPlugin {
	const string SINGLETON_NAME = "TimerManager";
    const string SINGLETON_PATH = "res://addons/timer/TimerManager.cs";

	public override void _EnterTree()
		=> AddAutoloadSingleton(SINGLETON_NAME, SINGLETON_PATH);

	public override void _ExitTree()
		=> RemoveAutoloadSingleton(SINGLETON_NAME);
}
#endif
