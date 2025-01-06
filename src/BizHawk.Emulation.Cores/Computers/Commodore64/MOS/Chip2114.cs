using BizHawk.Common;

namespace BizHawk.Emulation.Cores.Computers.Commodore64.MOS
{
	// used as Color RAM in C64
	public sealed class Chip2114
	{
		private byte[] _ram = new byte[0x400];

		public Chip2114()
		{
			HardReset();
		}

		public void HardReset()
		{
			_ram.AsSpan().Clear();
		}

		public byte Peek(ushort addr)
		{
			return _ram[addr & 0x3FF];
		}

		public void Poke(ushort addr, byte val)
		{
			_ram[addr & 0x3FF] = unchecked((byte)(val & 0xF));
		}

		public byte Read(ushort addr)
		{
			return _ram[addr & 0x3FF];
		}

		public void SyncState(Serializer ser)
		{
			ser.Sync(nameof(_ram), ref _ram, useNull: false);
		}

		public void Write(ushort addr, byte val)
		{
			_ram[addr & 0x3FF] = unchecked((byte) (val & 0xF));
		}
	}
}
