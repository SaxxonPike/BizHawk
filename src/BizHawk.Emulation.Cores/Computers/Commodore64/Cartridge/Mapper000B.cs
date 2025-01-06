using System.Collections.Generic;
using System.Linq;
using BizHawk.Common;

namespace BizHawk.Emulation.Cores.Computers.Commodore64.Cartridge;

// Westermann Learning mapper.
// Starts up with both banks enabled, any read to DFxx
// turns off the high bank by bringing GAME high.
// I suspect that the game loads by copying all hirom to
// the RAM underneath (BASIC variable values probably)
// and then disables once loaded.
internal sealed class Mapper000B : CartridgeDevice
{
	private readonly byte[] _rom;

	public Mapper000B(IReadOnlyList<CartridgeChip> chips)
	{
		var chip = chips.Single(x => x.Address == 0x8000 && x.Bank == 0);
		_rom = CreateRom(chip, 0x4000);
	}

	protected override void SyncStateInternal(Serializer ser)
	{
		// Nothing to save
	}

	public override byte Peek8000(ushort addr)
	{
		return _rom[addr];
	}

	public override byte PeekA000(ushort addr)
	{
		return _rom[addr | 0x2000];
	}

	public override byte Read8000(ushort addr)
	{
		return _rom[addr];
	}

	public override byte ReadA000(ushort addr)
	{
		return _rom[addr | 0x2000];
	}

	public override byte ReadDF00(ushort addr)
	{
		pinGame = true;
		return base.ReadDF00(addr);
	}
}