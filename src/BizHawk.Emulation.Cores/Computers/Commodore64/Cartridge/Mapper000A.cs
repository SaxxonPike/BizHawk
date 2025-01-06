using System.Collections.Generic;
using System.Linq;

using BizHawk.Common;
using BizHawk.Emulation.Common;

namespace BizHawk.Emulation.Cores.Computers.Commodore64.Cartridge;

// Epyx Fastload. Uppermost page is always visible at DFxx.
// They use a capacitor that is discharged by accesses to DExx
// to pull down EXROM. Also, accesses to LOROM while it is active
// discharge the capacitor.
// Thanks to VICE team for the info: http://vice-emu.sourceforge.net/vice_15.html
internal class Mapper000A : CartridgeDevice
{
	// This constant differs depending on whose research you reference. TODO: Verify.
	private const int RESET_CAPACITOR_CYCLES = 512;

	private readonly byte[] _rom;
	private int _capacitorCycles;

	public Mapper000A(IReadOnlyList<CartridgeChip> chips)
	{
		var chip = chips.Single(x => x.Address == 0x8000 && x.Bank == 0);
		_rom = CreateRom(chip, 0x2000);
		pinGame = true;
	}

	protected override void SyncStateInternal(Serializer ser)
	{
		ser.Sync("CapacitorCycles", ref _capacitorCycles);
	}

	public override void ExecutePhase()
	{
		pinExRom = !(_capacitorCycles > 0);
		if (!pinExRom)
		{
			_capacitorCycles--;
		}
	}

	public override void HardReset()
	{
		_capacitorCycles = RESET_CAPACITOR_CYCLES;
		base.HardReset();
	}

	public override byte Peek8000(ushort addr)
	{
		return _rom[addr & 0x1FFF];
	}

	public override byte PeekDE00(ushort addr)
	{
		return 0x00;
	}

	public override byte PeekDF00(ushort addr)
	{
		return _rom[(addr & 0xFF) | 0x1F00];
	}

	public override byte Read8000(ushort addr)
	{
		_capacitorCycles = RESET_CAPACITOR_CYCLES;
		return _rom[addr & 0x1FFF];
	}

	public override byte ReadDE00(ushort addr)
	{
		_capacitorCycles = RESET_CAPACITOR_CYCLES;
		return 0x00;
	}

	public override byte ReadDF00(ushort addr)
	{
		return _rom[(addr & 0xFF) | 0x1F00];
	}

	public override IEnumerable<MemoryDomain> CreateMemoryDomains()
	{
		yield return new MemoryDomainByteArray(
			name: "ROM Image",
			endian: MemoryDomain.Endian.Little,
			data: _rom,
			writable: false,
			wordSize: 1
		);
	}
}