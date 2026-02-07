using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;

using Godot;

public partial class TimerManager : Node {
	const string LOGGING_SETTING_KEY = "timer_plugin/log_messages";
	const string META_SCAN_KEY = "timer_plugin_scanned"; // for reflection

	const string PERFORMANCE_MONITOR_CATEGORY_NAME = "Timer";

	public static TimerManager Instance { get; private set; }

	static event Action<float> OnProcess, OnPhysicsProcess;
	static event Action OnClear;

	static Dictionary<Type, FieldInfo[]> timerScanFieldCache = new();
	
	static TimerManagerState state;
	bool logMessages;

	public static void RegisterTimer(Timer timer) {
		bool m = timer.Config.DoMonitor;

		switch(timer.Config.TickRate) {
			case TickRate.Process:
			OnProcess += timer.Tick;
			if(m) ++state.TimerCount.Process;
			break;

			case TickRate.PhysicsProcess:
			OnPhysicsProcess += timer.Tick;
			if(m) ++state.TimerCount.Physics;
			break;
			
			// a timer using an arbitrary source manages its own looping arbitrary timer
			case TickRate.Arbitrary: {
				Timer tickSourceTimer = CreateLooping (new() {
					MaxTime = timer.Config.TickFrequency,
					AutoStart = true,
					TickOnPause = true,
					DoMonitor = false
				});

				tickSourceTimer.OnTimeout += () => timer.Tick(timer.Config.TickFrequency);
				timer.tickSourceTimer = tickSourceTimer;

				if(m) ++state.TimerCount.Arbitrary;
			} break;
		}

		OnClear += timer.Dispose;

		if(!m) return;
			
		state.TimerCount.IsNode += timer.Config.IsNode ? 1u : 0u;
		state.TimerCount.AutoRefreshing += timer.Config.AutoRefresh ? 1u : 0u;
	}

	public static void UnregisterTimer(Timer timer) {
		bool m = timer.Config.DoMonitor;

		switch(timer.Config.TickRate) {
			case TickRate.Process: 
			OnProcess -= timer.Tick;
			if(m && state.TimerCount.Process > 0) --state.TimerCount.Process;
			break;

			case TickRate.PhysicsProcess: 
			OnPhysicsProcess -= timer.Tick;
			if(m && state.TimerCount.Physics > 0) --state.TimerCount.Physics;
			break;
			
			// lifecycle of arbitrary timers *should* be handled by themselves...hopefully
			case TickRate.Arbitrary: 
			if(m && state.TimerCount.Arbitrary > 0) --state.TimerCount.Arbitrary; 
			break;
		}

		OnClear -= timer.Dispose;

		if(!m) return;

		state.TimerCount.IsNode -= timer.Config.IsNode && state.TimerCount.IsNode > 0 ? 1u : 0u;
		state.TimerCount.AutoRefreshing -= timer.Config.AutoRefresh && state.TimerCount.AutoRefreshing > 0 ? 1u : 0u;
	}

	void AddPerformanceMonitors() {
		Performance.AddCustomMonitor($"{PERFORMANCE_MONITOR_CATEGORY_NAME}/Total Count", Callable.From(() => state.TimerCount.Total));
		Performance.AddCustomMonitor($"{PERFORMANCE_MONITOR_CATEGORY_NAME}/Process Count", Callable.From(() => state.TimerCount.Process));
		Performance.AddCustomMonitor($"{PERFORMANCE_MONITOR_CATEGORY_NAME}/Physics Count", Callable.From(() => state.TimerCount.Physics));
		Performance.AddCustomMonitor($"{PERFORMANCE_MONITOR_CATEGORY_NAME}/Arbitrary Count", Callable.From(() => state.TimerCount.Arbitrary));
		Performance.AddCustomMonitor($"{PERFORMANCE_MONITOR_CATEGORY_NAME}/Is Node Count", Callable.From(() => state.TimerCount.IsNode));
		Performance.AddCustomMonitor($"{PERFORMANCE_MONITOR_CATEGORY_NAME}/Auto Refreshing Count", Callable.From(() => state.TimerCount.AutoRefreshing));
	}

	static TimerManager() {
		state = new();
	}

	public override void _EnterTree() {
		Instance = this;
		state ??= new();

		ProcessMode = ProcessModeEnum.Always;

		logMessages = (bool)ProjectSettings.GetSetting(LOGGING_SETTING_KEY);

		AddPerformanceMonitors();
	}

	public override void _Process(double delta) {
		float dt = (float)delta;

		OnProcess?.Invoke(dt);
	}

	public override void _PhysicsProcess(double delta) {
		float dt = (float)delta;

		OnPhysicsProcess?.Invoke(dt);
	}

	public override void _ExitTree() {
		OnClear?.Invoke();

		OnProcess = OnPhysicsProcess = null;
		OnClear = null;
		state = null;

		Instance = null;

		RemovePerformanceMonitors();
	}

	void RemovePerformanceMonitors() {
		Performance.RemoveCustomMonitor($"{PERFORMANCE_MONITOR_CATEGORY_NAME}/Total Count");
		Performance.RemoveCustomMonitor($"{PERFORMANCE_MONITOR_CATEGORY_NAME}/Process Count");
		Performance.RemoveCustomMonitor($"{PERFORMANCE_MONITOR_CATEGORY_NAME}/Physics Count");
		Performance.RemoveCustomMonitor($"{PERFORMANCE_MONITOR_CATEGORY_NAME}/Arbitrary Count");
		Performance.RemoveCustomMonitor($"{PERFORMANCE_MONITOR_CATEGORY_NAME}/Is Node Count");
		Performance.RemoveCustomMonitor($"{PERFORMANCE_MONITOR_CATEGORY_NAME}/Auto Refreshing Count");
	}
	
	public static void ScanRegisterTimers(Node owner) {
		if(owner.HasMeta(META_SCAN_KEY)) return;
		owner.SetMeta(META_SCAN_KEY, true);
		
		Type ownerType = owner.GetType();

		if(!timerScanFieldCache.TryGetValue(ownerType, out FieldInfo[] fields)) {
			const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy;
			fields = ownerType.GetFields(flags).Where(f => f.FieldType == typeof(Timer)).ToArray();
			timerScanFieldCache[ownerType] = fields;
		}

		foreach(FieldInfo field in fields)
			if(field.GetValue(owner) is Timer timer) {
				Action disposeAction = null;
				
				disposeAction = () => {
					timer.Dispose();
					owner.TreeExiting -= disposeAction;
				};

				owner.TreeExiting += disposeAction;
			}
	}

	public static Timer Create(TimerConfig Config=null)
		=> new (Config?.Clone() ?? new());

	public static Timer CreateLooping(TimerConfig Config=null) {
		TimerConfig newConfig = Config?.Clone() ?? new();
		newConfig.AutoRefresh = true;
		return new (newConfig);
	}

	public static Timer CreateOneShotTimer(float maxTime, Action onTimeout, TimerConfig Config=null) {
		TimerConfig newConfig = Config?.Clone() ?? new();
		newConfig.MaxTime = maxTime;
		newConfig.AutoStart = newConfig.DisposeOnTimeout = true;
		
		Timer timer = new (newConfig);

		timer.OnTimeout += onTimeout;

		return timer;
	}
}
