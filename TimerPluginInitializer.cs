#if TOOLS
using Godot;

[Tool]
public partial class TimerPluginInitializer : EditorPlugin {
	const string SINGLETON_NAME = "TimerManager";
    const string SINGLETON_SCRIPT = "TimerManager.cs";

	public override void _EnterTree() {
		AddAutoloadSingleton(
			SINGLETON_NAME,
			((Resource)GetScript())
				.ResourcePath.GetBaseDir()
				.PathJoin(SINGLETON_SCRIPT)
		);
	}

	public override void _ExitTree()
		=> RemoveAutoloadSingleton(SINGLETON_NAME);
}
#endif
