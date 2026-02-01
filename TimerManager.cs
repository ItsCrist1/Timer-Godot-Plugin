using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;

using Godot;

public partial class TimerManager : Node {
	const string LOGGING_SETTING_KEY = "timer_plugin/log_messages";
	const string META_SCAN_KEY = "timer_plugin_scanned"; // for reflection

	const string GENERIC_LOG_TIMER_MANAGER = "Timer Manager Log";
	const string GENERIC_LOG_TIMERS_NAME = "Normal";

	const string PERFORMANCE_MONITOR_CATEGORY_NAME = "Timer";


	public static TimerManager Instance { get; private set; }

	// allows for declaration of timers before initialization in `_EnterTree`
	static LinkedList<Timer> PendingTimers = new();
	LinkedList<Timer> Timers = new();

	static event Action<float> OnProcess, OnPhysicsProcess;

	// for the reflection dispose on `_ExitTree` system
	static Dictionary<Type, FieldInfo[]> timerScanFieldCache = new();
	
	bool logMessages;

	public static LinkedListNode<Timer> RegisterTimer(Timer timer, TickRate TickRate, float TickFrequency) {
		switch(TickRate) {
			case TickRate.Process: OnProcess += timer.Tick; break;
			case TickRate.PhysicsProcess: OnPhysicsProcess += timer.Tick; break;
			
			// a timer using an arbitrary source manages its own looping arbitrary timer
			case TickRate.Arbitrary: {
				Timer tickSourceTimer = CreateLooping (new() {
					MaxTime = TickFrequency,
					AutoStart = true,
					TickOnPause = true
				});

				tickSourceTimer.OnTimeout += () => timer.Tick(TickFrequency);
				timer.tickSourceTimer = tickSourceTimer;
			} break;
		}

		return Instance == null 
				? PendingTimers.AddLast(timer) 
				: Instance.Timers.AddLast(timer);
	}

	public static void UnregisterTimer(Timer timer, TickRate TickRate) {
		switch(TickRate) {
			case TickRate.Process: OnProcess -= timer.Tick; break;
			case TickRate.PhysicsProcess: OnPhysicsProcess -= timer.Tick; break;
			
			// case TickRate.Arbitrary: break;
			// lifecycle of arbitrary timers *should* be handled by themselves...hopefully
		}
	}

	void MigratePendingTimers(LinkedList<Timer> PendingTimers, LinkedList<Timer> Timers, string timersLogName) {
		foreach(Timer timer in PendingTimers)
			timer.Migrate(Timers.AddLast(timer));

		if(logMessages && PendingTimers.Count != 0) 
			GD.Print($"{GENERIC_LOG_TIMER_MANAGER}:\n{timersLogName} timers moved from pending static list to active one: {PendingTimers.Count}");

		PendingTimers.Clear();
	}

	void AddPerformanceMonitors() {
		Performance.AddCustomMonitor($"{PERFORMANCE_MONITOR_CATEGORY_NAME}/Active_Count", Callable.From(() => Timers.Count));
	}

	public override void _EnterTree() {
		Instance = this;
		ProcessMode = ProcessModeEnum.Always;

		MigratePendingTimers(PendingTimers, Instance.Timers, GENERIC_LOG_TIMERS_NAME);

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

	void ClearTimers(LinkedList<Timer> Timers, string timersLogName) {
		foreach(Timer timer in Timers)
			timer.Dispose();

		if(logMessages)
			GD.Print($"{GENERIC_LOG_TIMER_MANAGER}:\n{timersLogName} timers have been cleared: {Timers.Count}");

		Timers.Clear();
	}

	void RemovePerformanceMonitors() {
    	Performance.RemoveCustomMonitor($"{PERFORMANCE_MONITOR_CATEGORY_NAME}/Active_Count");
	}

	public override void _ExitTree() {
		ClearTimers(Timers, GENERIC_LOG_TIMERS_NAME);

		RemovePerformanceMonitors();

		OnProcess = OnPhysicsProcess = null;
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
		=> new (Config?.Duplicate() as TimerConfig ?? new());

	public static Timer CreateLooping(TimerConfig Config=null) {
		TimerConfig newConfig = Config?.Duplicate() as TimerConfig ?? new();
		newConfig.AutoRefresh = true;
		return new (newConfig);
	}

	public static Timer CreateOneShotTimer(float maxTime, Action onTimeout, TimerConfig Config=null) {
		TimerConfig newConfig = Config?.Duplicate() as TimerConfig ?? new();
		newConfig.MaxTime = maxTime;
		newConfig.AutoStart = newConfig.DisposeOnTimeout = true;
		
		Timer timer = new (newConfig);

		timer.OnTimeout += onTimeout;

		return timer;
	}
}
