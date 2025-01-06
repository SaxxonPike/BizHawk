using System.Collections.Generic;
using BizHawk.Common;

namespace BizHawk.Emulation.Cores.Computers.Commodore64.Cartridge;

// Standard Commodore mapper - no bank switching, always present.
// Ultimax cartridge dumps also report as this mapper.
// Some ROMs are a flat 0x2000 or 0x4000, some are split ROMs, it
// really depends on the dump. We try to support the most common
// configurations:
//
// $8000: size up to 0x2000 goes into bank A, next 0x2000 goes into bank B;
//        if the image is smaller than the ROM size, it is mirrored
// $A000: always goes into bank B, is mirrored as necessary
internal sealed class Mapper0000 : CartridgeDevice
{
	private readonly byte[] _romA;
	private int _romAMask;

	private readonly byte[] _romB;
	private int _romBMask;

	public Mapper0000(IReadOnlyList<CartridgeChip> chips, bool game, bool exrom)
	{
		pinGame = game;
		pinExRom = exrom;

		// default to empty banks
		_romA = new byte[1];
		_romB = new byte[1];
		_romA[0] = 0xFF;
		_romB[0] = 0xFF;

		foreach (var chip in chips)
		{
			if (chip.Address == 0x8000)
			{
				if (chip.Data.Length < 0x2000)
				{
					_romAMask = GetMaskFromSize(chip.Data.Length);
					_romA = CreateMirroredRom(chip, 0x2000);
				}
				else if (chip.Data.Length < 0x4000)
				{
					_romAMask = 0x2000;
					_romA = chip.Data.Span.Slice(0, 0x2000).ToArray();
					var romBSize = chip.Data.Length - 0x2000;
					_romBMask = GetMaskFromSize(romBSize);
					_romB = CreateMirroredRom(chip, 0x2000);
				}
				else
				{
					throw new InvalidOperationException("Chip A size is too large (over 0x4000)");
				}
			}
			else if (chip.Address == 0xA000 || chip.Address == 0xE000)
			{
				if (chip.Data.Length >= 0x2000)
				{
					throw new InvalidOperationException("Chip B size is too large (over 0x2000)");
				}

				_romBMask = GetMaskFromSize(chip.Data.Length);
				_romB = CreateMirroredRom(chip, 0x2000);
			}
		}
	}

	protected override void SyncStateInternal(Serializer ser)
	{
		ser.Sync("RomMaskA", ref _romAMask);
		ser.Sync("RomMaskB", ref _romBMask);
	}

	public override int Peek8000(int addr)
	{
		return _romA[addr & _romAMask];
	}

	public override int PeekA000(int addr)
	{
		return _romB[addr & _romBMask];
	}

	public override int Read8000(int addr)
	{
		return _romA[addr & _romAMask];
	}

	public override int ReadA000(int addr)
	{
		return _romB[addr & _romBMask];
	}
}