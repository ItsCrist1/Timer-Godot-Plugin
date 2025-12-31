using System;
using System.Collections.Generic;

using Godot;

public partial class TimerManager : Node {
	const string INVALID_CHECK_SETTING_KEY = "timer_plugin/invalid_timer_check_interval";
	const string LOGGING_SETTING_KEY = "timer_plugin/log_messages";
	const float TIME_BETWEEN_INVALID_TIMER_CHECKS_DEFAULT = 3f;

	const string GENERIC_LOG_TIMER_MANAGER = "Timer Manager Log";
	const string GENERIC_LOG_TIMERS_NAME = "Normal";
	const string GENERIC_LOG_ARBITRARY_TIMERS_NAME = "Arbitrary";

	const string PERFORMANCE_MONITOR_CATEGORY_NAME = "Timer";


	public static TimerManager Instance { get; private set; }

	static LinkedList<WeakReference<Timer>> PendingTimers = new();
	static LinkedList<WeakReference<Timer>> PendingArbitraryTimers = new();
	LinkedList<WeakReference<Timer>> Timers = new();
	LinkedList<WeakReference<Timer>> ArbitraryTimers = new();

	static event Action<float> OnProcess, OnPhysicsProcess;

	Timer checkForInvalidTimers = CreateLooping();
	bool logMessages;

	public static LinkedListNode<WeakReference<Timer>> RegisterTimer(Timer timer, TickRate TickRate, float TickFrequency) {
		switch(TickRate) {
			case TickRate.Process: OnProcess += timer.Tick; break;
			case TickRate.PhysicsProcess: OnPhysicsProcess += timer.Tick; break;
			
			case TickRate.Arbitrary: {
				Timer tickSourceTimer = CreateLooping (new() {
					MaxTime = TickFrequency,
					AutoStart = true,
					TickOnPause = true
				});

				tickSourceTimer.OnTimeout += () => timer.Tick(TickFrequency);
				timer.tickSourceTimer = tickSourceTimer;

				WeakReference<Timer> tickSourceWeakRef = new (tickSourceTimer);

				if(Instance == null) PendingArbitraryTimers.AddLast(tickSourceWeakRef);
				else Instance.ArbitraryTimers.AddLast(tickSourceWeakRef);
			} break;
		}

		WeakReference<Timer> timerWeakRef = new (timer);

		return Instance == null 
				? PendingTimers.AddLast(timerWeakRef) 
				: Instance.Timers.AddLast(timerWeakRef);
	}

	public static void UnregisterTimer(Timer timer, TickRate TickRate) {
		switch(TickRate) {
			case TickRate.Process: OnProcess -= timer.Tick; break;
			case TickRate.PhysicsProcess: OnPhysicsProcess -= timer.Tick; break;
			
			// case TickRate.Arbitrary: break;
			// lifecycle of arbitrary timers *should* be handled by themselves... 
		}
	}

	void MigratePendingTimers(LinkedList<WeakReference<Timer>> PendingTimers, LinkedList<WeakReference<Timer>> Timers, string timersLogName) {
		foreach(WeakReference<Timer> timer in PendingTimers)
			Timers.AddLast(timer);

		if(logMessages && PendingTimers.Count != 0) 
			GD.Print($"{GENERIC_LOG_TIMER_MANAGER}:\n{timersLogName} timers moved from pending static list to active one: {PendingTimers.Count}");

		PendingTimers.Clear();
	}

	void AddPerformanceMonitors() {
		Performance.AddCustomMonitor($"{PERFORMANCE_MONITOR_CATEGORY_NAME}/Active_Count", Callable.From(() => Timers.Count));
		Performance.AddCustomMonitor($"{PERFORMANCE_MONITOR_CATEGORY_NAME}/Arbitrary_Count", Callable.From(() => ArbitraryTimers.Count));
	}
	
	void setupProjectSettings() {
		bool changed = false;

		if(!ProjectSettings.HasSetting(INVALID_CHECK_SETTING_KEY)) {
			ProjectSettings.SetSetting(INVALID_CHECK_SETTING_KEY, TIME_BETWEEN_INVALID_TIMER_CHECKS_DEFAULT);
			changed = true;
		}

		if(!ProjectSettings.HasSetting(LOGGING_SETTING_KEY)) {
			ProjectSettings.SetSetting(LOGGING_SETTING_KEY, true);
			changed = true;
		}

		if(changed)
			ProjectSettings.Save();
	}

