using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;

using Godot;

public partial class TimerManager : Node {
	const string LOGGING_SETTING_KEY = "timer_plugin/log_messages";
	const string MONITOR_EVENTS_SETTINGS_KEY = "timer_plugin/monitor_events_time";
	const string META_SCAN_KEY = "timer_plugin_scanned"; // for reflection

	const string PERFORMANCE_MONITOR_CATEGORY_NAME = "Timer";

	public static TimerManager Instance { get; private set; }

	static event Action<float> OnProcess, OnPhysicsProcess;
	static event Action OnClear;

	Dictionary<string, Callable> Monitors;

	static Dictionary<Type, FieldInfo[]> timerScanFieldCache;
	
	static TimerManagerState state, tempState;

	Timer eventUpdateTimer;
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

		timer.OnStart += OnStart;
		timer.OnTick += OnTick;
		timer.OnTimeout += OnTimeout;
		timer.OnStop += OnStop;
			
		state.TimerCount.IsNode += timer.Config.IsNode ? 1u : 0u;
		state.TimerCount.AutoRefreshing += timer.Config.AutoRefresh ? 1u : 0u;
	}

	static void OnStart() => ++tempState.TimerEventFireCount.OnStart;
	static void OnTick() => ++tempState.TimerEventFireCount.OnTick;
	static void OnTimeout() => ++tempState.TimerEventFireCount.OnTimeout;
	static void OnStop() => ++tempState.TimerEventFireCount.OnStop;

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

		timer.OnStart -= OnStart;
		timer.OnTick -= OnTick;
		timer.OnTimeout -= OnTimeout;
		timer.OnStop -= OnStop;

		state.TimerCount.IsNode -= timer.Config.IsNode && state.TimerCount.IsNode > 0 ? 1u : 0u;
		state.TimerCount.AutoRefreshing -= timer.Config.AutoRefresh && state.TimerCount.AutoRefreshing > 0 ? 1u : 0u;
	}

	void InitializeMonitors()
		=> Monitors ??= new() {
			{ "Total Count", Callable.From(() => state.TimerCount.Total) },
			{ "Process Count", Callable.From(() => state.TimerCount.Process) },
			{ "Physics Count", Callable.From(() => state.TimerCount.Physics) },
			{ "Arbitrary Count", Callable.From(() => state.TimerCount.Arbitrary) },
			{ "Is Node Count", Callable.From(() => state.TimerCount.IsNode) },
			{ "Auto Refreshing Count", Callable.From(() => state.TimerCount.AutoRefreshing) },

			{ "Events/Total/On Start", Callable.From(() => state.TimerEventFireCount.OnStart) },
			{ "Events/Total/On Tick", Callable.From(() => state.TimerEventFireCount.OnTick) },
			{ "Events/Total/On Timeout", Callable.From(() => state.TimerEventFireCount.OnTimeout) },
			{ "Events/Total/On Stop", Callable.From(() => state.TimerEventFireCount.OnStop) },

			{ "Events/Temporary/On Start", Callable.From(() => tempState.TimerEventFireCount.OnStart) },
			{ "Events/Temporary/On Tick", Callable.From(() => tempState.TimerEventFireCount.OnTick) },
			{ "Events/Temporary/On Timeout", Callable.From(() => tempState.TimerEventFireCount.OnTimeout) },
			{ "Events/Temporary/On Stop", Callable.From(() => tempState.TimerEventFireCount.OnStop) }
		};

	void RegisterPerformanceMonitors(bool add=true) {
		foreach(KeyValuePair<string,Callable> i in Monitors)
			if(add)
				Performance.AddCustomMonitor($"{PERFORMANCE_MONITOR_CATEGORY_NAME}/{i.Key}", i.Value);
			else
				Performance.RemoveCustomMonitor($"{PERFORMANCE_MONITOR_CATEGORY_NAME}/{i.Key}");
	}

	static TimerManager() {
		state = new();
		tempState = new();

		timerScanFieldCache = new();
	}

	public override void _EnterTree() {
		Instance = this;

		state ??= new();
		tempState ??= new();

		ProcessMode = ProcessModeEnum.Always;

		logMessages = (bool)ProjectSettingsManager.Get(LOGGING_SETTING_KEY);

		InitializeMonitors();
		RegisterPerformanceMonitors();

		eventUpdateTimer = CreateArbitrary(
			new() { AlwaysTick = true },
			(float)ProjectSettingsManager.Get(MONITOR_EVENTS_SETTINGS_KEY)
		);

		eventUpdateTimer.OnTick += onTempEventsReset;
	}

	void onTempEventsReset() {
		state.TimerEventFireCount += tempState.TimerEventFireCount;
		tempState.TimerEventFireCount.Reset();
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

		RegisterPerformanceMonitors(false);
		Monitors = null;

		eventUpdateTimer.Dispose();

		state = tempState = null;
		Instance = null;
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

	public static Timer Create(float MaxTime, TimerConfig Config=null) {
		TimerConfig newConfig = Config?.Clone() ?? new();
		newConfig.MaxTime = MaxTime;
		return new (newConfig);
	}

	public static Timer CreateLooping(TimerConfig Config=null) {
		TimerConfig newConfig = Config?.Clone() ?? new();
		newConfig.AutoRefresh = true;
		return new (newConfig);
	}

	public static Timer CreateArbitrary(TimerConfig Config=null, float updateInterval=.1f) {
		TimerConfig newConfig = Config?.Clone() ?? new();
		newConfig.TickRate = TickRate.Arbitrary;
		newConfig.TickFrequency = updateInterval;

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
