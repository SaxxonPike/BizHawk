using BizHawk.Common;

namespace BizHawk.Emulation.Cores.Computers.Commodore64.MOS
{
	// DRAM for the c64
	// 4164 = 64 kbit
	// 4464 = 256 kbit
	// 4864 = 512 kbit

	// for purposes of simplification we'll just
	// use one 4864, the C64 can use sets of 4164 or
	// 4464 typically

	// memory is striped 00/FF at intervals of 0x40
	public sealed class Chip4864
	{
		private byte[] _ram;

		public Chip4864()
		{
			_ram = new byte[0x10000];
			HardReset();
		}

		public void HardReset()
		{
			// stripe the ram
			_ram.AsSpan().Clear();

			for (var i = 0x40; i < 0x10000; i += 0x80)
			{
				_ram.AsSpan(i, 0x40).Fill(0xFF);
			}
		}

		public byte Peek(ushort addr)
		{
			return _ram[addr];
		}

		public void Poke(ushort addr, byte val)
		{
			_ram[addr] = val;
		}

		public byte Read(ushort addr)
		{
			return _ram[addr];
		}

		public void SyncState(Serializer ser)
		{
			ser.Sync(nameof(_ram), ref _ram, useNull: false);
		}

		public void Write(ushort addr, byte val)
		{
			_ram[addr] = unchecked((byte)val);
		}
	}
}
