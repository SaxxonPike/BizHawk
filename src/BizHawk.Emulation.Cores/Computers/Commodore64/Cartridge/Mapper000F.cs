using System.Collections.Generic;
using BizHawk.Common;

namespace BizHawk.Emulation.Cores.Computers.Commodore64.Cartridge;

// This is a mapper used commonly by System 3. It is
// also utilized by the short-lived C64 Game System.

// Bank select is DExx. You select them by writing to the
// register DE00+BankNr. For example, bank 01 is a write
// to DE01.
internal class Mapper000F : CartridgeDevice
{
	private readonly byte[][] _banks; // 8000

	private byte _bankMask;
	private byte _bankNumber;

	private byte[] _currentBank;

	public Mapper000F(IReadOnlyList<CartridgeChip> chips)
	{
		pinGame = true;
		pinExRom = false;

		var banks = CreateRoms(chips)[0x8000];
		_bankMask = banks.Mask;
		_banks = banks.Data;

		BankSet(0);
	}

	protected override void SyncStateInternal(Serializer ser)
	{
		ser.Sync("BankMask", ref _bankMask);
		ser.Sync("BankNumber", ref _bankNumber);

		if (ser.IsReader)
		{
			BankSet(_bankNumber);
		}
	}

	protected void BankSet(byte index)
	{
		_bankNumber = unchecked((byte) (index & _bankMask));
		UpdateState();
	}

	public override byte Peek8000(ushort addr)
	{
		return _currentBank[addr];
	}

	public override void PokeDE00(ushort addr, byte val)
	{
		BankSet(unchecked((byte) addr));
	}

	public override byte Read8000(ushort addr)
	{
		return _currentBank[addr];
	}

	private void UpdateState()
	{
		_currentBank = _banks[_bankNumber];
	}

	public override byte ReadDE00(ushort addr)
	{
		BankSet(0);

		return 0;
	}

	public override void WriteDE00(ushort addr, byte val)
	{
		BankSet(unchecked((byte) addr));
	}
}