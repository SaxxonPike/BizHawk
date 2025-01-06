namespace BizHawk.Emulation.Cores.Computers.Commodore64.Serial
{
	public sealed partial class Drive1541
	{
		private int _overflowFlagDelaySr;

		private byte CpuPeek(ushort addr)
		{
			return unchecked((byte)Peek(addr));
		}

		private byte CpuRead(ushort addr)
		{
			return unchecked((byte)Read(addr));
		}

		private void CpuWrite(ushort addr, byte val)
		{
			Write(addr, val);
		}

		private bool ViaReadClock()
		{
			var inputClock = ReadMasterClk();
			var outputClock = ReadDeviceClk();
			return !(inputClock && outputClock);
		}

		private bool ViaReadData()
		{
			var inputData = ReadMasterData();
			var outputData = ReadDeviceData();
			return !(inputData && outputData);
		}

		private bool ViaReadAtn()
		{
			var inputAtn = ReadMasterAtn();
			return !inputAtn;
		}

		private byte ReadVia1PrA()
		{
			return unchecked((byte) _bitHistory);
		}

		private byte ReadVia1PrB()
		{
			return unchecked((byte) ((_motorStep & 0x03) | (_motorEnabled ? 0x04 : 0x00) | (_sync ? 0x00 : 0x80) |
				(_diskWriteProtected ? 0x00 : 0x10)));
		}

		public byte Peek(ushort addr) =>
			(addr & 0xFC00) switch
			{
				0x1800 => Via0.Peek(addr),
				0x1C00 => Via1.Peek(addr),
				>= 0x8000 => DriveRom.Peek(unchecked((ushort)(addr & 0x3FFF))),
				< 0x800 => _ram[addr & 0x7FF],
				_ => unchecked((byte)(addr >> 8))
			};

		public byte PeekVia0(ushort addr)
		{
			return Via0.Peek(addr);
		}

		public byte PeekVia1(ushort addr)
		{
			return Via1.Peek(addr);
		}

		public void Poke(ushort addr, byte val)
		{
			switch (addr & 0xFC00)
			{
				case 0x1800:
					Via0.Poke(addr, val);
					break;
				case 0x1C00:
					Via1.Poke(addr, val);
					break;
				case < 0x800:
					_ram[addr & 0x7FF] = val;
					break;
			}
		}

		public void PokeVia0(ushort addr, byte val)
		{
			Via0.Poke(addr, val);
		}

		public void PokeVia1(ushort addr, byte val)
		{
			Via1.Poke(addr, val);
		}

		public byte Read(ushort addr) =>
			(addr & 0xFC00) switch
			{
				0x1800 => Via0.Read(addr),
				0x1C00 => Via1.Read(addr),
				>= 0x8000 => DriveRom.Read(unchecked((ushort) (addr & 0x3FFF))),
				< 0x800 => _ram[addr],
				_ => unchecked((byte) ((addr >> 8) & 0xFF))
			};

		public void Write(ushort addr, byte val)
		{
			switch (addr & 0xFC00)
			{
				case 0x1800:
					Via0.Write(addr, val);
					break;
				case 0x1C00:
					Via1.Write(addr, val);
					break;
				case < 0x800:
					_ram[addr & 0x7FF] = val;
					break;
			}
		}

		public override bool ReadDeviceClk()
		{
			var viaOutputClock = (Via0.DdrB & 0x08) != 0 && (Via0.PrB & 0x08) != 0;
			return !viaOutputClock;
		}

		public override bool ReadDeviceData()
		{
			// PB1 (input not pulled up)
			var viaOutputData = (Via0.DdrB & 0x02) != 0 && (Via0.PrB & 0x02) != 0;
			// inverted from c64, input, not pulled up to PB7/CA1
			var viaInputAtn = ViaReadAtn();
			// PB4 (input not pulled up)
			var viaOutputAtna = (Via0.DdrB & 0x10) != 0 && (Via0.PrB & 0x10) != 0;

			return !(viaOutputAtna ^ viaInputAtn) && !viaOutputData;
		}

		public override bool ReadDeviceLight()
		{
			return _driveLightOffTime > 0;
		}
	}
}
