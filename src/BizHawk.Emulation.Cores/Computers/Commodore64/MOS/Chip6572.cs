namespace BizHawk.Emulation.Cores.Computers.Commodore64.MOS
{
	// pal n / drean - TODO correct?
	public static class Chip6572
	{
		private static readonly int Cycles = 65;
		private static readonly int ScanWidth = Cycles * 8;
		private static readonly int Lines = 312;
		private static readonly int VblankStart = 0x12C % Lines;
		private static readonly int VblankEnd = 0x00F % Lines;
		private static readonly int HblankOffset = 24;
		private static readonly int HblankStart = (0x18C + HblankOffset) % ScanWidth;
		private static readonly int HblankEnd = (0x1F0 + HblankOffset) % ScanWidth;

		private static int[] GetTiming() => Vic.TimingBuilder_XRaster(0x19C, 0x200, ScanWidth, 0x18C, 8);
		private static int[] GetFetch(int[] timing) => Vic.TimingBuilder_Fetch(timing, 0x174);
		private static int[] GetBa(int[] fetch) => Vic.TimingBuilder_BA(fetch);
		private static int[] GetAct(int[] timing) => Vic.TimingBuilder_Act(timing, 0x004, 0x154, 0x16C);

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
				14328225 / 14,
				HblankStart, HblankEnd,
				VblankStart, VblankEnd,
				borderType,
				908,
				1000);
		}
	}
}
