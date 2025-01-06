using System.Collections.Generic;
using System.Linq;
using BizHawk.Common;

namespace BizHawk.Emulation.Cores.Computers.Commodore64.Cartridge
{
	// Westermann Learning mapper.
	// Starts up with both banks enabled, any read to DFxx
	// turns off the high bank by bringing GAME high.
	// I suspect that the game loads by copying all hirom to
	// the RAM underneath (BASIC variable values probably)
	// and then disables once loaded.
	internal sealed class Mapper000B : CartridgeDevice
	{
		private readonly byte[] _rom = new byte[0x4000];

		public Mapper000B(IReadOnlyList<CartridgeChip> chips)
		{
			validCartridge = false;
			_rom.AsSpan().Fill(0xFF);
			var data = chips.Single(x => x.Address == 0x8000 && x.Bank == 0);
			data.ConvertDataToBytes().AsSpan().CopyTo(_rom);
		}

		protected override void SyncStateInternal(Serializer ser)
		{
			// Nothing to save
		}

		public override int Peek8000(int addr)
		{
			return _rom[addr];
		}

		public override int PeekA000(int addr)
		{
			return _rom[addr | 0x2000];
		}

		public override int Read8000(int addr)
		{
			return _rom[addr];
		}

		public override int ReadA000(int addr)
		{
			return _rom[addr | 0x2000];
		}

		public override int ReadDF00(int addr)
		{
			pinGame = true;
			return base.ReadDF00(addr);
		}
	}
}
