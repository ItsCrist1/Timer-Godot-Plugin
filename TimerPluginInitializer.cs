#if TOOLS
using Godot;

[Tool]
public partial class TimerPluginInitializer : EditorPlugin {
	const string SINGLETON_NAME = "TimerManager";
    const string SINGLETON_SCRIPT = "TimerManager.cs";

	const string CUSTOM_TYPE_NAME = "Custom Timer";
	const string CUSTOM_TYPE_SCRIPT = "TimerNode.cs";

	public override void _EnterTree() {
		AddAutoloadSingleton(
			SINGLETON_NAME,
			((Resource)GetScript())
				.ResourcePath.GetBaseDir()
				.PathJoin(SINGLETON_SCRIPT)
		);

		Script script = GD.Load<Script>(
            ((Resource)GetScript())
                .ResourcePath.GetBaseDir()
                .PathJoin(CUSTOM_TYPE_SCRIPT)
        );
        Texture2D icon = EditorInterface.Singleton.GetBaseControl().GetThemeIcon(nameof(Godot.Timer), "EditorIcons");

        AddCustomType(CUSTOM_TYPE_NAME, nameof(Node), script, icon);
	}

	public override void _ExitTree() {
		RemoveAutoloadSingleton(SINGLETON_NAME);
		RemoveCustomType(CUSTOM_TYPE_NAME);
	}
}
#endif
