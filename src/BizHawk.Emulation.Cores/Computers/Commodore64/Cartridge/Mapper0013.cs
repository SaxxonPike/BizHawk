using System.Collections.Generic;
using BizHawk.Common;
using BizHawk.Emulation.Common;

namespace BizHawk.Emulation.Cores.Computers.Commodore64.Cartridge;

// Mapper for a few Domark and HES Australia games.
// It seems a lot of people dumping these have remapped
// them to the Ocean mapper (0005) but this is still here
// for compatibility.
//
// Bank select is DE00, bit 7 enabled means to disable
// ROM in 8000-9FFF.

internal sealed class Mapper0013 : CartridgeDevice
{
	private const int BankSize = 0x2000;

	private readonly byte[][] _banks;

	private readonly byte _bankMask;
	private readonly int _bankCount;
	private byte _bankNumber;
	private byte[] _currentBank;
	private bool _romEnable;

	public Mapper0013(IEnumerable<CartridgeChip> chips)
	{
		pinGame = true;
		pinExRom = false;
		_romEnable = true;

		var banks = CreateRoms(chips)[0x8000];
		_bankMask = banks.Mask;
		_banks = banks.Data;
		_bankCount = _bankMask + 1;

		// Start with bank 0.
		BankSet(0);
	}

	public override IEnumerable<MemoryDomain> CreateMemoryDomains()
	{
		yield return new MemoryDomainDelegate(
			name: "ROM",
			size: _bankCount * BankSize,
			endian: MemoryDomain.Endian.Little,
			peek: a => _banks[a >> 13][a & 0x1FFF],
			poke: (a, d) => _banks[a >> 13][a & 0x1FFF] = d,
			wordSize: 1
		);
	}

	protected override void SyncStateInternal(Serializer ser)
	{
		ser.Sync("BankNumber", ref _bankNumber);
		ser.Sync("ROMEnable", ref _romEnable);

		if (ser.IsReader)
		{
			BankSet(_bankNumber | (_romEnable ? 0x00 : 0x80));
		}
	}

	private void BankSet(int index)
	{
		_bankNumber = unchecked((byte) (index & _bankMask));
		_romEnable = (index & 0x80) == 0;
		UpdateState();
	}

	public override byte Peek8000(ushort addr) => 
		_currentBank[addr];

	public override void PokeDE00(ushort addr, byte val)
	{
		if (addr == 0x00)
		{
			BankSet(val);
		}
	}

	public override byte Read8000(ushort addr) => 
		_currentBank[addr];

	private void UpdateState()
	{
		_currentBank = _banks[_bankNumber];

		(pinExRom, pinGame) = _romEnable
			? (false, true)
			: (true, true);
	}

	public override void WriteDE00(ushort addr, byte val)
	{
		if (addr == 0x00)
		{
			BankSet(val);
		}
	}
}