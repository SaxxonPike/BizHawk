using System.Collections.Generic;
using BizHawk.Common;

namespace BizHawk.Emulation.Cores.Computers.Commodore64.Cartridge;

internal sealed class Mapper0007 : CartridgeDevice
{
	private readonly byte[][] _banks; 

	private byte _bankNumber;
	private bool _disabled;

	// Fun Play mapper
	// bank switching is done from DE00
	public Mapper0007(IEnumerable<CartridgeChip> chips, bool game, bool exrom)
	{
		const int bankSize = 0x2000;

		pinGame = game;
		pinExRom = exrom;
		_disabled = false;

		// load data into the banks from the list
		var dummyBank = new byte[bankSize];
		dummyBank.AsSpan().Fill(0xFF);
		_banks = new byte[16][];
			
		foreach (var chip in chips)
		{
			var bank = CreateRom(chip, bankSize);
			_banks[chip.Bank] = bank;
		}

		_bankNumber = 0;
	}

	protected override void SyncStateInternal(Serializer ser)
	{
		ser.Sync("BankNumber", ref _bankNumber);
		ser.Sync("Disabled", ref _disabled);
	}

	public override byte Peek8000(ushort addr)
	{
		if (!_disabled)
		{
			return _banks[_bankNumber][addr];
		}

		return base.Read8000(addr);
	}

	public override void PokeDE00(ushort addr, byte val)
	{
		if (addr == 0)
		{
			var tempBank = unchecked((byte)((val & 0x1) << 3));
			tempBank |= (byte)((val & 0x38) >> 3);
			_bankNumber = tempBank;
			if (val == 0x86)
			{
				_disabled = true;
			}
		}
	}

	public override byte Read8000(ushort addr)
	{
		if (!_disabled)
		{
			return _banks[_bankNumber][addr];
		}

		return base.Read8000(addr);
	}

	public override void WriteDE00(ushort addr, byte val)
	{
		if (addr == 0)
		{
			byte tempBank = (byte)((val & 0x1) << 3);
			tempBank |= (byte)((val & 0x38) >> 3);
			_bankNumber = tempBank;
			if (val == 0x86)
			{
				_disabled = true;
			}
		}
	}
}