	public override void _EnterTree() {
		Instance = this;
		ProcessMode = ProcessModeEnum.Always;

		MigratePendingTimers(PendingTimers, Instance.Timers, GENERIC_LOG_TIMERS_NAME);
		MigratePendingTimers(PendingArbitraryTimers, Instance.ArbitraryTimers, GENERIC_LOG_ARBITRARY_TIMERS_NAME);

		checkForInvalidTimers.OnTimeout += RemoveInvalidTimers;

		setupProjectSettings();

		checkForInvalidTimers.Start(
			(float)ProjectSettings.GetSetting(INVALID_CHECK_SETTING_KEY)
		);

		logMessages = (bool)ProjectSettings.GetSetting(LOGGING_SETTING_KEY);

		AddPerformanceMonitors();
	}

	void RemoveInvalidTimersFromList(LinkedList<WeakReference<Timer>> Timers, string timersLogName) {
		LinkedListNode<WeakReference<Timer>> currentNode = Timers.First;
		uint removedCount = 0;

		while(currentNode != null) {
			LinkedListNode<WeakReference<Timer>> nextNode = currentNode.Next;
			
			if(!currentNode.Value.TryGetTarget(out Timer _)) {
				Timers.Remove(currentNode);
				++removedCount;
			}

			currentNode = nextNode;
		}

		if(logMessages && removedCount != 0)
			GD.Print($"{GENERIC_LOG_TIMER_MANAGER}:\n{timersLogName} timers have had some timers removed for being invalid: {removedCount}");
	}

	void RemoveInvalidTimers() {
		if(Instance == null) return;

		RemoveInvalidTimersFromList(Timers, GENERIC_LOG_TIMERS_NAME);
		RemoveInvalidTimersFromList(ArbitraryTimers, GENERIC_LOG_ARBITRARY_TIMERS_NAME);
	}

	public override void _Process(double delta) {
		float dt = (float)delta;

		OnProcess?.Invoke(dt);
	}

    public override void _PhysicsProcess(double delta) {
		float dt = (float)delta;

		OnPhysicsProcess?.Invoke(dt);
    }

	void ClearTimers(LinkedList<WeakReference<Timer>> Timers, string timersLogName) {
		foreach(WeakReference<Timer> timerRef in Timers)
			if(timerRef.TryGetTarget(out Timer timer))
				timer.Dispose();

		if(logMessages)
			GD.Print($"{GENERIC_LOG_TIMER_MANAGER}:\n{timersLogName} timers have been cleared: {Timers.Count}");

		Timers.Clear();
	}

	void RemovePerformanceMonitors() {
    	Performance.RemoveCustomMonitor($"{PERFORMANCE_MONITOR_CATEGORY_NAME}/Active_Count");
    	Performance.RemoveCustomMonitor($"{PERFORMANCE_MONITOR_CATEGORY_NAME}/Arbitrary_Count");
	}

	public override void _ExitTree() {
		ClearTimers(Timers, GENERIC_LOG_TIMERS_NAME);
		ClearTimers(ArbitraryTimers, GENERIC_LOG_ARBITRARY_TIMERS_NAME);

		RemovePerformanceMonitors();

		if(checkForInvalidTimers != null) {
			checkForInvalidTimers.Dispose();
			checkForInvalidTimers = null;
		}

		OnProcess = OnPhysicsProcess = null;
		Instance = null;
	}

	public static Timer Create(TimerConfig Config=null)
		=> new (Config?.Duplicate() as TimerConfig ?? new());

	public static Timer CreateLooping(TimerConfig Config=null) {
		TimerConfig newConfig = Config?.Duplicate() as TimerConfig ?? new();
		newConfig.AutoRefresh = true;
		return new (newConfig);
	}

	public static Timer CreateOneShotTimer(float maxTime, Action onTimeout, TimerConfig Config=null) {
		TimerConfig newConfig = Config.Duplicate() as TimerConfig ?? new();
		newConfig.MaxTime = maxTime;
		newConfig.AutoStart = newConfig.DisposeOnTimeout = true;
		
		Timer timer = new (newConfig);

		timer.OnTimeout += onTimeout;

		return timer;
	}
}
