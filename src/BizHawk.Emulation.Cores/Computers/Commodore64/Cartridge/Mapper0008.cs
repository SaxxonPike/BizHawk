using System.Collections.Generic;
using BizHawk.Common;

namespace BizHawk.Emulation.Cores.Computers.Commodore64.Cartridge;

internal sealed class Mapper0008 : CartridgeDevice
{
	private readonly byte[][] _banks;

	private byte _bankMask;
	private byte _bankNumber;
	private bool _disabled;
	private byte _latchedval;

	// SuperGame mapper
	// bank switching is done from DF00
	public Mapper0008(IEnumerable<CartridgeChip> chips)
	{
		pinGame = false;
		pinExRom = false;

		_bankMask = 0x03;
		_disabled = false;
		_latchedval = 0;

		var dummyBank = new byte[0x4000];
		dummyBank.AsSpan().Fill(0xFF);
		_banks = new byte[4][];
		_banks.AsSpan().Fill(dummyBank);

		// load data into the banks from the list
		foreach (var chip in chips)
		{
			var bank = CreateRom(chip, 0x4000);
			_banks[chip.Bank] = bank;
		}

		BankSet(0);
	}

	protected override void SyncStateInternal(Serializer ser)
	{
		ser.Sync("BankMask", ref _bankMask);
		ser.Sync("BankNumber", ref _bankNumber);
		ser.Sync("Disabled", ref _disabled);
		ser.Sync("Latchedvalue", ref _latchedval);

		if (ser.IsReader)
		{
			BankSet(_bankNumber);
		}
	}

	private void BankSet(byte index)
	{
		if (!_disabled)
		{
			_bankNumber = unchecked((byte)(index & _bankMask));
			pinExRom = (index & 0x4) > 0;
			pinGame = (index & 0x4) > 0;
			_disabled = (index & 0x8) > 0;
			_latchedval = index;
		}
	}

	public override byte Peek8000(ushort addr)
	{
		return _banks[_bankNumber][addr];
	}

	public override byte PeekA000(ushort addr)
	{
		return _banks[_bankNumber][addr + 0x2000];
	}

	public override void PokeDF00(ushort addr, byte val)
	{
		if (addr == 0)
		{
			BankSet(val);
		}
	}

	public override byte Read8000(ushort addr)
	{
		return _banks[_bankNumber][addr];
	}

	public override byte ReadA000(ushort addr)
	{
		return _banks[_bankNumber][addr + 0x2000];
	}

	public override void WriteDF00(ushort addr, byte val)
	{
		if (addr == 0)
		{
			BankSet(val);
		}
	}

	public override byte ReadDF00(ushort addr)
	{
		return _latchedval;
	}
}