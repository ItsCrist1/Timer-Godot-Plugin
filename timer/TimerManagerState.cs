using Godot;

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

public class TimerManagerState {
	public TimerCount TimerCount { get; set; }

	public TimerManagerState() => TimerCount = new();
}
