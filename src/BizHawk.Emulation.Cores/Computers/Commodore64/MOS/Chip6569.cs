namespace BizHawk.Emulation.Cores.Computers.Commodore64.MOS
{
	// vic pal
	public static class Chip6569
	{
		private static readonly int Cycles = 63;
		private static readonly int ScanWidth = Cycles * 8;
		private static readonly int Lines = 312;
		private static readonly int VblankStart = 0x120 % Lines;
		private static readonly int VblankEnd = 0x00F % Lines;
		private static readonly int HblankOffset = 24;
		private static readonly int HblankStart = (0x178 + HblankOffset) % ScanWidth;
		private static readonly int HblankEnd = (0x1F0 + HblankOffset) % ScanWidth;

		private static int[] GetTiming() => Vic.TimingBuilder_XRaster(0x194, 0x1F8, ScanWidth, -1, -1);
		private static int[] GetFetch(int[] timing) => Vic.TimingBuilder_Fetch(timing, 0x164);
		private static int[] GetBa(int[] fetch) => Vic.TimingBuilder_BA(fetch);
		private static int[] GetAct(int[] timing) => Vic.TimingBuilder_Act(timing, 0x004, 0x14C, 0x164);

		private static int[][] GetPipeline()
		{
			var timing = GetTiming();
			var fetch = GetFetch(timing);
			var ba = GetBa(fetch);
			var act = GetAct(timing);
			return [ timing, fetch, ba, act ];
		}

		public static Vic Create(C64.BorderType borderType)
		{
			return new Vic(
				Cycles, Lines,
				GetPipeline(),
				17734472 / 18,
				HblankStart, HblankEnd,
				VblankStart, VblankEnd,
				borderType,
				7375,
				7882);
		}
	}
}
