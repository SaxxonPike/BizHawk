using System.Collections.Generic;
using System.Linq;

using BizHawk.Common;

namespace BizHawk.Emulation.Cores.Computers.Commodore64.Cartridge;

// Prophet 64 cartridge. Because we can.
// 32 banks of 8KB.
// DFxx = status register, xxABBBBB. A=enable cart, B=bank
// Thanks to VICE team for the info: http://vice-emu.sourceforge.net/vice_15.html
internal class Mapper002B : CartridgeDevice
{
	private readonly byte[] _rom;

	private int _romOffset;
	private bool _romEnabled;

	public Mapper002B(IReadOnlyList<CartridgeChip> chips)
	{
		pinExRom = false;
		pinGame = true;
		_rom = new byte[0x40000];
			
		foreach (var chip in chips)
		{
			chip.Data.Span.CopyTo(_rom.AsSpan(chip.Bank * 0x2000));
		}
	}

	protected override void SyncStateInternal(Serializer ser)
	{
		ser.Sync("RomOffset", ref _romOffset);
		ser.Sync("RomEnabled", ref _romEnabled);
	}

	public override void HardReset()
	{
		_romEnabled = true;
		_romOffset = 0;
	}

	public override byte Peek8000(ushort addr)
	{
		return _rom[_romOffset | (addr & 0x1FFF)];
	}

	public override byte PeekDF00(ushort addr)
	{
		// For debugging only. The processor does not see this.
		return unchecked((byte) (((_romOffset >> 13) & 0x1F) | (_romEnabled ? 0x20 : 0x00)));
	}

	public override void PokeDF00(ushort addr, byte val)
	{
		_romOffset = (val & 0x1F) << 13;
		_romEnabled = (val & 0x20) != 0;
	}

	public override byte Read8000(ushort addr)
	{
		return _rom[_romOffset | (addr & 0x1FFF)];
	}

	public override byte ReadDF00(ushort addr)
	{
		return 0x00;
	}

	public override void WriteDF00(ushort addr, byte val)
	{
		_romOffset = (val & 0x1F) << 13;
		_romEnabled = (val & 0x20) != 0;
	}
}