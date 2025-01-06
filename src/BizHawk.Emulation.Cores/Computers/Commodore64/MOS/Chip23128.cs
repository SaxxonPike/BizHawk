namespace BizHawk.Emulation.Cores.Computers.Commodore64.MOS
{
	// ROM chips
	public sealed class Chip23128
	{
		private readonly byte[] _rom;

		public Chip23128()
		{
			_rom = new byte[0x4000];
		}

		public Chip23128(byte[] data) : this()
		{
			Flash(data);
		}

		public void Flash(byte[] data)
		{
			// ensures ROM is mirrored
			for (var i = 0; i < _rom.Length; i += data.Length)
			{
				data.AsSpan().CopyTo(_rom.AsSpan(i));
			}
		}

		public byte Peek(ushort addr)
		{
			return _rom[addr & 0x3FFF];
		}

		public byte Read(ushort addr)
		{
			return _rom[addr & 0x3FFF];
		}
	}
}
