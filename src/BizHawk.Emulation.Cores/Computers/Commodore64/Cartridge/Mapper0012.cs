using System.Collections.Generic;
using System.Linq;
using BizHawk.Common;

namespace BizHawk.Emulation.Cores.Computers.Commodore64.Cartridge;

internal sealed class Mapper0012 : CartridgeDevice
{
	private readonly byte[] _bankMain;

	private readonly byte[][] _bankHigh;

	private byte[] _bankHighSelected;

	private byte _bankIndex;

	// Zaxxon and Super Zaxxon cartridges
	// - read to 8xxx selects bank 0 in A000-BFFF
	// - read to 9xxx selects bank 1 in A000-BFFF
	public Mapper0012(IEnumerable<CartridgeChip> chips)
	{
		_bankHigh = new byte[2][];
		var dummyBank = new byte[0x2000];

		// create dummy bank just in case
		dummyBank.AsSpan().Fill(0xFF);

		_bankMain = dummyBank;
		_bankHigh[0] = dummyBank;
		_bankHigh[1] = dummyBank;

		// load in the banks
		foreach (var chip in chips)
		{
			if (chip.Address == 0x8000)
			{
				_bankMain = CreateMirroredRom(chip, 0x2000);
			}
			else if (chip.Address is 0xA000 or 0xE000)
			{
				if (chip.Bank >= 2)
				{
					throw new InvalidOperationException("Cartridge has more than the two banks supported by this mapper");
				}
				_bankHigh[chip.Bank] = CreateRom(chip, 0x2000);
			}
		}

		// set both pins low for 16k rom config
		pinExRom = false;
		pinGame = false;
	}

	protected override void SyncStateInternal(Serializer ser)
	{
		ser.Sync("BankIndex", ref _bankIndex);
		if (ser.IsReader)
		{
			_bankHighSelected = _bankHigh[_bankIndex];
		}
	}

	public override byte Peek8000(ushort addr)
	{
		return _bankMain[addr];
	}

	public override byte PeekA000(ushort addr)
	{
		return _bankHighSelected[addr];
	}

	public override byte Read8000(ushort addr)
	{
		_bankIndex = unchecked((byte) ((addr & 0x1000) >> 12));
		_bankHighSelected = _bankHigh[_bankIndex];
		return _bankMain[addr];
	}

	public override byte ReadA000(ushort addr)
	{
		return _bankHighSelected[addr];
	}
}