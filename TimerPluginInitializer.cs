#if TOOLS
using Godot;

[Tool]
public partial class TimerPluginInitializer : EditorPlugin {
	const string SINGLETON_NAME = "TimerManager";
    const string SINGLETON_SCRIPT = "TimerManager.cs";

	const string CUSTOM_TYPE_NAME = "Custom Timer";
	const string CUSTOM_TYPE_SCRIPT = "timer/TimerNode.cs";

	public override void _EnterTree() {
		string path = ((Resource)GetScript())
						.ResourcePath.GetBaseDir();

		AddAutoloadSingleton(
			SINGLETON_NAME,
			path.PathJoin(SINGLETON_SCRIPT)
		);

        AddCustomType(
			CUSTOM_TYPE_NAME, 
			nameof(Node), 
			GD.Load<Script>(path.PathJoin(CUSTOM_TYPE_SCRIPT)), 
			EditorInterface.Singleton.GetBaseControl().GetThemeIcon(nameof(Godot.Timer), "EditorIcons")
		);
	}

	public override void _ExitTree() {
		RemoveAutoloadSingleton(SINGLETON_NAME);
		RemoveCustomType(CUSTOM_TYPE_NAME);
	}
}
#endif
