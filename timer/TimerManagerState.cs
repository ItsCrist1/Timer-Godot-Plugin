public class TimerCount {
	public uint Total {
		get => Process + Physics + Arbitrary;
	}
	public uint Process { get; set; }
	public uint Physics { get; set; }
	public uint Arbitrary { get; set; }
	public uint AutoRefreshing { get; set; }
	public uint IsNode { get; set; }
}

public class TimerEventFireCount {
	public uint OnStart { get; set; }
	public uint OnTick { get; set; }
	public uint OnTimeout { get; set; }
	public uint OnStop { get; set; }

    public void Reset()
        => OnStart
         = OnTick
         = OnTimeout
         = OnStop
         = 0u;
}

public class TimerManagerState {
	public TimerCount TimerCount { get; set; }
	public TimerEventFireCount TimerEventFireCount { get; set; }

	public TimerManagerState() {
		TimerCount = new();
		TimerEventFireCount = new();
	}
}
