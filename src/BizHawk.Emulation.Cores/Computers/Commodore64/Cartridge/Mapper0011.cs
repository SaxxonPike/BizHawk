﻿using System.Collections.Generic;
using BizHawk.Common;

namespace BizHawk.Emulation.Cores.Computers.Commodore64.Cartridge;

// This mapper comes from Dinamic. It is in fact identical
// to the System 3 mapper (000F) except that bank switching is
// done by reads to the DExx region instead of writes.
// This is why mapper 0011 inherits directly from 000F.
internal class Mapper0011 : Mapper000F
{
	public Mapper0011(IReadOnlyList<CartridgeChip> chips)
		: base(chips)
	{
		// required to pass information to base class
	}

	protected override void SyncStateInternal(Serializer ser)
	{
		// Nothing to save
	}

	public override void PokeDE00(ushort addr, byte val)
	{
		// do nothing
	}

	public override byte ReadDE00(ushort addr)
	{
		BankSet(unchecked((byte) addr));
		return base.ReadDE00(addr);
	}

	public override void WriteDE00(ushort addr, byte val)
	{
		// do nothing
	}
}